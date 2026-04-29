using System.ComponentModel;
using Arrr.Core.Attributes;

namespace Arrr.Plugin.Jellyfin.Data;

public class JellyfinPluginConfig
{
    [Description("Jellyfin server URL (e.g. http://jellyfin:8096)")]
    public string ServerUrl { get; set; } = "";

    [Sensitive, Description("Jellyfin API key — used to verify connectivity in the test")]
    public string ApiKey { get; set; } = "";

    [Description("Send a notification when a new movie or episode is added to the library")]
    public bool NotifyOnItemAdded { get; set; } = true;

    [Description("Send a notification when a user starts playing media")]
    public bool NotifyOnPlaybackStart { get; set; } = false;

    [Description("Send a notification when a user stops / finishes playing media")]
    public bool NotifyOnPlaybackStop { get; set; } = false;

    [Description("Include movies in ItemAdded notifications")]
    public bool IncludeMovies { get; set; } = true;

    [Description("Include TV episodes in ItemAdded notifications")]
    public bool IncludeEpisodes { get; set; } = true;

    [Description("Include music tracks in ItemAdded notifications")]
    public bool IncludeMusic { get; set; } = false;
}
