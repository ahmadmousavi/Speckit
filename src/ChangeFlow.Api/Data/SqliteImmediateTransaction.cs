using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Api.Data;

public sealed class SqliteImmediateTransaction
{
    public const int BusyTimeoutMilliseconds = 5000;
    public const int MaximumAttempts = 3;

    public async Task ExecuteAsync(
        IServiceScopeFactory scopeFactory,
        Func<ChangeFlowDbContext, Task> operation,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync<object?>(scopeFactory, async db =>
        {
            await operation(db);
            return null;
        }, cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        IServiceScopeFactory scopeFactory,
        Func<ChangeFlowDbContext, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ChangeFlowDbContext>();

                var connection = (SqliteConnection)db.Database.GetDbConnection();
                await connection.OpenAsync(cancellationToken);
                connection.DefaultTimeout = BusyTimeoutMilliseconds / 1000;

                await using var transaction = connection.BeginTransaction(deferred: false);
                await db.Database.UseTransactionAsync(transaction, cancellationToken);

                var result = await operation(db);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (SqliteException ex) when (
                attempt < MaximumAttempts &&
                (ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("SQLite remained locked after the bounded retry limit.");
    }
}
