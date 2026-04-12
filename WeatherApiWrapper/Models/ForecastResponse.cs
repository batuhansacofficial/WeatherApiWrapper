namespace WeatherApiWrapper.Models
{
    public class ForecastResponse
    {
        public string City { get; set; } = string.Empty;
        public string ResolvedAddress { get; set; } = string.Empty;
        public int DaysRequested { get; set; }
        public DateTime RetrievedAtUtc { get; set; }
        public bool FromCache { get; set; }
        public List<ForecastDayResponse> Forecasts { get; set; } = new();
    }
}
