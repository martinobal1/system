using IsSuas.Application.Policies;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Neautorizovaný"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Zakázané"),
            NotFoundException => (StatusCodes.Status404NotFound, "Nenájdené"),
            ConflictException => (StatusCodes.Status409Conflict, "Konflikt"),
            ValidationException => (StatusCodes.Status400BadRequest, "Neplatná požiadavka"),
            NotImplementedException => (StatusCodes.Status501NotImplemented, "Zatiaľ neimplementované"),
            _ => (StatusCodes.Status500InternalServerError, "Chyba servera")
        };

        if (status >= 500)
            logger.LogError(ex, "Unhandled exception");
        else
            logger.LogWarning(ex, "Handled domain exception");

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "Nastala neočakávaná chyba." : ex.Message
        });
    }
}
