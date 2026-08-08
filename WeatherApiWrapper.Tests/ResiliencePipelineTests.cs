using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Polly.Timeout;
using WeatherApiWrapper.Extensions;
using WeatherApiWrapper.Options;
using WeatherApiWrapper.Services;

namespace WeatherApiWrapper.Tests
{
    public class ResiliencePipelineTests
    {
        [Fact]
        public async Task ProviderAttemptTimeout_ThrowsTimeoutRejectedException()
        {
            var handler = new CancellationAwareHandler();

            await using var serviceProvider = BuildServiceProvider(
                handler,
                attemptTimeout: TimeSpan.FromMilliseconds(50));

            var weatherService = serviceProvider.GetRequiredService<IWeatherService>();

            await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
                weatherService.GetCurrentWeatherAsync(
                    "Timeout City",
                    TestContext.Current.CancellationToken));

            Assert.Equal(2, handler.AttemptCount);
        }

        [Fact]
        public async Task CallerCancellation_ThrowsOperationCanceledException_NotTimeoutRejectedException()
        {
            var handler = new CancellationAwareHandler();

            await using var serviceProvider = BuildServiceProvider(
                handler,
                attemptTimeout: TimeSpan.FromSeconds(2));

            var weatherService = serviceProvider.GetRequiredService<IWeatherService>();
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);

            var requestTask = weatherService.GetCurrentWeatherAsync(
                "Cancelled City",
                cancellationSource.Token);

            await handler.WaitForAttemptAsync(TestContext.Current.CancellationToken);
            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                requestTask);

            Assert.Equal(1, handler.AttemptCount);
        }

        private static ServiceProvider BuildServiceProvider(
            HttpMessageHandler handler,
            TimeSpan attemptTimeout)
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddDistributedMemoryCache();
            services.Configure<WeatherApiOptions>(options =>
            {
                options.ApiKey = "test-api-key";
                options.BaseUrl = "https://fake-weather-provider/";
            });

            services
                .AddWeatherService(options =>
                {
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                    options.AttemptTimeout.Timeout = attemptTimeout;
                    options.Retry.MaxRetryAttempts = 1;
                    options.Retry.Delay = TimeSpan.Zero;
                    options.Retry.UseJitter = false;
                })
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            return services.BuildServiceProvider();
        }

        private sealed class CancellationAwareHandler : HttpMessageHandler
        {
            private int _attemptCount;
            private readonly TaskCompletionSource _attemptStarted = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public int AttemptCount => Volatile.Read(ref _attemptCount);

            public Task WaitForAttemptAsync(CancellationToken cancellationToken) =>
                _attemptStarted.Task.WaitAsync(cancellationToken);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _attemptCount);
                _attemptStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }
    }
}
