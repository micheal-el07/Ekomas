using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Unhandled exception on {Path}", context.Request.Path);

        // SQL Server: 2627/2601 = unique violation, 547 = foreign key conflict
        if (exception is DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2627 or 2601 or 547 } })
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 409,
                Title = "The request conflicts with existing data."
            }, ct);
            return true;
        }

        if (exception is PaymentGatewayException)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 502,
                Title = "Payment provider error"
            }, ct);
        }

        if (exception is TaskCanceledException or TimeoutException)
        {
            // if (context.ResponseAborted.IsCancellationRequested)
            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 504,
                Title = "Payment provider error"
            }, ct);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 500,
            Title = "An unexpected error occurred."
        }, ct);

        return true;   // true = "I handled it, stop here"
    }
}