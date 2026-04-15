namespace WeatherApiWrapper.Models
{
    public class ApiErrorResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string TraceId { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
