namespace WeatherBotAPI.Model
{
    public class WeatherResponse
    {
    
        public DateTime DateTimeIst { get; set; }
        public double Temperature { get; set; }
        public int Humidity { get; set; }
        public string Condition { get; set; }
    }
}
