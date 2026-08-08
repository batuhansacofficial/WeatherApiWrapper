using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherApiWrapper.Middleware;

namespace WeatherApiWrapper.Tests.Middleware
{
    public class ExceptionHandlingMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_RequestAborted_RethrowsCancellationWithoutWritingErrorResponse()
        {
            using var requestAbortSource = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            requestAbortSource.Cancel();

            var expectedException = new OperationCanceledException(requestAbortSource.Token);
            using var responseBody = new MemoryStream();
            var context = new DefaultHttpContext
            {
                RequestAborted = requestAbortSource.Token
            };
            context.Response.Body = responseBody;

            var middleware = new ExceptionHandlingMiddleware(
                _ => Task.FromException(expectedException),
                NullLogger<ExceptionHandlingMiddleware>.Instance);

            var actualException = await Assert.ThrowsAsync<OperationCanceledException>(
                () => middleware.InvokeAsync(context));

            Assert.Same(expectedException, actualException);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.False(context.Response.HasStarted);
            Assert.Null(context.Response.ContentType);
            Assert.Equal(0, responseBody.Length);
        }
    }
}
