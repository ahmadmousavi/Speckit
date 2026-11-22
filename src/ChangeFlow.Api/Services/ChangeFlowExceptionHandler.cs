using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace ChangeFlow.Api.Services;

public sealed class ChangeFlowExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ChangeFlowProblemException ex => ex,
            _ => new ChangeFlowProblemException(500, "InternalError", "Internal server error",
                "An unexpected error occurred.")
        };

        context.Response.StatusCode = problem.StatusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = $"https://changeflow.local/problems/{problem.Code.ToLowerInvariant()}",
            title = problem.Title,
            status = problem.StatusCode,
            code = problem.Code,
            detail = problem.Message,
            instance = context.Request.Path.Value
        }), cancellationToken);

        return true;
    }
}
