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
        private const ulong TheButcheryChannelId = 479947815989149699;

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
            var logChannel = _client.GetChannel(TheButcheryChannelId) as SocketTextChannel;
            try {
                var userId = arg.Id.ToString();
                var httpClient = new HttpClient();

                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(BanListToken);

                await _client_Log(new LogMessage(LogSeverity.Info, "DBANS", $"DBAN query for userid {arg.Id}: {String.Format(PublicDiscordBansApiUrl, userId)}"));
                var response = await httpClient.GetAsync(String.Format(PublicDiscordBansApiUrl, userId));

                if (response.IsSuccessStatusCode)
                {
                    responseString = await response.Content.ReadAsStringAsync();
                    await _client_Log(new LogMessage(LogSeverity.Info, "DBANS", $"DBAN response on userid {arg.Id} : {responseString}"));
                    await logChannel?.SendMessageAsync($"Response was: {response.StatusCode} | {responseString}");

                    var responseObject = JArray.Parse(responseString)[0];
                    if (responseObject["banned"] != null && responseObject["banned"].Value<string>().Equals("1"))
                    { 
                        await arg.Guild.AddBanAsync(arg.Id, 1, $"Banned: {responseObject["reason"].Value<string>()}");
                    }
                    else if (responseObject["error"] != null) 
                    {
                        await _client_Log(new LogMessage(LogSeverity.Warning, "DBANS", $"DBAN error when checking userid {arg.Id} : {responseObject["error"].Value<string>()}"));
                        
                        await logChannel?.SendMessageAsync($"API error when checking bans for userid {arg.Id}.");
                        await logChannel?.SendMessageAsync($"Response was: {response.StatusCode} | {responseObject["error"].Value<string>()}");

                    }
                    else
                    {
                        await _client_Log(new LogMessage(LogSeverity.Warning, "DBANS", $"No ban found for user {arg.Nickname} : {responseString}"));
                        await logChannel?.SendMessageAsync($"No ban found for userid {arg.Id} : {responseString}.");
                    }
                }
                else
                {
                    await _client_Log(new LogMessage(LogSeverity.Info, "DBANS", $"DBAN HTTP API error on userid {arg.Id} : {response.StatusCode} : {response.ReasonPhrase}"));

                    await logChannel?.SendMessageAsync($"API error when checking bans for userid {arg.Id}.");
                    await logChannel?.SendMessageAsync($"Response was: {response.StatusCode} | {response.ReasonPhrase}");
                }
            }
            catch (Exception e) {
                await logChannel?.SendMessageAsync($"Unexpected error occured when checking bans for user {arg.Id} : ({e.Message})");
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
