using MarikinaMarket.API.Application;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MarikinaMarket.API.Presentation.Middleware
{
    public class ExceptionHandlingMiddleware : IMiddleware
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
            => _logger = logger;

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var statusCode = exception switch
            {
                InvalidCredentialsException => StatusCodes.Status400BadRequest,
                ValidationException => StatusCodes.Status400BadRequest,
                RecordNotFoundException => StatusCodes.Status404NotFound,
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                AccountLockedException => StatusCodes.Status423Locked,
                SessionExpiredException => StatusCodes.Status401Unauthorized,
                DuplicateOrdinanceException => StatusCodes.Status409Conflict,
                AlreadyProcessedException => StatusCodes.Status409Conflict,
                ConcurrencyConflictException => StatusCodes.Status409Conflict,
                ResourceCreationFailedException => StatusCodes.Status422UnprocessableEntity,
                InvalidRequestException => StatusCodes.Status400BadRequest,
                DuplicateWarningException => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Unhandled server exception occurred.");
            }
            else
            {
                _logger.LogWarning("Handled domain exception: {ExceptionType} — {Message}", exception.GetType().Name, exception.Message);
            }

            context.Response.StatusCode = statusCode;

            object response = exception switch
            {
                DuplicateOrdinanceException dupEx => new
                {
                    title = "Duplicate Ordinances Today",
                    message = dupEx.Message,
                    duplicateOrdinances = dupEx.DuplicateOrdinances
                },
                DuplicateWarningException dupWar => new
                {
                    title = "Duplicate Warning",
                    message = dupWar.Message,  
                },
                _ => new
                {
                    message = context.Response.StatusCode == StatusCodes.Status500InternalServerError
                        ? "An unexpected server error occurred."
                        : exception.Message
                }
            };

            return context.Response.WriteAsJsonAsync(response);
        }
    }
}
