namespace WeatherApiWrapper.Models
{
    public class HistoricalDayResponse
    {
        public string Date { get; set; } = string.Empty;
        public decimal? TemperatureMaxCelsius { get; set; }
        public decimal? TemperatureMinCelsius { get; set; }
        public decimal? TemperatureAverageCelsius { get; set; }
        public decimal? Humidity { get; set; }
        public decimal? WindSpeed { get; set; }
        public string Condition { get; set; } = string.Empty;
    }
}
