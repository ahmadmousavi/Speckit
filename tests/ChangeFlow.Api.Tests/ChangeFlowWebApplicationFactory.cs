using ChangeFlow.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeFlow.Api.Tests;

public sealed class ChangeFlowWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"changeflow-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("DevelopmentJwt:SigningKey", "ChangeFlow-Development-Test-Signing-Key-32Chars!");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ChangeFlow"] = $"Data Source={_databasePath}",
                ["DevelopmentJwt:Issuer"] = "ChangeFlow.Development",
                ["DevelopmentJwt:Audience"] = "ChangeFlow.Api"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ChangeFlowDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<ChangeFlowDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        // SQLite connections are released by the host during disposal; leave temporary test
        // databases for the OS temporary-file cleanup rather than risking a cleanup race.
    }
}
