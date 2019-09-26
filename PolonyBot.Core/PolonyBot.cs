using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using PolonyBot.Core.Configuration;

namespace PolonyBot.Core
{
    public class PolonyBot
    {
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
            // Enables retrieving "invisible" users' details 
            var config = new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true
            };

            _client = new DiscordSocketClient(config);
            _commands = new CommandService();

            _services = new ServiceCollection().BuildServiceProvider();
            
            await InstallCommands(_services).ConfigureAwait(false);

            await _client.LoginAsync(TokenType.Bot, _botToken).ConfigureAwait(false);
            await _client.SetStatusAsync(UserStatus.Online).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);

            _client.Log += _client_Log;
        }

        private static Task _client_Log(LogMessage message)
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

        public async Task InstallCommands(IServiceProvider services)
        {
            // Hook the MessageReceived Event into our Command Handler
            _client.MessageReceived += HandleCommand;

            foreach (var module in _settings.Modules)
            {
                // TODO:  Move to config in the driver APP
                var modulePath = Path.Combine(AppContext.BaseDirectory, $"{module}.dll");
                var moduleAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(modulePath);

                // Discover all of the commands in this assembly and load them.
                await _commands.AddModulesAsync(moduleAssembly, services).ConfigureAwait(false);
            }
        }

        public async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            if (!(messageParam is SocketUserMessage message))
                return;

            // Create a number to track where the prefix ends and the command begins
            var argPos = 0;

            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix(_settings.CommandPrefix, ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos))) return;

            // Create a Command Context
            var context = new CommandContext(_client, message);

            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully)
            var result = await _commands.ExecuteAsync(context, argPos, _services).ConfigureAwait(false);
            if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
            {
                await context.Channel.SendMessageAsync(result.ErrorReason).ConfigureAwait(false);
            }
        }
    }
}
