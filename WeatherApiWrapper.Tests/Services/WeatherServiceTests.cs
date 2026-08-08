using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using WeatherApiWrapper.Exceptions;
using WeatherApiWrapper.Models;
using WeatherApiWrapper.Options;
using WeatherApiWrapper.Services;
using WeatherApiWrapper.Tests.TestDoubles;

namespace WeatherApiWrapper.Tests.Services
{
    public class WeatherServiceTests
    {
        private const string CurrentProviderJson = """
        {
          "resolvedAddress": "Istanbul, Türkiye",
          "currentConditions": {
            "temp": 14.2,
            "humidity": 61.0,
            "windspeed": 18.4,
            "conditions": "Partially cloudy",
            "datetime": "18:50:00"
          }
        }
        """;

        private const string ForecastProviderJson = """
        {
          "resolvedAddress": "Istanbul, Türkiye",
          "days": [
            {
              "datetime": "2026-08-10",
              "tempmax": 31.5,
              "tempmin": 22.1,
              "humidity": 55.0,
              "windspeed": 16.4,
              "conditions": "Clear"
            },
            {
              "datetime": "2026-08-11",
              "tempmax": 29.2,
              "tempmin": 21.4,
              "humidity": 63.0,
              "windspeed": 19.1,
              "conditions": "Rain"
            },
            {
              "datetime": "2026-08-12",
              "tempmax": 28.7,
              "tempmin": 20.8,
              "humidity": 60.0,
              "windspeed": 14.3,
              "conditions": "Cloudy"
            }
          ]
        }
        """;

        private const string HistoryProviderJson = """
        {
          "resolvedAddress": "Istanbul, Türkiye",
          "days": [
            {
              "datetime": "2025-08-09",
              "tempmax": 30.8,
              "tempmin": 21.2,
              "temp": 25.6,
              "humidity": 58.0,
              "windspeed": 13.7,
              "conditions": "Clear"
            }
          ]
        }
        """;

        private static WeatherApiOptions CreateOptions() =>
            new()
            {
                BaseUrl = "https://fake-weather-provider/",
                ApiKey = "test-key"
            };

        private static Mock<IDistributedCache> CreateCacheMissMock()
        {
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);
            cacheMock
                .Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return cacheMock;
        }

        private static WeatherService CreateService(
            IDistributedCache cache,
            Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            new(
                new HttpClient(new FakeHttpMessageHandler(handler)),
                cache,
                Microsoft.Extensions.Options.Options.Create(CreateOptions()),
                Mock.Of<ILogger<WeatherService>>());

        private static byte[] SerializeToBytes<T>(T value) =>
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenCached_ReturnsFromCacheTrue()
        {
            var cached = new WeatherResponse
            {
                City = "Istanbul",
                ResolvedAddress = "Istanbul",
                TemperatureCelsius = 10.5m,
                Humidity = 50,
                WindSpeed = 12,
                Condition = "Cloudy",
                ProviderObservationTime = "12:00:00",
                RetrievedAtUtc = DateTime.UtcNow,
                FromCache = false
            };

            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(SerializeToBytes(cached));

            var service = CreateService(
                cacheMock.Object,
                _ => throw new InvalidOperationException("Provider should not be called on a cache hit."));

            var result = await service.GetCurrentWeatherAsync(
                "Istanbul",
                TestContext.Current.CancellationToken);

            Assert.True(result.FromCache);
            Assert.Equal("Istanbul", result.City);
            Assert.Equal(10.5m, result.TemperatureCelsius);
            cacheMock.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenCacheMiss_CallsProviderAndCaches()
        {
            var cacheMock = CreateCacheMissMock();
            var providerCallCount = 0;
            var service = CreateService(
                cacheMock.Object,
                _ =>
                {
                    providerCallCount++;
                    return FakeHttpMessageHandler.JsonResponse(CurrentProviderJson);
                });

            var result = await service.GetCurrentWeatherAsync(
                "Istanbul",
                TestContext.Current.CancellationToken);

            Assert.False(result.FromCache);
            Assert.Equal("Istanbul", result.City);
            Assert.Equal("Istanbul, Türkiye", result.ResolvedAddress);
            Assert.Equal(14.2m, result.TemperatureCelsius);
            Assert.Equal(1, providerCallCount);
            cacheMock.Verify(x => x.SetAsync(
                "weather:current:istanbul",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenProviderReturnsBadRequest_ThrowsArgumentException()
        {
            var cacheMock = CreateCacheMissMock();
            var service = CreateService(
                cacheMock.Object,
                _ => FakeHttpMessageHandler.JsonResponse(
                    "Invalid location parameter.",
                    HttpStatusCode.BadRequest));

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => service.GetCurrentWeatherAsync(
                    "asdasdasdxyz",
                    TestContext.Current.CancellationToken));

            Assert.Equal("Invalid city value: 'asdasdasdxyz'.", exception.Message);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        public async Task GetCurrentWeatherAsync_WhenProviderFailureIsNotLocationSpecific_ThrowsWeatherProviderException(
            HttpStatusCode statusCode)
        {
            var cacheMock = CreateCacheMissMock();
            var service = CreateService(
                cacheMock.Object,
                _ => FakeHttpMessageHandler.JsonResponse("provider failure", statusCode));

            var exception = await Assert.ThrowsAsync<WeatherProviderException>(
                () => service.GetCurrentWeatherAsync(
                    "Istanbul",
                    TestContext.Current.CancellationToken));

            Assert.Equal(
                $"External weather provider returned {(int)statusCode}.",
                exception.Message);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task GetCurrentWeatherAsync_WhenNonLocationFailureBodyContainsNotFound_ThrowsWeatherProviderException(
            HttpStatusCode statusCode)
        {
            var cacheMock = CreateCacheMissMock();
            var service = CreateService(
                cacheMock.Object,
                _ => FakeHttpMessageHandler.JsonResponse("API key not found.", statusCode));

            var exception = await Assert.ThrowsAsync<WeatherProviderException>(
                () => service.GetCurrentWeatherAsync(
                    "Istanbul",
                    TestContext.Current.CancellationToken));

            Assert.Equal(
                $"External weather provider returned {(int)statusCode}.",
                exception.Message);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenProviderReturnsNotFound_ThrowsKeyNotFoundException()
        {
            var cacheMock = CreateCacheMissMock();
            var service = CreateService(
                cacheMock.Object,
                _ => FakeHttpMessageHandler.JsonResponse("not found", HttpStatusCode.NotFound));

            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.GetCurrentWeatherAsync(
                    "UnknownCity",
                    TestContext.Current.CancellationToken));

            Assert.Equal("City 'UnknownCity' not found.", exception.Message);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenRedisReadFails_FallsBackToProvider()
        {
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis unavailable."));
            cacheMock
                .Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var providerCallCount = 0;
            var service = CreateService(
                cacheMock.Object,
                _ =>
                {
                    providerCallCount++;
                    return FakeHttpMessageHandler.JsonResponse(CurrentProviderJson);
                });

            var result = await service.GetCurrentWeatherAsync(
                "Istanbul",
                TestContext.Current.CancellationToken);

            Assert.False(result.FromCache);
            Assert.Equal(14.2m, result.TemperatureCelsius);
            Assert.Equal(1, providerCallCount);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenRedisWriteFails_ReturnsProviderResult()
        {
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);
            cacheMock
                .Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis unavailable."));

            var providerCallCount = 0;
            var service = CreateService(
                cacheMock.Object,
                _ =>
                {
                    providerCallCount++;
                    return FakeHttpMessageHandler.JsonResponse(CurrentProviderJson);
                });

            var result = await service.GetCurrentWeatherAsync(
                "Istanbul",
                TestContext.Current.CancellationToken);

            Assert.False(result.FromCache);
            Assert.Equal(14.2m, result.TemperatureCelsius);
            Assert.Equal(1, providerCallCount);
            cacheMock.Verify(x => x.SetAsync(
                "weather:current:istanbul",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

#pragma warning disable xUnit1051 // These tests deliberately use controlled cancellation tokens.
        [Fact]
        public async Task GetCurrentWeatherAsync_WhenRedisReadIsCanceled_PropagatesCancellationWithoutCallingProvider()
        {
            var canceledToken = new CancellationToken(canceled: true);
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), canceledToken))
                .ThrowsAsync(new OperationCanceledException(canceledToken));

            var providerCalled = false;
            var service = CreateService(
                cacheMock.Object,
                _ =>
                {
                    providerCalled = true;
                    return FakeHttpMessageHandler.JsonResponse(CurrentProviderJson);
                });

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.GetCurrentWeatherAsync("Istanbul", canceledToken));

            Assert.Equal(canceledToken, exception.CancellationToken);
            Assert.False(providerCalled);
            cacheMock.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenRedisWriteIsCanceled_PropagatesCancellation()
        {
            using var cancellationSource = new CancellationTokenSource();
            var callerToken = cancellationSource.Token;
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), callerToken))
                .ReturnsAsync((byte[]?)null);
            cacheMock
                .Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    callerToken))
                .Returns(() =>
                {
                    cancellationSource.Cancel();
                    return Task.FromCanceled(cancellationSource.Token);
                });

            var providerCallCount = 0;
            var service = CreateService(
                cacheMock.Object,
                _ =>
                {
                    providerCallCount++;
                    return FakeHttpMessageHandler.JsonResponse(CurrentProviderJson);
                });

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.GetCurrentWeatherAsync("Istanbul", callerToken));

            Assert.Equal(callerToken, exception.CancellationToken);
            Assert.Equal(1, providerCallCount);
        }
#pragma warning restore xUnit1051

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenProviderJsonIsMalformed_ThrowsWeatherProviderException()
        {
            var cacheMock = CreateCacheMissMock();
            var service = CreateService(
                cacheMock.Object,
                _ => FakeHttpMessageHandler.JsonResponse("{ malformed-json"));

            var exception = await Assert.ThrowsAsync<WeatherProviderException>(
                () => service.GetCurrentWeatherAsync(
                    "Istanbul",
                    TestContext.Current.CancellationToken));

            Assert.Equal("External weather provider returned an unusable response.", exception.Message);
            Assert.IsType<JsonException>(exception.InnerException);
            cacheMock.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WithReservedCharacters_EncodesCityInProviderUrl()
        {
            var cacheMock = CreateCacheMissMock();
            string? requestedUrl = null;
            var service = CreateService(
                cacheMock.Object,
                request =>
                {
                    requestedUrl = request.RequestUri?.OriginalString;
                    return FakeHttpMessageHandler.JsonResponse(CurrentProviderJson);
                });

            await service.GetCurrentWeatherAsync(
                "New York/Üsküdar",
                TestContext.Current.CancellationToken);

            Assert.Equal(
                "https://fake-weather-provider/New%20York%2F%C3%9Csk%C3%BCdar" +
                "?unitGroup=metric&include=current&key=test-key&contentType=json",
                requestedUrl);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WithMixedCaseCity_UsesNormalizedCacheKey()
        {
            var cached = new WeatherResponse
            {
                City = "London",
                ResolvedAddress = "London, UK",
                TemperatureCelsius = 20.1m,
                RetrievedAtUtc = DateTime.UtcNow
            };
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(
                    "weather:current:london",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(SerializeToBytes(cached));
            var service = CreateService(
                cacheMock.Object,
                _ => throw new InvalidOperationException("Provider should not be called on a cache hit."));

            var result = await service.GetCurrentWeatherAsync(
                "  LoNDoN  ",
                TestContext.Current.CancellationToken);

            Assert.True(result.FromCache);
            cacheMock.Verify(x => x.GetAsync(
                "weather:current:london",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetForecastAsync_WhenProviderSucceeds_MapsRequestedDays()
        {
            var cacheMock = CreateCacheMissMock();
            string? requestedUrl = null;
            var service = CreateService(
                cacheMock.Object,
                request =>
                {
                    requestedUrl = request.RequestUri?.OriginalString;
                    return FakeHttpMessageHandler.JsonResponse(ForecastProviderJson);
                });

            var result = await service.GetForecastAsync(
                "Istanbul",
                2,
                TestContext.Current.CancellationToken);

            Assert.False(result.FromCache);
            Assert.Equal("Istanbul", result.City);
            Assert.Equal("Istanbul, Türkiye", result.ResolvedAddress);
            Assert.Equal(2, result.DaysRequested);
            Assert.Equal(
                "https://fake-weather-provider/Istanbul/next2days" +
                "?unitGroup=metric&include=days&key=test-key&contentType=json",
                requestedUrl);
            Assert.Collection(
                result.Forecasts,
                first =>
                {
                    Assert.Equal("2026-08-10", first.Date);
                    Assert.Equal(31.5m, first.TemperatureMaxCelsius);
                    Assert.Equal(22.1m, first.TemperatureMinCelsius);
                    Assert.Equal(55m, first.Humidity);
                    Assert.Equal(16.4m, first.WindSpeed);
                    Assert.Equal("Clear", first.Condition);
                },
                second =>
                {
                    Assert.Equal("2026-08-11", second.Date);
                    Assert.Equal(29.2m, second.TemperatureMaxCelsius);
                    Assert.Equal(21.4m, second.TemperatureMinCelsius);
                    Assert.Equal("Rain", second.Condition);
                });
        }

        [Fact]
        public async Task GetForecastAsync_WhenCached_ReturnsCachedForecastWithoutCallingProvider()
        {
            var cached = new ForecastResponse
            {
                City = "London",
                ResolvedAddress = "London, UK",
                DaysRequested = 1,
                RetrievedAtUtc = DateTime.UtcNow,
                FromCache = false,
                Forecasts =
                [
                    new ForecastDayResponse
                    {
                        Date = "2026-08-10",
                        TemperatureMaxCelsius = 24.3m,
                        TemperatureMinCelsius = 15.2m,
                        Condition = "Cloudy"
                    }
                ]
            };
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(
                    "weather:forecast:london:1",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(SerializeToBytes(cached));
            var service = CreateService(
                cacheMock.Object,
                _ => throw new InvalidOperationException("Provider should not be called on a cache hit."));

            var result = await service.GetForecastAsync(
                "LoNDoN",
                1,
                TestContext.Current.CancellationToken);

            Assert.True(result.FromCache);
            var forecast = Assert.Single(result.Forecasts);
            Assert.Equal("2026-08-10", forecast.Date);
            Assert.Equal(24.3m, forecast.TemperatureMaxCelsius);
            cacheMock.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetForecastAsync_WhenCachedForecastListIsNull_FallsBackToProvider()
        {
            const string corruptCacheJson = """
            {
              "city": "Istanbul",
              "resolvedAddress": "Istanbul, Türkiye",
              "daysRequested": 2,
              "forecasts": null
            }
            """;
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(
                    "weather:forecast:istanbul:2",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(corruptCacheJson));
            cacheMock
                .Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var providerCallCount = 0;
            var service = CreateService(
                cacheMock.Object,
                _ =>
                {
                    providerCallCount++;
                    return FakeHttpMessageHandler.JsonResponse(ForecastProviderJson);
                });

            var result = await service.GetForecastAsync(
                "Istanbul",
                2,
                TestContext.Current.CancellationToken);

            Assert.False(result.FromCache);
            Assert.Equal(1, providerCallCount);
            Assert.Equal(2, result.Forecasts.Count);
            cacheMock.Verify(x => x.SetAsync(
                "weather:forecast:istanbul:2",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetForecastAsync_WhenProviderSucceeds_WritesNormalizedCacheEntryWithForecastExpiration()
        {
            var cacheMock = CreateCacheMissMock();
            string? writtenKey = null;
            byte[]? writtenValue = null;
            DistributedCacheEntryOptions? writtenOptions = null;
            cacheMock
                .Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                    (key, value, options, _) =>
                    {
                        writtenKey = key;
                        writtenValue = value;
                        writtenOptions = options;
                    })
                .Returns(Task.CompletedTask);
            var service = CreateService(
                cacheMock.Object,
                _ => FakeHttpMessageHandler.JsonResponse(ForecastProviderJson));

            await service.GetForecastAsync(
                "  IsTaNbUl  ",
                2,
                TestContext.Current.CancellationToken);

            Assert.Equal("weather:forecast:istanbul:2", writtenKey);
            Assert.NotNull(writtenValue);
            var writtenForecast = JsonSerializer.Deserialize<ForecastResponse>(writtenValue);
            Assert.NotNull(writtenForecast);
            Assert.False(writtenForecast.FromCache);
            Assert.Equal(2, writtenForecast.Forecasts.Count);
            Assert.Equal(TimeSpan.FromMinutes(30), writtenOptions?.AbsoluteExpirationRelativeToNow);
            Assert.Equal(TimeSpan.FromMinutes(10), writtenOptions?.SlidingExpiration);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(11)]
        public async Task GetForecastAsync_WithInvalidDays_ThrowsBeforeCacheOrProvider(int days)
        {
            var cacheMock = new Mock<IDistributedCache>();
            var providerCalled = false;
            var service = CreateService(
                cacheMock.Object,
                _ =>
                {
                    providerCalled = true;
                    return FakeHttpMessageHandler.JsonResponse(ForecastProviderJson);
                });

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => service.GetForecastAsync(
                    "Istanbul",
                    days,
                    TestContext.Current.CancellationToken));

            Assert.Equal("Days must be between 1 and 10.", exception.Message);
            Assert.False(providerCalled);
            cacheMock.Verify(x => x.GetAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("{}")]
        [InlineData("{\"resolvedAddress\":\"Istanbul\",\"days\":[]}")]
        public async Task GetForecastAsync_WhenProviderPayloadHasNoForecast_ThrowsWeatherProviderException(
            string providerJson)
        {
            var cacheMock = CreateCacheMissMock();
            var service = CreateService(
                cacheMock.Object,
                _ => FakeHttpMessageHandler.JsonResponse(providerJson));

            await Assert.ThrowsAsync<WeatherProviderException>(
                () => service.GetForecastAsync(
                    "Istanbul",
                    2,
                    TestContext.Current.CancellationToken));

            cacheMock.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetHistoricalWeatherAsync_WhenProviderSucceeds_MapsHistoricalDay()
        {
            var cacheMock = CreateCacheMissMock();
            string? requestedUrl = null;
            var service = CreateService(
                cacheMock.Object,
                request =>
                {
                    requestedUrl = request.RequestUri?.OriginalString;
                    return FakeHttpMessageHandler.JsonResponse(HistoryProviderJson);
                });
            var requestedDate = new DateOnly(2025, 8, 9);

            var result = await service.GetHistoricalWeatherAsync(
                "Istanbul",
                requestedDate,
                TestContext.Current.CancellationToken);

            Assert.False(result.FromCache);
            Assert.Equal("Istanbul", result.City);
            Assert.Equal("Istanbul, Türkiye", result.ResolvedAddress);
            Assert.Equal(requestedDate, result.RequestedDate);
            Assert.Equal(
                "https://fake-weather-provider/Istanbul/2025-08-09" +
                "?unitGroup=metric&include=days&key=test-key&contentType=json",
                requestedUrl);
            Assert.Equal("2025-08-09", result.History.Date);
            Assert.Equal(30.8m, result.History.TemperatureMaxCelsius);
            Assert.Equal(21.2m, result.History.TemperatureMinCelsius);
            Assert.Equal(25.6m, result.History.TemperatureAverageCelsius);
            Assert.Equal(58m, result.History.Humidity);
            Assert.Equal(13.7m, result.History.WindSpeed);
            Assert.Equal("Clear", result.History.Condition);
        }

        [Fact]
        public async Task GetHistoricalWeatherAsync_WhenCached_ReturnsCachedHistoryWithoutCallingProvider()
        {
            var requestedDate = new DateOnly(2025, 8, 9);
            var cached = new HistoryResponse
            {
                City = "London",
                ResolvedAddress = "London, UK",
                RequestedDate = requestedDate,
                RetrievedAtUtc = DateTime.UtcNow,
                FromCache = false,
                History = new HistoricalDayResponse
                {
                    Date = "2025-08-09",
                    TemperatureAverageCelsius = 19.4m,
                    Condition = "Rain"
                }
            };
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(
                    "weather:history:london:2025-08-09",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(SerializeToBytes(cached));
            var service = CreateService(
                cacheMock.Object,
                _ => throw new InvalidOperationException("Provider should not be called on a cache hit."));

            var result = await service.GetHistoricalWeatherAsync(
                "LoNDoN",
                requestedDate,
                TestContext.Current.CancellationToken);

            Assert.True(result.FromCache);
            Assert.Equal(19.4m, result.History.TemperatureAverageCelsius);
            Assert.Equal("Rain", result.History.Condition);
            cacheMock.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetHistoricalWeatherAsync_WhenCachedHistoryIsNull_FallsBackToProvider()
        {
            const string corruptCacheJson = """
            {
              "city": "Istanbul",
              "resolvedAddress": "Istanbul, Türkiye",
              "requestedDate": "2025-08-09",
              "history": null
            }
            """;
            var requestedDate = new DateOnly(2025, 8, 9);
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(
                    "weather:history:istanbul:2025-08-09",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(corruptCacheJson));
            cacheMock
                .Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var providerCallCount = 0;
            var service = CreateService(
                cacheMock.Object,
                _ =>
                {
                    providerCallCount++;
                    return FakeHttpMessageHandler.JsonResponse(HistoryProviderJson);
                });

            var result = await service.GetHistoricalWeatherAsync(
                "Istanbul",
                requestedDate,
                TestContext.Current.CancellationToken);

            Assert.False(result.FromCache);
            Assert.Equal(1, providerCallCount);
            Assert.Equal("2025-08-09", result.History.Date);
            cacheMock.Verify(x => x.SetAsync(
                "weather:history:istanbul:2025-08-09",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetHistoricalWeatherAsync_WhenProviderSucceeds_WritesNormalizedCacheEntryWithHistoryExpiration()
        {
            var cacheMock = CreateCacheMissMock();
            string? writtenKey = null;
            byte[]? writtenValue = null;
            DistributedCacheEntryOptions? writtenOptions = null;
            cacheMock
                .Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                    (key, value, options, _) =>
                    {
                        writtenKey = key;
                        writtenValue = value;
                        writtenOptions = options;
                    })
                .Returns(Task.CompletedTask);
            var service = CreateService(
                cacheMock.Object,
                _ => FakeHttpMessageHandler.JsonResponse(HistoryProviderJson));
            var requestedDate = new DateOnly(2025, 8, 9);

            await service.GetHistoricalWeatherAsync(
                "  IsTaNbUl  ",
                requestedDate,
                TestContext.Current.CancellationToken);

            Assert.Equal("weather:history:istanbul:2025-08-09", writtenKey);
            Assert.NotNull(writtenValue);
            var writtenHistory = JsonSerializer.Deserialize<HistoryResponse>(writtenValue);
            Assert.NotNull(writtenHistory);
            Assert.False(writtenHistory.FromCache);
            Assert.Equal(requestedDate, writtenHistory.RequestedDate);
            Assert.Equal("2025-08-09", writtenHistory.History.Date);
            Assert.Equal(TimeSpan.FromHours(12), writtenOptions?.AbsoluteExpirationRelativeToNow);
            Assert.Equal(TimeSpan.FromHours(1), writtenOptions?.SlidingExpiration);
        }

        [Fact]
        public async Task GetHistoricalWeatherAsync_WhenProviderHasNoDay_ThrowsWeatherProviderException()
        {
            const string providerJson = """
            {
              "resolvedAddress": "Istanbul, Türkiye",
              "days": []
            }
            """;
            var cacheMock = CreateCacheMissMock();
            var service = CreateService(
                cacheMock.Object,
                _ => FakeHttpMessageHandler.JsonResponse(providerJson));

            var exception = await Assert.ThrowsAsync<WeatherProviderException>(
                () => service.GetHistoricalWeatherAsync(
                    "Istanbul",
                    new DateOnly(2025, 8, 9),
                    TestContext.Current.CancellationToken));

            Assert.Equal(
                "External weather provider returned unusable historical weather data.",
                exception.Message);
            cacheMock.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
