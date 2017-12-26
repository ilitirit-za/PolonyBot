using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net.Http;

namespace Polony.NetCore.Core
{
    public class PolonyBot
    {
        private const string BanListToken = "1JSORrwdF8";
        private const string PublicDiscordBansApiUrl = "https://bans.discordlist.net/api";
        private const ulong PolonyPlayGroundId = 229951183882551303;

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

            var modulePath = Path.Combine(AppContext.BaseDirectory, @"PolonyBot.Modules.LFG.dll");
            var moduleAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(modulePath);

            // Discover all of the commands in this assembly and load them.
            await _commands.AddModulesAsync(moduleAssembly);
        }
              

        private async Task UserJoined(SocketGuildUser arg)
        {
            try
            {
                var httpClient = new HttpClient();

                var values = new Dictionary<string, string>
                {
                    { "token", BanListToken },
                    { "userid", arg.Id.ToString() }
                };

                var content = new FormUrlEncodedContent(values);

                var response = await httpClient.PostAsync(PublicDiscordBansApiUrl, content);

                var responseString = await response.Content.ReadAsStringAsync();

                if (responseString.Equals("true", StringComparison.CurrentCultureIgnoreCase))
                {
                    await arg.Guild.AddBanAsync(arg.Id, 1, "Known offender on Public Discord Ban List");
                }
            }
            catch (Exception e)
            {
                var channel = _client.GetChannel(PolonyPlayGroundId) as SocketTextChannel;

                await channel?.SendMessageAsync($"I could not check if {arg.Username} is a known offender.  Please be cautious when interacting!");
            }
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


