namespace WeatherApiWrapper.Models
{
    public class HistoryResponse
    {
        public string City { get; set; } = string.Empty;
        public string ResolvedAddress { get; set; } = string.Empty;
        public DateOnly RequestedDate { get; set; }
        public DateTime RetrievedAtUtc { get; set; }
        public bool FromCache { get; set; }
        public HistoricalDayResponse History { get; set; } = new();
    }
}
