using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RoomBookingClient;

public sealed class MeetingSyncService
{
    private readonly HttpClient _httpClient = new();
    private readonly Timer _timer;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    private Func<ClientSettings>? _settingsProvider;
    private string? _lastMeetingVersion;
    private int? _lastMeetingId;
    private int _refreshInProgress;

    public event EventHandler<MeetingSnapshot?>? MeetingUpdated;
    public event EventHandler<string>? ErrorOccurred;

    public MeetingSyncService()
    {
        _timer = new Timer(async _ => await RefreshAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start(Func<ClientSettings> settingsProvider)
    {
        _settingsProvider = settingsProvider;
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _timer.Dispose();
        _httpClient.Dispose();
    }

    public void TriggerRefresh()
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _refreshInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            var settings = _settingsProvider?.Invoke();
            if (settings == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.ServerUrl) || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                MeetingUpdated?.Invoke(this, null);
                return;
            }

            var baseUrl = settings.ServerUrl.Trim().TrimEnd('/');
            var uri = $"{baseUrl}/api/client/current-meeting?room={settings.RoomNumber}";

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Api-Key", settings.ApiKey);
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                ErrorOccurred?.Invoke(this, $"Sync failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var meetingResponse = await JsonSerializer.DeserializeAsync<CurrentMeetingResponse>(stream, _jsonOptions);
            if (meetingResponse?.HasMeeting != true || meetingResponse.Meeting == null)
            {
                _lastMeetingVersion = null;
                _lastMeetingId = null;
                TempMeetingStorage.DeleteCurrentMeetingFolder();
                MeetingUpdated?.Invoke(this, null);
                return;
            }

            var meeting = meetingResponse.Meeting;
            var version = BuildMeetingVersion(meeting);
            if (_lastMeetingVersion == version && _lastMeetingId == meeting.RoomBookingId)
            {
                return;
            }

            var folder = TempMeetingStorage.CreateMeetingFolder(meeting.RoomBookingId);
            foreach (var document in meeting.Documents)
            {
                await DownloadDocumentAsync(baseUrl, settings, meeting.RoomBookingId, document, folder);
            }

            var detailsPath = Path.Combine(folder, "meeting-details.json");
            var detailsJson = JsonSerializer.Serialize(meeting, _jsonOptions);
            await File.WriteAllTextAsync(detailsPath, detailsJson);

            var startLocal = DateTime.ParseExact(
                $"{meeting.Date} {meeting.StartTime}",
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal);
            var endLocal = DateTime.ParseExact(
                $"{meeting.Date} {meeting.EndTime}",
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal);

            _lastMeetingVersion = version;
            _lastMeetingId = meeting.RoomBookingId;

            MeetingUpdated?.Invoke(this, new MeetingSnapshot(
                meeting.RoomBookingId,
                meeting.Title,
                meeting.Note ?? string.Empty,
                string.IsNullOrWhiteSpace(meeting.Name) ? meeting.Creator : $"{meeting.Creator} {meeting.Name}",
                startLocal,
                endLocal,
                folder));
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Sync failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInProgress, 0);
        }
    }

    private async Task DownloadDocumentAsync(string baseUrl, ClientSettings settings, int bookingId, MeetingDocument document, string folder)
    {
        var fileUrl = $"{baseUrl}/api/client/bookings/{bookingId}/documents/{document.BookingDocumentId}?room={settings.RoomNumber}";

        using var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
        request.Headers.Add("X-Api-Key", settings.ApiKey);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var safeFileName = MakeSafeFileName(document.OriginalFileName);
        var targetPath = Path.Combine(folder, safeFileName);

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var destination = File.Create(targetPath);
        await source.CopyToAsync(destination);
    }

    private static string MakeSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safe = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "file.bin" : safe;
    }

    private static string BuildMeetingVersion(MeetingDto meeting)
    {
        var docs = string.Join("|", meeting.Documents.Select(d => $"{d.BookingDocumentId}:{d.UploadedAtUtc:o}:{d.FileSizeBytes}"));
        return $"{meeting.RoomBookingId}:{meeting.Title}:{meeting.StartTime}:{meeting.EndTime}:{docs}";
    }

    private sealed class CurrentMeetingResponse
    {
        public bool HasMeeting { get; set; }
        public MeetingDto? Meeting { get; set; }
    }

    private sealed class MeetingDto
    {
        public int RoomBookingId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public int Room { get; set; }
        public string Date { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public List<MeetingDocument> Documents { get; set; } = [];
    }
}

public sealed class MeetingDocument
{
    public long BookingDocumentId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

public sealed record MeetingSnapshot(
    int BookingId,
    string Title,
    string Note,
    string CreatorDisplay,
    DateTime StartLocal,
    DateTime EndLocal,
    string DownloadFolder);
