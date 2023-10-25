namespace VkSignBot
{
    public class BotClient : IBotClient
    {
        private static readonly Random Random = new Random();
        private readonly string? _botToken;
        private readonly string? _appToken;
        private readonly uint _appId;
        private readonly ulong _groupId;
        private readonly IVkApi _userApi;
        private readonly IVkApi _botApi;

        public IVkApi UserApi { get => _userApi; init => _userApi = value; }
        public IVkApi BotApi { get => _botApi; init => _botApi = value; }

        public BotClient(IOptions<BotClientOptions> options)
        {
            _botToken = options.Value.BotToken;
            _appToken = options.Value.AppToken;
            _appId = options.Value.AppId;
            _groupId = options.Value.GroupId;

            _userApi = new VkApi();
            _botApi = new VkApi();
        }

        public async Task AuthorizeAsync()
        {
            await _userApi.AuthorizeAsync(new ApiAuthParams
            {
                AccessToken = _appToken,
                ApplicationId = _appId
            });

            await _botApi.AuthorizeAsync(new ApiAuthParams
            {
                AccessToken = _botToken,
            });

            if ((_botApi.IsAuthorized && (_botApi.IsAuthorized || _userApi.IsAuthorized)) == false)
                throw new VkAuthorizationException("Bot is not authorized");
            Console.WriteLine("Bot is authorized");
        }

        public async Task StartPolling()
        {
            Console.WriteLine("Polling...");
            try
            {
                while (true)
                {
                    var longPollResponse = await _botApi!.Groups.GetLongPollServerAsync(_groupId);
                    var updates = await _botApi!.Groups.GetBotsLongPollHistoryAsync(new BotsLongPollHistoryParams
                    {
                        Ts = longPollResponse.Ts,
                        Key = longPollResponse.Key,
                        Server = longPollResponse.Server,
                        Wait = 25
                    });

                    if (updates.Updates is null || updates.Updates.Count == 0) continue;
                    if (updates.Updates.Count == 0) continue;

                    var handleUpdatesTask = new Task(async () =>
                    {
                        foreach (var update in updates.Updates)
                        {
                            await HandleUpdateAsync(update);
                        }
                    });
                    handleUpdatesTask.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public async Task HandleUpdateAsync(GroupUpdate update)
        {
            switch (update.Instance)
            {
                case MessageNew:
                    await HandleMessageAsync(update);
                    break;
            }
        }

        public async Task HandleMessageAsync(GroupUpdate update)
        {
            const char commandSymbol = '/';
            Message message = (update.Instance as MessageNew)!.Message;
            var text = message.Text.Trim().ToLower();

            if (!text.StartsWith(commandSymbol))
                return;

            var command = text.Split(new char[] { commandSymbol, ' ' })[1];
            var parameters =
                text.Split(' ').Length > 1 && text.IndexOf(command) == 1 ?
                text.Split(' ').AsSpan<string>().Slice(1).ToArray() :
                null;
            
            var cmdService = new CommandService(this);
            switch (command)
            {
                case "роспись":
                    if (message.Attachments.Count > 0)
                    {
                        var post = (message.Attachments.First(attach => attach.Type == typeof(Wall)).Instance as Wall);
                        await cmdService.MakeSign(message, post);
                        break;
                    }
                    else if (parameters is null)
                    {
                        await _botApi.Messages.SendAsync(new MessagesSendParams
                        {
                            UserId = message.FromId,
                            RandomId = Random.NextInt64(),
                            Message = $"Ни ссылки на пост, ни репост поста я не вижу, а значит расписаться нигде не могу."
                        });
                        break;
                    }
                    await cmdService.MakeSign(message, parameters[0]);
                    break;
                default:
                    await _botApi.Messages.SendAsync(new MessagesSendParams
                    {
                        UserId = message.FromId,
                        RandomId = Random.NextInt64(),
                        Message = "Я не знаю такой команды, чел."
                    });
                    break;
            }
            Console.WriteLine($"{Task.CurrentId} is running in the method {nameof(HandleMessageAsync)}");
        }
    }

    public class BotClientOptions
    {
        public string? BotToken { get; set; }
        public string? AppToken { get; set; }
        public uint AppId { get; set; }
        public ulong GroupId { get; set; }
    }
}