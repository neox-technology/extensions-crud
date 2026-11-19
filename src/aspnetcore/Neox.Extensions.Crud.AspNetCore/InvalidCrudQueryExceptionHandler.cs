using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.AspNetCore;

public sealed class InvalidCrudQueryExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not InvalidCrudQueryException crud)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Detail = crud.Message,
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid CRUD query",
        };

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
