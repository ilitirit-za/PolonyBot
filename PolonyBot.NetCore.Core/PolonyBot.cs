using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace Polony.NetCore.Core
{
    public class PolonyBot
    {
        private readonly string _botToken;
        private CommandService _commands;
        private DiscordSocketClient _client;
        private IServiceProvider _services;

        public PolonyBot(string botToken)
        {
            _botToken = botToken;
        }

        public async Task Start()
        {
            _client = new DiscordSocketClient();
            _commands = new CommandService();

            _services = new ServiceCollection()
                    .BuildServiceProvider();

            await InstallCommands();

            await _client.LoginAsync(TokenType.Bot, _botToken);
            await _client.StartAsync();

            //await Task.Delay(-1);
        }

        public async Task InstallCommands()
        {
            // Hook the MessageReceived Event into our Command Handler
            _client.MessageReceived += HandleCommand;

            var modulePath = Path.Combine(AppContext.BaseDirectory, @"PolonyBot.Modules.LFG.dll");
            var moduleAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(modulePath);

            // Discover all of the commands in this assembly and load them.
            await _commands.AddModulesAsync(moduleAssembly);
        }

        public async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix('.', ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos))) return;
            // Create a Command Context
            var context = new CommandContext(_client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully)
            var result = await _commands.ExecuteAsync(context, argPos, _services);
            if (!result.IsSuccess)
                await context.Channel.SendMessageAsync(result.ErrorReason);
        }
    }
}


