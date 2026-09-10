using System.Net;
using System.Text.Json;
namespace ElectroWorld.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger)
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
                _logger.LogError(
                    ex,
                    "Unhandled exception occurred while processing the request.");

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(
    HttpContext context,
    Exception exception)
        {
            context.Response.ContentType = "application/json";

            var statusCode = exception switch
            {
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                InvalidOperationException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Forbidden,
                _ => (int)HttpStatusCode.InternalServerError
            };

            context.Response.StatusCode = statusCode;

            var message = statusCode == (int)HttpStatusCode.InternalServerError
                ? "حدث خطأ داخلي في الخادم"
                : exception.Message;

            var response = new
            {
                statusCode,
                message
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response));
        }
    }
}



