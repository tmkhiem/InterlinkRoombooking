namespace RoomBookingClient;

public sealed class ClientSettings
{
    public string ServerUrl { get; set; } = string.Empty;
    public int RoomNumber { get; set; } = 2;
    public string ApiKey { get; set; } = string.Empty;

    public static ClientSettings Default() => new()
    {
        ServerUrl = "http://localhost:5000",
        RoomNumber = 2,
        ApiKey = string.Empty
    };
}
