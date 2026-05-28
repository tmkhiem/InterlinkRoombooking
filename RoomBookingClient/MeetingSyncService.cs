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
    public event EventHandler<string>? LogOccurred;

    public MeetingSyncService()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _timer = new Timer(async _ => await RefreshAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start(Func<ClientSettings> settingsProvider)
    {
        _settingsProvider = settingsProvider;
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
        Log("Đã lên lịch đồng bộ mỗi 30 giây.");
    }

    public void Stop()
    {
        Log("Đang dừng dịch vụ đồng bộ.");
        _timer.Change(Timeout.Infinite, Timeout.Infinite);

        while (Interlocked.CompareExchange(ref _refreshInProgress, 0, 0) == 1)
        {
            Thread.Sleep(25);
        }

        _timer.Dispose();
        _httpClient.Dispose();
        Log("Đã dừng dịch vụ đồng bộ.");
    }

    public void TriggerRefresh()
    {
        Log("Đã yêu cầu đồng bộ thủ công.");
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _refreshInProgress, 1) == 1)
        {
            Log("Bỏ qua lần đồng bộ vì một tiến trình khác vẫn đang chạy.");
            return;
        }

        try
        {
            Log("Bắt đầu đồng bộ cuộc họp.");
            var settings = _settingsProvider?.Invoke();
            if (settings == null)
            {
                Log("Không có cấu hình để đồng bộ.");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.ServerUrl) || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                Log("Thiếu URL máy chủ hoặc khóa API, xóa cuộc họp hiện tại.");
                MeetingUpdated?.Invoke(this, null);
                return;
            }

            var baseUrl = settings.ServerUrl.Trim().TrimEnd('/');
            var uri = $"{baseUrl}/api/client/current-meeting?room={settings.RoomNumber}";
            Log($"Đang tải thông tin cuộc họp từ '{uri}'.");

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Api-Key", settings.ApiKey);
            using var response = await _httpClient.SendAsync(request);
            Log($"Máy chủ trả về {(int)response.StatusCode} {response.ReasonPhrase}.");

            if (!response.IsSuccessStatusCode)
            {
                ErrorOccurred?.Invoke(this, $"Đồng bộ thất bại: {(int)response.StatusCode} {response.ReasonPhrase}");
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            var meetingResponse = await JsonSerializer.DeserializeAsync<CurrentMeetingResponse>(stream, _jsonOptions);
            if (meetingResponse?.HasMeeting != true || meetingResponse.Meeting == null)
            {
                Log("Không có cuộc họp hiện tại từ máy chủ.");
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
                Log($"Không có thay đổi cho cuộc họp #{meeting.RoomBookingId}.");
                return;
            }

            var folder = TempMeetingStorage.CreateMeetingFolder(meeting.RoomBookingId);
            Log($"Đã tạo thư mục tạm '{folder}'.");
            foreach (var document in meeting.Documents)
            {
                await DownloadDocumentAsync(baseUrl, settings, meeting.RoomBookingId, document, folder);
            }

            var detailsPath = Path.Combine(folder, "meeting-details.json");
            var detailsJson = JsonSerializer.Serialize(meeting, _jsonOptions);
            File.WriteAllText(detailsPath, detailsJson);
            Log($"Đã lưu chi tiết cuộc họp vào '{detailsPath}'.");

            if (!DateTime.TryParseExact(
                    $"{meeting.Date} {meeting.StartTime}",
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var startLocal))
            {
                ErrorOccurred?.Invoke(this, $"Đồng bộ thất bại: thời gian bắt đầu không hợp lệ '{meeting.Date} {meeting.StartTime}'.");
                return;
            }

            if (!DateTime.TryParseExact(
                    $"{meeting.Date} {meeting.EndTime}",
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var endLocal))
            {
                ErrorOccurred?.Invoke(this, $"Đồng bộ thất bại: thời gian kết thúc không hợp lệ '{meeting.Date} {meeting.EndTime}'.");
                return;
            }

            _lastMeetingVersion = version;
            _lastMeetingId = meeting.RoomBookingId;
            Log($"Đã cập nhật ảnh chụp cuộc họp #{meeting.RoomBookingId}.");

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
            ErrorOccurred?.Invoke(this, $"Đồng bộ thất bại: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInProgress, 0);
            Log("Kết thúc chu kỳ đồng bộ.");
        }
    }

    private async Task DownloadDocumentAsync(string baseUrl, ClientSettings settings, int bookingId, MeetingDocument document, string folder)
    {
        var fileUrl = $"{baseUrl}/api/client/bookings/{bookingId}/documents/{document.BookingDocumentId}?room={settings.RoomNumber}";
        Log($"Đang tải tệp '{document.OriginalFileName}' từ '{fileUrl}'.");

        using var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
        request.Headers.Add("X-Api-Key", settings.ApiKey);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var safeFileName = MakeSafeFileName(document.OriginalFileName);
        var targetPath = Path.Combine(folder, safeFileName);

        using var source = await response.Content.ReadAsStreamAsync();
        using var destination = File.Create(targetPath);
        await source.CopyToAsync(destination);
        Log($"Đã lưu tệp vào '{targetPath}'.");
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

    private void Log(string message)
    {
        LogOccurred?.Invoke(this, message);
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
        public List<MeetingDocument> Documents { get; set; } = new List<MeetingDocument>();
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

public sealed class MeetingSnapshot
{
    public MeetingSnapshot(
        int bookingId,
        string title,
        string note,
        string creatorDisplay,
        DateTime startLocal,
        DateTime endLocal,
        string downloadFolder)
    {
        BookingId = bookingId;
        Title = title;
        Note = note;
        CreatorDisplay = creatorDisplay;
        StartLocal = startLocal;
        EndLocal = endLocal;
        DownloadFolder = downloadFolder;
    }

    public int BookingId { get; }
    public string Title { get; }
    public string Note { get; }
    public string CreatorDisplay { get; }
    public DateTime StartLocal { get; }
    public DateTime EndLocal { get; }
    public string DownloadFolder { get; }
}
