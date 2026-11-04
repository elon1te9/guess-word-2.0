using GuessWord.Api.Features.Games;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GuessWord.Api.Middleware;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var rule = exception as GameRuleException;
        var status = rule?.Status ?? 500;
        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = rule?.Message ?? "Не удалось выполнить запрос. Попробуйте ещё раз.",
            Extensions = { ["code"] = rule?.Code ?? "server_error" }
        };
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
