using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WeatherBotAPI.Helpers;
using WeatherBotAPI.Model;

namespace WeatherBotAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public WeatherController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }


        [HttpGet]
        [ProducesResponseType(typeof(WeatherResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ForecastResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetWeather(
    [FromQuery] string city,
    [FromQuery] string? mode,
    [FromQuery] DateTime? dateTime)
        {
            if (string.IsNullOrWhiteSpace(city))
                return BadRequest("City is required.");

            // ================= DATE TIME REQUEST =================
            if (dateTime.HasValue)
            {
                var requestedUtc = TimeZoneHelper.ConvertIstToUtc(dateTime.Value);
                var nowUtc = DateTime.UtcNow;

                if (requestedUtc < nowUtc)
                    return BadRequest("Past weather data is not supported in free version.");

                if ((requestedUtc - nowUtc).TotalHours <= 2)
                {
                    var current = await GetWeatherFromAPI(city);
                    if (current == null)
                        return NotFound("Unable to fetch weather data.");

                    return Ok(new WeatherResponse
                    {
                        City = city,
                        DateTimeIst = TimeZoneHelper.ConvertUtcToIst(nowUtc),
                        Temperature = current.Temperature,
                        Humidity = current.Humidity,
                        Condition = current.Condition
                    });
                }

                if (requestedUtc <= nowUtc.AddDays(5))
                {
                    var forecastByDate = await GetForecastByDate(city, requestedUtc);
                    if (forecastByDate == null)
                        return NotFound("Forecast not available for requested time.");

                    return Ok(forecastByDate);
                }

                return BadRequest("Forecast available only for next 5 days.");
            }

            // ================= GENERAL FORECAST =================
            if (mode?.ToLower() == "forecast")
            {
                var forecast = await GetForecastFromAPI(city);
                if (forecast == null)
                    return NotFound("Unable to fetch forecast data.");

                return Ok(forecast);
            }

            // ================= CURRENT WEATHER =================
            var weatherData = await GetWeatherFromAPI(city);
            if (weatherData == null)
                return NotFound("Unable to fetch weather data.");

            return Ok(new WeatherResponse
            {
                City = city,
                DateTimeIst = TimeZoneHelper.ConvertUtcToIst(DateTime.UtcNow),
                Temperature = weatherData.Temperature,
                Humidity = weatherData.Humidity,
                Condition = weatherData.Condition
            });
        }

        // ================= CURRENT WEATHER =================

        private async Task<WeatherResult?> GetWeatherFromAPI(string city)
        {
            var client = _httpClientFactory.CreateClient();

            string apiKey = _configuration["OpenWeather:ApiKey"];

            string url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;

            return new WeatherResult
            {
                Temperature = root.GetProperty("main").GetProperty("temp").GetDouble(),
                Humidity = root.GetProperty("main").GetProperty("humidity").GetInt32(),
                Condition = root.GetProperty("weather")[0].GetProperty("description").GetString()
            };
        }

        // ================= GENERAL FORECAST =================

        private async Task<ForecastResponse?> GetForecastFromAPI(string city)
        {
            var client = _httpClientFactory.CreateClient();

            string apiKey = _configuration["OpenWeather:ApiKey"];

            string url = $"https://api.openweathermap.org/data/2.5/forecast?q={city}&appid={apiKey}&units=metric";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var list = doc.RootElement.GetProperty("list");

            var forecasts = new List<WeatherResponse>();

            for (int i = 0; i < 3; i++)
            {
                var item = list[i];

                var utcTime = DateTime.SpecifyKind(
                    DateTime.Parse(item.GetProperty("dt_txt").GetString()),
                    DateTimeKind.Utc);

                forecasts.Add(new WeatherResponse
                {
                    City = city,
                    DateTimeIst = TimeZoneHelper.ConvertUtcToIst(utcTime),
                    Temperature = item.GetProperty("main").GetProperty("temp").GetDouble(),
                    Humidity = item.GetProperty("main").GetProperty("humidity").GetInt32(),
                    Condition = item.GetProperty("weather")[0].GetProperty("description").GetString()
                });
            }

            return new ForecastResponse
            {
                City = city,
                Forecasts = forecasts
            };
        }

        // ================= FORECAST BY DATE =================

        private async Task<WeatherResponse?> GetForecastByDate(string city, DateTime requestedUtc)
        {
            var client = _httpClientFactory.CreateClient();

            string apiKey = _configuration["OpenWeather:ApiKey"];

            string url = $"https://api.openweathermap.org/data/2.5/forecast?q={city}&appid={apiKey}&units=metric";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var list = doc.RootElement.GetProperty("list");

            DateTime? closestTime = null;
            JsonElement closestItem = default;
            double smallestDifference = double.MaxValue;

            foreach (var item in list.EnumerateArray())
            {
                var forecastUtc = DateTime.SpecifyKind(
                    DateTime.Parse(item.GetProperty("dt_txt").GetString()),
                    DateTimeKind.Utc);

                var difference = Math.Abs((forecastUtc - requestedUtc).TotalMinutes);

                if (difference < smallestDifference)
                {
                    smallestDifference = difference;
                    closestTime = forecastUtc;
                    closestItem = item;
                }
            }

            if (closestTime == null)
                return null;

            return new WeatherResponse
            {
                City = city,
                DateTimeIst = TimeZoneHelper.ConvertUtcToIst(closestTime.Value),
                Temperature = closestItem.GetProperty("main").GetProperty("temp").GetDouble(),
                Humidity = closestItem.GetProperty("main").GetProperty("humidity").GetInt32(),
                Condition = closestItem.GetProperty("weather")[0].GetProperty("description").GetString()
            };
        }
    }
}
