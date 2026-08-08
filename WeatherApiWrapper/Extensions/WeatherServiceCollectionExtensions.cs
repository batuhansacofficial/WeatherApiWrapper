using Microsoft.Extensions.Http.Resilience;
using WeatherApiWrapper.Services;

namespace WeatherApiWrapper.Extensions
{
    public static class WeatherServiceCollectionExtensions
    {
        public static IHttpClientBuilder AddWeatherService(
            this IServiceCollection services,
            Action<HttpStandardResilienceOptions>? configureResilience = null)
        {
            var clientBuilder = services.AddHttpClient<IWeatherService, WeatherService>(client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

            clientBuilder.AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);

                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.UseJitter = true;

                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

                configureResilience?.Invoke(options);
            });

            return clientBuilder;
        }
    }
}
