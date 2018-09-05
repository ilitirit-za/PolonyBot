using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using PolonyBot.Core.Configuration;

namespace PolonyBot.Core
{
    public class PolonyBot
    {
        private const string BanListToken = "GVnPX_UmfTTE7yLA6TGHDsC4nptgnKFaljlWuuwtBog";
        private const string PublicDiscordBansApiUrl = "https://bans.discord.id/api/check.php?user_id={0}";
        private const ulong PolonyPlayGroundId = 229951183882551303;

        private readonly string _botToken;
        private readonly PolonyBotSettings _settings;
        private CommandService _commands;
        private DiscordSocketClient _client;
        private IServiceProvider _services;

        public PolonyBot(string botToken)
        {
            _botToken = botToken;
            
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("PolonyBot.settings.json", optional: false, reloadOnChange: true);
            
            var configuration = builder.Build();
            _settings = configuration
                .GetSection(nameof(PolonyBotSettings))
                .Get<PolonyBotSettings>();
        }

        public async Task Start()
        {
            _client = new DiscordSocketClient();
            _commands = new CommandService();

            _services = new ServiceCollection().BuildServiceProvider();

            await InstallCommands();


            await _client.LoginAsync(TokenType.Bot, _botToken);
            await _client.SetStatusAsync(UserStatus.Online);
            await _client.StartAsync();

            _client.Log += _client_Log;
            //await Task.Delay(-1);
        }

        private Task _client_Log(LogMessage message)
        {

            var cc = Console.ForegroundColor;
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }
            Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message}");
            Console.ForegroundColor = cc;

            return Task.CompletedTask;
        }

        public async Task InstallCommands()
        {
            // Hook the MessageReceived Event into our Command Handler
            _client.MessageReceived += HandleCommand;
            _client.UserJoined += UserJoined;

            foreach (var module in _settings.Modules)
            {
                // TODO:  Move to config in the driver APP
                var modulePath = Path.Combine(AppContext.BaseDirectory, $"{module}.dll");
                var moduleAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(modulePath);

                // Discover all of the commands in this assembly and load them.
                await _commands.AddModulesAsync(moduleAssembly);
            }
        }

        [Command("join", RunMode = RunMode.Async)]
        private async Task UserJoined(SocketGuildUser arg)
        {
            var responseString = "";
            try
            {
                var userId = arg.Id.ToString();
                var httpClient = new HttpClient();

                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(BanListToken);
                var response = await httpClient.GetAsync(String.Format(PublicDiscordBansApiUrl, userId));

                responseString = await response.Content.ReadAsStringAsync();

                var responseObject = JArray.Parse(responseString)[0];
                if (responseObject["banned"].Value<string>().Equals("1"))
                {
                    await arg.Guild.AddBanAsync(arg.Id, 1, $"Banned: {responseObject["reason"].Value<string>()}");
                }


            }
            catch (Exception e)
            {
                var channel = _client.GetChannel(PolonyPlayGroundId) as SocketTextChannel;

                await channel?.SendMessageAsync($"I could not check if {arg.Username} is a known offender ({e.Message})");
                await channel?.SendMessageAsync($"Response was: {responseString}");
            }
        }

        public async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            if (!(messageParam is SocketUserMessage message)) return;


            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix(_settings.CommandPrefix, ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos))) return;

            // Create a Command Context
            var context = new CommandContext(_client, message);

            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully)
            var result = await _commands.ExecuteAsync(context, argPos, _services);
            if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
            {
                await context.Channel.SendMessageAsync(result.ErrorReason);
            }
        }
    }
}
