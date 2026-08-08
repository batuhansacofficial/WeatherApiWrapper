using System.Net;
using Polly.CircuitBreaker;
using Polly.Timeout;
using WeatherApiWrapper.Exceptions;
using WeatherApiWrapper.Models;

namespace WeatherApiWrapper.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
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
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, message, logLevel) = MapException(exception);

            var response = new ApiErrorResponse
            {
                StatusCode = statusCode,
                Message = message,
                TraceId = context.TraceIdentifier,
                TimestampUtc = DateTime.UtcNow,
                Errors = null
            };

            _logger.Log(
                logLevel,
                exception,
                "Unhandled exception. TraceId: {TraceId}, StatusCode: {StatusCode}, Message: {Message}",
                context.TraceIdentifier,
                statusCode,
                message);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            await context.Response.WriteAsJsonAsync(response);
        }

        private static (int statusCode, string message, LogLevel logLevel) MapException(Exception exception)
        {
            return exception switch
            {
                ArgumentException ex => ((int)HttpStatusCode.BadRequest, ex.Message, LogLevel.Warning),
                KeyNotFoundException ex => ((int)HttpStatusCode.NotFound, ex.Message, LogLevel.Warning),
                TimeoutRejectedException _ => ((int)HttpStatusCode.GatewayTimeout, "The external weather provider timed out.", LogLevel.Error),
                BrokenCircuitException _ => ((int)HttpStatusCode.BadGateway, "The external weather provider is temporarily unavailable.", LogLevel.Error),
                WeatherProviderException _ => ((int)HttpStatusCode.BadGateway, "Failed to fetch weather data from external provider.", LogLevel.Error),
                HttpRequestException _ => ((int)HttpStatusCode.BadGateway, "Failed to fetch weather data from external provider.", LogLevel.Error),
                InvalidOperationException _ => ((int)HttpStatusCode.InternalServerError, "An unexpected server error occurred.", LogLevel.Error),
                _ => ((int)HttpStatusCode.InternalServerError, "An unexpected server error occurred.", LogLevel.Error)
            };
        }
    }
}
