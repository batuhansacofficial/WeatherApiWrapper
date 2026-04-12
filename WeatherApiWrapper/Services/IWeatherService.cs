using WeatherApiWrapper.Models;

namespace WeatherApiWrapper.Services
{
    public interface IWeatherService
    {
        Task<WeatherResponse> GetCurrentWeatherAsync(string city, CancellationToken cancellationToken = default);

        Task<ForecastResponse> GetForecastAsync(string city, int days, CancellationToken cancellationToken = default);

        Task<HistoryResponse> GetHistoricalWeatherAsync(
            string city,
            DateOnly date,
            CancellationToken cancellationToken = default);
    }
}
