using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using WeatherApiWrapper.Models;
using WeatherApiWrapper.Options;

namespace WeatherApiWrapper.Services
{
    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly IDistributedCache _cache;
        private readonly WeatherApiOptions _options;
        private readonly ILogger<WeatherService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public WeatherService(
            HttpClient httpClient,
            IDistributedCache cache,
            IOptions<WeatherApiOptions> options,
            ILogger<WeatherService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<WeatherResponse> GetCurrentWeatherAsync(
            string city,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(city))
                throw new ArgumentException("City is required.");

            city = city.Trim();

            var cacheKey = $"weather:current:{city.ToLowerInvariant()}";

            var cachedResponse = await GetCachedAsync<WeatherResponse>(cacheKey, cancellationToken);

            if (cachedResponse is not null)
            {
                _logger.LogInformation("Cache HIT for current weather. City: {City}", city);

                return new WeatherResponse
                {
                    City = cachedResponse.City,
                    ResolvedAddress = cachedResponse.ResolvedAddress,
                    TemperatureCelsius = cachedResponse.TemperatureCelsius,
                    Humidity = cachedResponse.Humidity,
                    WindSpeed = cachedResponse.WindSpeed,
                    Condition = cachedResponse.Condition,
                    ProviderObservationTime = cachedResponse.ProviderObservationTime,
                    RetrievedAtUtc = cachedResponse.RetrievedAtUtc,
                    FromCache = true
                };
            }

            _logger.LogInformation("Cache MISS for current weather. City: {City}", city);

            EnsureConfiguration();

            var encodedCity = Uri.EscapeDataString(city);
            var requestUrl =
                $"{_options.BaseUrl}{encodedCity}?unitGroup=metric&include=current&key={_options.ApiKey}&contentType=json";

            _logger.LogInformation("Fetching current weather from provider. City: {City}", city);

            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            await EnsureProviderSuccessAsync(response, city, cancellationToken);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var providerResponse = await JsonSerializer.DeserializeAsync<VisualCrossingResponse>(
                responseStream,
                JsonOptions,
                cancellationToken);

            if (providerResponse?.CurrentConditions is null)
                throw new InvalidOperationException("Weather data could not be parsed.");

            var result = new WeatherResponse
            {
                City = city,
                ResolvedAddress = providerResponse.ResolvedAddress ?? city,
                TemperatureCelsius = providerResponse.CurrentConditions.Temp,
                Humidity = providerResponse.CurrentConditions.Humidity,
                WindSpeed = providerResponse.CurrentConditions.WindSpeed,
                Condition = providerResponse.CurrentConditions.Conditions ?? "Unknown",
                ProviderObservationTime = providerResponse.CurrentConditions.DateTime ?? string.Empty,
                RetrievedAtUtc = DateTime.UtcNow,
                FromCache = false
            };

            await SetCachedAsync(
                cacheKey,
                result,
                TimeSpan.FromMinutes(10),
                TimeSpan.FromMinutes(5),
                cancellationToken);

            return result;
        }

        public async Task<ForecastResponse> GetForecastAsync(
            string city,
            int days,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(city))
                throw new ArgumentException("City is required.");

            if (days < 1 || days > 10)
                throw new ArgumentException("Days must be between 1 and 10.");

            city = city.Trim();

            var cacheKey = $"weather:forecast:{city.ToLowerInvariant()}:{days}";

            var cachedForecast = await GetCachedAsync<ForecastResponse>(cacheKey, cancellationToken);

            if (cachedForecast is not null)
            {
                _logger.LogInformation("Cache HIT for forecast. City: {City}, Days: {Days}", city, days);

                return new ForecastResponse
                {
                    City = cachedForecast.City,
                    ResolvedAddress = cachedForecast.ResolvedAddress,
                    DaysRequested = cachedForecast.DaysRequested,
                    RetrievedAtUtc = cachedForecast.RetrievedAtUtc,
                    FromCache = true,
                    Forecasts = cachedForecast.Forecasts
                        .Select(x => new ForecastDayResponse
                        {
                            Date = x.Date,
                            TemperatureMaxCelsius = x.TemperatureMaxCelsius,
                            TemperatureMinCelsius = x.TemperatureMinCelsius,
                            Humidity = x.Humidity,
                            WindSpeed = x.WindSpeed,
                            Condition = x.Condition
                        })
                        .ToList()
                };
            }

            _logger.LogInformation("Cache MISS for forecast. City: {City}, Days: {Days}", city, days);

            EnsureConfiguration();

            var encodedCity = Uri.EscapeDataString(city);
            var requestUrl =
                $"{_options.BaseUrl}{encodedCity}/next{days}days?unitGroup=metric&include=days&key={_options.ApiKey}&contentType=json";

            _logger.LogInformation("Fetching forecast from provider. City: {City}, Days: {Days}", city, days);

            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            await EnsureProviderSuccessAsync(response, city, cancellationToken);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var providerResponse = await JsonSerializer.DeserializeAsync<VisualCrossingResponse>(
                responseStream,
                JsonOptions,
                cancellationToken);

            if (providerResponse?.Days is null || providerResponse.Days.Count == 0)
                throw new InvalidOperationException("Forecast data could not be parsed.");

            var result = new ForecastResponse
            {
                City = city,
                ResolvedAddress = providerResponse.ResolvedAddress ?? city,
                DaysRequested = days,
                RetrievedAtUtc = DateTime.UtcNow,
                FromCache = false,
                Forecasts = providerResponse.Days
                    .Take(days)
                    .Select(day => new ForecastDayResponse
                    {
                        Date = day.Date ?? string.Empty,
                        TemperatureMaxCelsius = day.TempMax,
                        TemperatureMinCelsius = day.TempMin,
                        Humidity = day.Humidity,
                        WindSpeed = day.WindSpeed,
                        Condition = day.Conditions ?? "Unknown"
                    })
                    .ToList()
            };

            await SetCachedAsync(
                cacheKey,
                result,
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(10),
                cancellationToken);

            return result;
        }

        public async Task<HistoryResponse> GetHistoricalWeatherAsync(
            string city,
            DateOnly date,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(city))
                throw new ArgumentException("City is required.");

            city = city.Trim();

            var cacheKey = $"weather:history:{city.ToLowerInvariant()}:{date:yyyy-MM-dd}";

            var cachedHistory = await GetCachedAsync<HistoryResponse>(cacheKey, cancellationToken);

            if (cachedHistory is not null)
            {
                _logger.LogInformation("Cache HIT for history. City: {City}, Date: {Date}", city, date);

                return new HistoryResponse
                {
                    City = cachedHistory.City,
                    ResolvedAddress = cachedHistory.ResolvedAddress,
                    RequestedDate = cachedHistory.RequestedDate,
                    RetrievedAtUtc = cachedHistory.RetrievedAtUtc,
                    FromCache = true,
                    History = new HistoricalDayResponse
                    {
                        Date = cachedHistory.History.Date,
                        TemperatureMaxCelsius = cachedHistory.History.TemperatureMaxCelsius,
                        TemperatureMinCelsius = cachedHistory.History.TemperatureMinCelsius,
                        TemperatureAverageCelsius = cachedHistory.History.TemperatureAverageCelsius,
                        Humidity = cachedHistory.History.Humidity,
                        WindSpeed = cachedHistory.History.WindSpeed,
                        Condition = cachedHistory.History.Condition
                    }
                };
            }

            _logger.LogInformation("Cache MISS for history. City: {City}, Date: {Date}", city, date);

            EnsureConfiguration();

            var encodedCity = Uri.EscapeDataString(city);
            var formattedDate = date.ToString("yyyy-MM-dd");
            var requestUrl =
                $"{_options.BaseUrl}{encodedCity}/{formattedDate}?unitGroup=metric&include=days&key={_options.ApiKey}&contentType=json";

            _logger.LogInformation("Fetching historical weather from provider. City: {City}, Date: {Date}", city, date);

            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            await EnsureProviderSuccessAsync(response, city, cancellationToken);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var providerResponse = await JsonSerializer.DeserializeAsync<VisualCrossingResponse>(
                responseStream,
                JsonOptions,
                cancellationToken);

            var historicalDay = providerResponse?.Days?.FirstOrDefault();

            if (historicalDay is null)
                throw new InvalidOperationException("Historical weather data could not be parsed.");

            var result = new HistoryResponse
            {
                City = city,
                ResolvedAddress = providerResponse?.ResolvedAddress ?? city,
                RequestedDate = date,
                RetrievedAtUtc = DateTime.UtcNow,
                FromCache = false,
                History = new HistoricalDayResponse
                {
                    Date = historicalDay.Date ?? formattedDate,
                    TemperatureMaxCelsius = historicalDay.TempMax,
                    TemperatureMinCelsius = historicalDay.TempMin,
                    TemperatureAverageCelsius = historicalDay.Temp,
                    Humidity = historicalDay.Humidity,
                    WindSpeed = historicalDay.WindSpeed,
                    Condition = historicalDay.Conditions ?? "Unknown"
                }
            };

            await SetCachedAsync(
                cacheKey,
                result,
                TimeSpan.FromHours(12),
                TimeSpan.FromHours(1),
                cancellationToken);

            return result;
        }

        private void EnsureConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                throw new InvalidOperationException("Weather API key is not configured.");

            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
                throw new InvalidOperationException("Weather API base URL is not configured.");
        }

        private async Task EnsureProviderSuccessAsync(
            HttpResponseMessage response,
            string city,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Provider request failed. City: {City}, StatusCode: {StatusCode}, Body: {Body}",
                city,
                response.StatusCode,
                errorContent);

            if (IsLocationNotFound(response.StatusCode, errorContent))
                throw new KeyNotFoundException($"City '{city}' not found.");

            if (response.StatusCode == HttpStatusCode.BadRequest)
                throw new ArgumentException($"Invalid city value: '{city}'.");

            throw new HttpRequestException(
                $"External weather provider returned {(int)response.StatusCode}.");
        }

        private static bool IsLocationNotFound(HttpStatusCode statusCode, string? responseBody)
        {
            if (statusCode == HttpStatusCode.NotFound)
                return true;

            if (string.IsNullOrWhiteSpace(responseBody))
                return false;

            var body = responseBody.ToLowerInvariant();

            return
                body.Contains("not found") ||
                body.Contains("unknown location") ||
                body.Contains("invalid location") ||
                body.Contains("address not found") ||
                body.Contains("cannot find");
        }

        private async Task<T?> GetCachedAsync<T>(string key, CancellationToken cancellationToken)
        {
            try
            {
                var cachedJson = await _cache.GetStringAsync(key, cancellationToken);

                if (string.IsNullOrWhiteSpace(cachedJson))
                    return default;

                return JsonSerializer.Deserialize<T>(cachedJson, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis read failed for key: {Key}", key);
                return default;
            }
        }

        private async Task SetCachedAsync<T>(
            string key,
            T value,
            TimeSpan absoluteExpiration,
            TimeSpan slidingExpiration,
            CancellationToken cancellationToken)
        {
            try
            {
                var serialized = JsonSerializer.Serialize(value, JsonOptions);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = absoluteExpiration,
                    SlidingExpiration = slidingExpiration
                };

                await _cache.SetStringAsync(key, serialized, options, cancellationToken);
                _logger.LogInformation("Cache WRITE for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis write failed for key: {Key}", key);
            }
        }
    }
}
