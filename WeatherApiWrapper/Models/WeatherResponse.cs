namespace WeatherApiWrapper.Models
{
    public class WeatherResponse
    {
        public string City { get; set; } = string.Empty;
        public string ResolvedAddress { get; set; } = string.Empty;
        public decimal? TemperatureCelsius { get; set; }
        public decimal? Humidity { get; set; }
        public decimal? WindSpeed { get; set; }
        public string Condition { get; set; } = string.Empty;
        public string ProviderObservationTime { get; set; } = string.Empty;
        public DateTime RetrievedAtUtc { get; set; }
        public bool FromCache { get; set; }
    }
}
