using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayDesk.Api.Domain.Exceptions;

namespace PayDesk.Api.Middleware;

/// <summary>
/// Maps domain exceptions to RFC 7807 ProblemDetails responses so controllers can stay
/// focused on orchestration instead of repeating try/catch + status code boilerplate.
/// </summary>
public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, title) = ex switch
            {
                EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
                BusinessRuleException => (StatusCodes.Status409Conflict, "Business rule violation"),
                ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
                _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = statusCode == StatusCodes.Status500InternalServerError ? "An unexpected error occurred." : ex.Message,
                Instance = context.Request.Path
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
