using WeatherApiWrapper.Models;
using WeatherApiWrapper.Services;

namespace WeatherApiWrapper.Tests.TestDoubles
{
    internal sealed class ThrowingWeatherService : IWeatherService
    {
        private readonly Exception _exception;

        public ThrowingWeatherService(Exception exception)
        {
            _exception = exception;
        }

        public Task<WeatherResponse> GetCurrentWeatherAsync(
            string city,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<WeatherResponse>(_exception);
        }

        public Task<ForecastResponse> GetForecastAsync(
            string city,
            int days,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<ForecastResponse>(_exception);
        }

        public Task<HistoryResponse> GetHistoricalWeatherAsync(
            string city,
            DateOnly date,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<HistoryResponse>(_exception);
        }
    }
}
