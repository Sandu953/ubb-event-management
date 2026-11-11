using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace UbbEvents.Api.Infrastructure;

public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public static ApiException BadRequest(string message) => new(400, message);
    public static ApiException Unauthorized() => new(401, "Authentication is required or credentials are invalid.");
    public static ApiException Forbidden() => new(403, "You do not have permission for this operation.");
    public static ApiException NotFound(string entity) => new(404, $"{entity} was not found.");
    public static ApiException Conflict(string message) => new(409, message);
}

public static class RequestValidation
{
    public static void Ensure(object value)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), errors, true))
            throw ApiException.BadRequest(string.Join(" ", errors.Select(x => x.ErrorMessage)));
    }
    public static void Nonempty(Guid id, string name)
    {
        if (id == Guid.Empty) throw ApiException.BadRequest($"{name} must be a nonempty UUID.");
    }
}

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var pg = exception as PostgresException ?? exception.InnerException as PostgresException;
        var (status, detail) = exception switch
        {
            ApiException e => (e.StatusCode, e.Message),
            DbUpdateConcurrencyException => (409, "The resource changed. Reload it and retry."),
            _ when pg?.SqlState == PostgresErrorCodes.UniqueViolation => (409, "A record with these unique values already exists."),
            _ when pg?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.LockNotAvailable
                => (409, "A concurrent operation conflicted. Retry the request."),
            _ when pg?.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.CheckViolation
                => (409, "The operation conflicts with a data constraint."),
            _ => (500, "An unexpected server error occurred.")
        };
        // Database exceptions can contain sensitive parameter data; only log the type and trace ID.
        if (status == 500) logger.LogError("Request {TraceId} failed with {ExceptionType}.",
            context.TraceIdentifier, exception.GetType().Name);
        context.Response.StatusCode = status;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status, Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status),
                Detail = detail, Instance = context.Request.Path
            }
        });
        return true;
    }
}
