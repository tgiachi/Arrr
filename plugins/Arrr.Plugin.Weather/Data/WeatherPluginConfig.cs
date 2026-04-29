using System.ComponentModel;

namespace Arrr.Plugin.Weather.Data;

public class WeatherPluginConfig
{
    [Description("Display name for this location (shown in notifications and digest)")]
    public string LocationName { get; set; } = "";

    [Description("City name to geocode automatically (leave empty if providing Latitude/Longitude directly)")]
    public string City { get; set; } = "";

    [Description("Latitude coordinate — filled automatically when City is geocoded")]
    public double Latitude { get; set; }

    [Description("Longitude coordinate — filled automatically when City is geocoded")]
    public double Longitude { get; set; }

    [Description("Temperature unit: celsius or fahrenheit")]
    public string TemperatureUnit { get; set; } = "celsius";

    [Description("How often to fetch weather data, in minutes")]
    public int PollIntervalMinutes { get; set; } = 30;

    [Description("How many hours ahead to scan for severe-weather alerts")]
    public int AlertLookaheadHours { get; set; } = 3;

    [Description("Section heading used in digest notifications")]
    public string DigestSectionTitle { get; set; } = "Weather";
}
