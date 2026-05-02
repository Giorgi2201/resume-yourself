using Resume.Api.Exceptions;

namespace Resume.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            await WriteProblem(context, ex.StatusCode, "Request validation failed", ex.Message);
        }
        catch (FileParsingException ex)
        {
            logger.LogWarning(ex, "File parsing failed.");
            await WriteProblem(context, StatusCodes.Status422UnprocessableEntity, "File parsing failed", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception.");
            await WriteProblem(context, StatusCodes.Status500InternalServerError, "Unexpected server error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblem(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.com/{statusCode}",
            title,
            status = statusCode,
            detail,
            traceId = context.TraceIdentifier
        });
    }
}
