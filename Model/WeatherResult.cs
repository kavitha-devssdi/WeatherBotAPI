namespace WeatherBotAPI.Model
{
    public class WeatherResult
    {
        public double Temperature { get; set; }
        public int Humidity { get; set; }
        public string? Condition { get; set; }
    }
}
