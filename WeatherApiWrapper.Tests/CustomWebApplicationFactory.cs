using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherApiWrapper.Options;
using WeatherApiWrapper.Services;
using WeatherApiWrapper.Tests.TestDoubles;

namespace WeatherApiWrapper.Tests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private const string ProviderClientName = "IntegrationTestWeatherProvider";

        private const string SuccessfulProviderPayload = """
            {
              "resolvedAddress": "Istanbul",
              "currentConditions": {
                "temp": 20,
                "humidity": 50,
                "windspeed": 10,
                "conditions": "Clear",
                "datetime": "12:00:00"
              },
              "days": [
                {
                  "datetime": "2026-04-15",
                  "tempmax": 21,
                  "tempmin": 12,
                  "temp": 17,
                  "humidity": 55,
                  "windspeed": 11,
                  "conditions": "Clear"
                },
                {
                  "datetime": "2026-04-16",
                  "tempmax": 22,
                  "tempmin": 13,
                  "temp": 18,
                  "humidity": 54,
                  "windspeed": 12,
                  "conditions": "Partially cloudy"
                },
                {
                  "datetime": "2026-04-17",
                  "tempmax": 23,
                  "tempmin": 14,
                  "temp": 19,
                  "humidity": 53,
                  "windspeed": 13,
                  "conditions": "Cloudy"
                }
              ]
            }
            """;

        private readonly Func<HttpRequestMessage, HttpResponseMessage> _providerHandler;
        private readonly IWeatherService? _weatherService;
        private readonly Action<WeatherApiOptions>? _configureOptions;

        public CustomWebApplicationFactory()
            : this(CreateSuccessfulProviderResponse, weatherService: null, configureOptions: null)
        {
        }

        private CustomWebApplicationFactory(
            Func<HttpRequestMessage, HttpResponseMessage> providerHandler,
            IWeatherService? weatherService,
            Action<WeatherApiOptions>? configureOptions)
        {
            _providerHandler = providerHandler;
            _weatherService = weatherService;
            _configureOptions = configureOptions;
        }

        public static CustomWebApplicationFactory CreateWithProviderHandler(
            Func<HttpRequestMessage, HttpResponseMessage> providerHandler)
        {
            ArgumentNullException.ThrowIfNull(providerHandler);

            return new CustomWebApplicationFactory(
                providerHandler,
                weatherService: null,
                configureOptions: null);
        }

        public static CustomWebApplicationFactory CreateWithServiceException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return new CustomWebApplicationFactory(
                CreateSuccessfulProviderResponse,
                new ThrowingWeatherService(exception),
                configureOptions: null);
        }

        public static CustomWebApplicationFactory CreateWithOptions(
            Action<WeatherApiOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            return new CustomWebApplicationFactory(
                CreateSuccessfulProviderResponse,
                weatherService: null,
                configureOptions);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<WeatherApiOptions>(options =>
                {
                    options.BaseUrl = "https://fake-weather-provider/";
                    options.ApiKey = "test-key";
                    _configureOptions?.Invoke(options);
                });

                services.RemoveAll<IWeatherService>();
                services.RemoveAll<IDistributedCache>();
                services.AddDistributedMemoryCache();

                if (_weatherService is not null)
                {
                    services.AddSingleton(_weatherService);
                    return;
                }

                services.AddHttpClient(ProviderClientName)
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        new FakeHttpMessageHandler(_providerHandler));

                services.AddTransient<IWeatherService>(serviceProvider =>
                    new WeatherService(
                        serviceProvider.GetRequiredService<IHttpClientFactory>()
                            .CreateClient(ProviderClientName),
                        serviceProvider.GetRequiredService<IDistributedCache>(),
                        serviceProvider.GetRequiredService<IOptions<WeatherApiOptions>>(),
                        serviceProvider.GetRequiredService<ILogger<WeatherService>>()));
            });
        }

        private static HttpResponseMessage CreateSuccessfulProviderResponse(HttpRequestMessage _)
        {
            return FakeHttpMessageHandler.JsonResponse(SuccessfulProviderPayload);
        }
    }
}
