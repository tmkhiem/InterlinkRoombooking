namespace RoomBookingServer.Options;

public sealed class ClientAppOptions
{
    public const string SectionName = "ClientApp";

    public string ApiKey { get; set; } = string.Empty;
}
