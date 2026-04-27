using System.Text;
using Arrr.Core.Data.Digest;
using Arrr.Core.Interfaces;
using Ical.Net;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Digest.Providers;

internal class CalDavDigestProvider : IDigestProvider
{
    private readonly HttpClient _http;
    private readonly string _calendarUrl;
    private readonly string _username;
    private readonly string _password;
    private readonly ILogger _logger;

    public string SectionTitle { get; }

    internal CalDavDigestProvider(
        HttpClient http,
        string calendarUrl,
        string username,
        string password,
        string sectionTitle,
        ILogger logger)
    {
        _http = http;
        _calendarUrl = calendarUrl;
        _username = username;
        _password = password;
        SectionTitle = sectionTitle;
        _logger = logger;
    }

    public async Task<DigestSection> GetSectionAsync(DateOnly forDate, CancellationToken ct)
    {
        var section = new DigestSection { Title = SectionTitle };

        if (string.IsNullOrEmpty(_calendarUrl))
        {
            return section;
        }

        var icsContent = await FetchIcsAsync(ct);

        if (icsContent is null)
        {
            return section;
        }

        Calendar calendar;

        try
        {
            calendar = Calendar.Load(icsContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Digest: failed to parse ICS content");

            return section;
        }

        var targetDate = forDate.ToDateTime(TimeOnly.MinValue).Date;

        var events = calendar.Events
            .Where(e => GetEventLocalDate(e) == targetDate)
            .OrderBy(e => e.IsAllDay ? DateTime.MinValue : e.DtStart.AsUtc.ToLocalTime())
            .ToList();

        foreach (var evt in events)
        {
            string text;

            if (evt.IsAllDay)
            {
                text = $"{evt.Summary} (all day)";
            }
            else
            {
                var localTime = evt.DtStart.AsUtc.ToLocalTime();
                text = $"{localTime:HH:mm} - {evt.Summary}";
            }

            section.Items.Add(new DigestItem { Text = text });
        }

        return section;
    }

    private static DateTime GetEventLocalDate(Ical.Net.CalendarComponents.CalendarEvent evt)
        => evt.IsAllDay
            ? evt.DtStart.Value.Date
            : evt.DtStart.AsUtc.ToLocalTime().Date;

    private async Task<string?> FetchIcsAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _calendarUrl);

        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            request.Headers.Authorization = new("Basic", credentials);
        }

        try
        {
            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Digest: ICS fetch failed for {Url}", _calendarUrl);

            return null;
        }
    }
}
