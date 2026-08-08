using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using WeatherApiWrapper.Models;

namespace WeatherApiWrapper.Tests
{
    public class WeatherApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private const string GenericProviderErrorMessage =
            "Failed to fetch weather data from external provider.";

        private readonly HttpClient _client;

        public WeatherApiIntegrationTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetCurrentWeather_ReturnsMappedContract()
        {
            const string city = "Istanbul-current-contract";
            var cancellationToken = TestContext.Current.CancellationToken;

            using var response = await _client.GetAsync(
                $"/api/weather/current?city={city}",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<WeatherResponse>(cancellationToken);

            Assert.NotNull(result);
            Assert.Equal(city, result.City);
            Assert.Equal("Istanbul", result.ResolvedAddress);
            Assert.Equal(20m, result.TemperatureCelsius);
            Assert.Equal(50m, result.Humidity);
            Assert.Equal(10m, result.WindSpeed);
            Assert.Equal("Clear", result.Condition);
            Assert.Equal("12:00:00", result.ProviderObservationTime);
            Assert.NotEqual(default, result.RetrievedAtUtc);
            Assert.False(result.FromCache);
        }

        [Fact]
        public async Task GetCurrentWeather_SecondCall_ReturnsFromCache()
        {
            const string requestUri =
                "/api/weather/current?city=Istanbul-cache-contract";
            var cancellationToken = TestContext.Current.CancellationToken;

            var first = await _client.GetFromJsonAsync<WeatherResponse>(
                requestUri,
                cancellationToken);

            var second = await _client.GetFromJsonAsync<WeatherResponse>(
                requestUri,
                cancellationToken);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.False(first.FromCache);
            Assert.True(second.FromCache);
            Assert.Equal(first.RetrievedAtUtc, second.RetrievedAtUtc);
            Assert.Equal(first.TemperatureCelsius, second.TemperatureCelsius);
        }

        [Fact]
        public async Task GetForecast_ReturnsMappedContract()
        {
            const string city = "Istanbul-forecast-contract";
            var cancellationToken = TestContext.Current.CancellationToken;

            using var response = await _client.GetAsync(
                $"/api/weather/forecast?city={city}&days=3",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ForecastResponse>(cancellationToken);

            Assert.NotNull(result);
            Assert.Equal(city, result.City);
            Assert.Equal("Istanbul", result.ResolvedAddress);
            Assert.Equal(3, result.DaysRequested);
            Assert.NotEqual(default, result.RetrievedAtUtc);
            Assert.False(result.FromCache);
            Assert.Collection(
                result.Forecasts,
                day =>
                {
                    Assert.Equal("2026-04-15", day.Date);
                    Assert.Equal(21m, day.TemperatureMaxCelsius);
                    Assert.Equal(12m, day.TemperatureMinCelsius);
                    Assert.Equal(55m, day.Humidity);
                    Assert.Equal(11m, day.WindSpeed);
                    Assert.Equal("Clear", day.Condition);
                },
                day => Assert.Equal("2026-04-16", day.Date),
                day => Assert.Equal("2026-04-17", day.Date));
        }

        [Fact]
        public async Task GetHistory_ReturnsMappedContract()
        {
            const string city = "Istanbul-history-contract";
            var cancellationToken = TestContext.Current.CancellationToken;

            using var response = await _client.GetAsync(
                $"/api/weather/history?city={city}&date=2026-04-15",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<HistoryResponse>(cancellationToken);

            Assert.NotNull(result);
            Assert.Equal(city, result.City);
            Assert.Equal("Istanbul", result.ResolvedAddress);
            Assert.Equal(new DateOnly(2026, 4, 15), result.RequestedDate);
            Assert.NotEqual(default, result.RetrievedAtUtc);
            Assert.False(result.FromCache);
            Assert.Equal("2026-04-15", result.History.Date);
            Assert.Equal(21m, result.History.TemperatureMaxCelsius);
            Assert.Equal(12m, result.History.TemperatureMinCelsius);
            Assert.Equal(17m, result.History.TemperatureAverageCelsius);
            Assert.Equal(55m, result.History.Humidity);
            Assert.Equal(11m, result.History.WindSpeed);
            Assert.Equal("Clear", result.History.Condition);
        }

        [Fact]
        public async Task GetCurrentWeather_MissingCity_ReturnsStandardizedBadRequest()
        {
            using var response = await _client.GetAsync(
                "/api/weather/current",
                TestContext.Current.CancellationToken);

            await AssertErrorAsync(
                response,
                HttpStatusCode.BadRequest,
                "Query parameter 'city' is required.",
                expectModelStateErrors: false);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(11)]
        public async Task GetForecast_DaysOutsideRange_ReturnsStandardizedBadRequest(int days)
        {
            using var response = await _client.GetAsync(
                $"/api/weather/forecast?city=Istanbul&days={days}",
                TestContext.Current.CancellationToken);

            await AssertErrorAsync(
                response,
                HttpStatusCode.BadRequest,
                "Query parameter 'days' must be between 1 and 10.",
                expectModelStateErrors: false);
        }

        [Fact]
        public async Task GetHistory_MissingDate_ReturnsStandardizedBadRequest()
        {
            using var response = await _client.GetAsync(
                "/api/weather/history?city=Istanbul",
                TestContext.Current.CancellationToken);

            await AssertErrorAsync(
                response,
                HttpStatusCode.BadRequest,
                "Query parameter 'date' is required and must be in yyyy-MM-dd format.",
                expectModelStateErrors: false);
        }

        [Fact]
        public async Task GetHistory_MalformedDate_ReturnsStandardizedModelStateError()
        {
            using var response = await _client.GetAsync(
                "/api/weather/history?city=Istanbul&date=not-a-date",
                TestContext.Current.CancellationToken);

            var error = await AssertErrorAsync(
                response,
                HttpStatusCode.BadRequest,
                "Validation failed.",
                expectModelStateErrors: true);

            Assert.NotNull(error.Errors);
            Assert.Contains("date", error.Errors.Keys, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetHistory_FutureDate_ReturnsStandardizedBadRequest()
        {
            using var response = await _client.GetAsync(
                "/api/weather/history?city=Istanbul&date=2999-01-01",
                TestContext.Current.CancellationToken);

            await AssertErrorAsync(
                response,
                HttpStatusCode.BadRequest,
                "Date cannot be in the future.",
                expectModelStateErrors: false);
        }

        [Fact]
        public async Task GetCurrentWeather_ProviderNotFound_ReturnsNotFoundContract()
        {
            using var factory = CustomWebApplicationFactory.CreateWithProviderHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("location not found")
                });
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                "/api/weather/current?city=Missing-provider-city",
                TestContext.Current.CancellationToken);

            await AssertErrorAsync(
                response,
                HttpStatusCode.NotFound,
                "City 'Missing-provider-city' not found.",
                expectModelStateErrors: false);
        }

        [Fact]
        public async Task GetCurrentWeather_ProviderServerError_ReturnsBadGatewayContract()
        {
            using var factory = CustomWebApplicationFactory.CreateWithProviderHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("provider internal failure")
                });
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                "/api/weather/current?city=Provider-error-city",
                TestContext.Current.CancellationToken);

            await AssertErrorAsync(
                response,
                HttpStatusCode.BadGateway,
                GenericProviderErrorMessage,
                expectModelStateErrors: false);
        }

        [Fact]
        public async Task GetCurrentWeather_MalformedProviderJson_ReturnsBadGatewayContract()
        {
            using var factory = CustomWebApplicationFactory.CreateWithProviderHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{not-valid-json",
                        Encoding.UTF8,
                        "application/json")
                });
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                "/api/weather/current?city=Malformed-provider-json-city",
                TestContext.Current.CancellationToken);

            await AssertErrorAsync(
                response,
                HttpStatusCode.BadGateway,
                GenericProviderErrorMessage,
                expectModelStateErrors: false);
        }

        [Fact]
        public async Task GetCurrentWeather_InternalInvalidOperation_ReturnsGenericServerError()
        {
            const string internalMessage = "sensitive-internal-diagnostic-marker";
            using var factory = CustomWebApplicationFactory.CreateWithServiceException(
                new InvalidOperationException(internalMessage));
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                "/api/weather/current?city=Internal-error-city",
                TestContext.Current.CancellationToken);

            var error = await AssertErrorAsync(
                response,
                HttpStatusCode.InternalServerError,
                "An unexpected server error occurred.",
                expectModelStateErrors: false);

            Assert.DoesNotContain(internalMessage, error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("", "https://fake-weather-provider/", "WeatherApi:ApiKey is required.")]
        [InlineData("   ", "https://fake-weather-provider/", "WeatherApi:ApiKey is required.")]
        [InlineData("test-key", "relative-provider-url", "WeatherApi:BaseUrl must be an absolute HTTP(S) URL ending with '/'.")]
        [InlineData("test-key", "ftp://fake-weather-provider/", "WeatherApi:BaseUrl must be an absolute HTTP(S) URL ending with '/'.")]
        [InlineData("test-key", "https://fake-weather-provider", "WeatherApi:BaseUrl must be an absolute HTTP(S) URL ending with '/'.")]
        public void Startup_InvalidWeatherOptions_FailsValidation(
            string apiKey,
            string baseUrl,
            string expectedFailure)
        {
            using var factory = CustomWebApplicationFactory.CreateWithOptions(options =>
            {
                options.ApiKey = apiKey;
                options.BaseUrl = baseUrl;
            });

            var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

            Assert.Contains(expectedFailure, exception.Failures);
        }

        [Fact]
        public async Task GetCurrentWeather_ProviderTimeout_ReturnsGatewayTimeoutContract()
        {
            using var factory = CustomWebApplicationFactory.CreateWithServiceException(
                new TimeoutRejectedException("provider timed out"));
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                "/api/weather/current?city=Timeout-city",
                TestContext.Current.CancellationToken);

            await AssertErrorAsync(
                response,
                HttpStatusCode.GatewayTimeout,
                "The external weather provider timed out.",
                expectModelStateErrors: false);
        }

        [Fact]
        public async Task GetCurrentWeather_OpenCircuit_ReturnsBadGatewayContract()
        {
            using var factory = CustomWebApplicationFactory.CreateWithServiceException(
                new BrokenCircuitException("provider circuit is open"));
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                "/api/weather/current?city=Open-circuit-city",
                TestContext.Current.CancellationToken);

            await AssertErrorAsync(
                response,
                HttpStatusCode.BadGateway,
                "The external weather provider is temporarily unavailable.",
                expectModelStateErrors: false);
        }

        private static async Task<ApiErrorResponse> AssertErrorAsync(
            HttpResponseMessage response,
            HttpStatusCode expectedStatusCode,
            string expectedMessage,
            bool expectModelStateErrors)
        {
            Assert.Equal(expectedStatusCode, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(
                TestContext.Current.CancellationToken);

            Assert.NotNull(error);
            Assert.Equal((int)expectedStatusCode, error.StatusCode);
            Assert.Equal(expectedMessage, error.Message);
            Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
            Assert.NotEqual(default, error.TimestampUtc);

            if (expectModelStateErrors)
            {
                Assert.NotNull(error.Errors);
                Assert.NotEmpty(error.Errors);
            }
            else
            {
                Assert.Null(error.Errors);
            }

            return error;
        }
    }
}
