using FlowHub.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FlowHub.Api.Configuration;

/// <summary>Złamana reguła domeny → HTTP 400 z komunikatem dla użytkownika.</summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not DomainException)
        {
            return false;
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Nie można wykonać operacji", Detail = exception.Message },
            ct);

        return true;
    }
}
