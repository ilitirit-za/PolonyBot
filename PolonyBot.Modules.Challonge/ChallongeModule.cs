using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Challonge.Abstract;
using Challonge.Infrastructure;
using Challonge.Models;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Monad;
using PolonyBot.Modules.Challonge.DAL.Models;

namespace PolonyBot.Modules.Challonge
{
    public class ChallongeModule : ModuleBase
    {
        private IChallonge _api = new ChallongeApi("", "");
        public ChallongeModule()
        {

        }


        [Command("chal"), Summary("Challonge")]
        public async Task Chal(string url = null, string command = null)
        {
            var user = Context.User as SocketGuildUser;
            if (!user.Roles.Any(r => r.Name == "PolonyAdmin" || r.Name == "PolonyBot-Dev"))
                return;

            if (String.IsNullOrEmpty(url))
            {
                await ReplyAsync("Please supply a tournament ID");
                return;
            }

            try
            {
                var tournamentResult = await _api.ShowTournamentAsync(url);
                if (!tournamentResult)
                {
                    await ReplyAsync($"Could not retrieve Tournament: {tournamentResult.Message}");
                    return;
                }
                var tournament = tournamentResult.Value;

                var participantListResult = await _api.AllParticipantsAsync(url);
                if (!participantListResult)
                {
                    await ReplyAsync($"Could not retrieve Participants: {tournamentResult.Message}");
                    return;
                }
                var participants = participantListResult.Value.ToList().Select(p => p.display_name + $" ({p.id})").ToList();

                var builder = new EmbedBuilder();

                builder.WithTitle("Challonge Tournament Registered!")
                    .WithDescription(tournament.name)
                    .WithAuthor(Context.User.Username)
                    .WithColor(Color.Orange)
                    .WithFooter("South African FGC")
                    .WithTimestamp(new DateTimeOffset(DateTime.Now))
                    .WithUrl(tournament.full_challonge_url)
                    .WithThumbnailUrl("https://i2.wp.com/s3.amazonaws.com/challonge_app/misc/challonge_fireball_gray.png")
                    .AddField("Game", tournament.game_name ?? "Unknown", true)
                    .AddField("Status", tournament.state ?? "Unknown", true)
                    .AddField("Planned Start Date", String.IsNullOrWhiteSpace(tournament.start_at)
                        ? "Unknown"
                        : DateTime.Parse(tournament.start_at).ToString(CultureInfo.CurrentCulture))
                    .AddField($"Participants ({participants.Count})", String.Join(Environment.NewLine, participants));

                await Context.Channel.SendMessageAsync("", false, builder.Build());
            }
            catch (Exception e)
            {
                await ReplyAsync("pwnbait probably broke something again");
            }
        }

    }
}
