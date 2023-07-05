namespace VkSignBot.Abstractions
{
    public interface IBotClient
    {
        IVkApi UserApi { get; init; }
        IVkApi BotApi { get; init; }
        Task StartPolling();
        Task HandleUpdateAsync(GroupUpdate update);
        Task HandleMessageAsync(GroupUpdate update);
    }
}