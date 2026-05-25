using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;

namespace RoomBookingBackend.Utilities;
public class TelegramBotService : IHostedService
{
    private readonly TelegramBotClient _botClient;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider; // To resolve other services if needed
    private readonly long _preferredChatId;

    public TelegramBotService(IConfiguration configuration, ILogger<TelegramBotService> logger, IServiceProvider serviceProvider)
    {
#if !DEBUG
        //_configuration = configuration;
        _logger = logger;
        //_serviceProvider = serviceProvider;

        //var botToken = _configuration["TelegramBotToken"]; // Ensure this is in your appsettings.json
        //var preferredChatId = _configuration["TelegramChatId"]; // Ensure this is in your appsettings.json

        //if (string.IsNullOrEmpty(botToken))
        //{
        //    _logger.LogError("TelegramBotToken is missing in the configuration.");
        //    throw new ArgumentNullException(nameof(botToken), "Telegram Bot Token is required.");
        //}

        //if (string.IsNullOrEmpty(preferredChatId) || !long.TryParse(preferredChatId, out _preferredChatId))
        //{
        //    _logger.LogError("TelegramChatId is missing in the configuration.");
        //    throw new ArgumentNullException(nameof(preferredChatId), "Telegram Chat ID is required.");
        //}

        //_botClient = new TelegramBotClient(botToken);
#endif
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
#if !DEBUG
        _logger.LogInformation("Telegram Bot Service is starting.");

        //var receiverOptions = new ReceiverOptions
        //{
        //    AllowedUpdates = new UpdateType[] { UpdateType.Message, UpdateType.CallbackQuery }, // Specify the update types you want to receive
        //};

        //_botClient.StartReceiving(
        //    updateHandler: HandleUpdateAsync,
        //    errorHandler: HandlePollingErrorAsync,
        //    receiverOptions: receiverOptions,
        //    cancellationToken: cancellationToken
        //);
        
        _logger.LogInformation($"Bot started successfully.");
#endif
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Telegram Bot Service is stopping.");
        //await _botClient.Close(cancellationToken);
        _logger.LogInformation("Telegram Bot Service stopped.");
    }

    public async Task SendMessageAsync(string text, long chatId = 0, ParseMode parseMode = ParseMode.Html, CancellationToken cancellationToken = default)
    {
#if !DEBUG
        //if (chatId == 0)
        //    chatId = _preferredChatId;
        
        //try
        //{
        //    await _botClient.SendMessage(
        //        chatId: chatId,
        //        text: text,
        //        parseMode: parseMode,
        //        cancellationToken: cancellationToken
        //    );
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogError($"Error sending message to chat {chatId}: {ex.Message}");
        //}
#endif
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // This handler is called for each update received from Telegram
        //if (update.Message is { } message)
        //    await BotOnMessageReceived(botClient, message, cancellationToken);
        //_logger.LogInformation($"Received update of type: {update.Type}.");
    }

    private async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        //_logger.LogInformation($"Received message type: {message.Type} in chat {message.Chat.Id}.");

        //if (message.Text is { } messageText)
        //{
        //    _logger.LogInformation($"Received message: '{messageText}' from user {message.Chat.Id}.");
        //}
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        //var ErrorMessage = exception switch
        //{
        //    ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        //    _ => exception.ToString()
        //};

        //_logger.LogError($"Polling Error: {ErrorMessage}");
        return Task.CompletedTask;
    }
}
