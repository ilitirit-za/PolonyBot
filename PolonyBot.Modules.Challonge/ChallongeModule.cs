using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Challonge.Abstract;
using Challonge.Infrastructure;
using Challonge.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

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
                await ReplyAsync($"Please supply a tournament ID");
                return;
            }

            var tournament = await ShowTournamentAsync(url);
            var participantList = await ShowParticipantsAsync(url);
            var participants = participantList.ToList().Select(p => p.display_name + $" ({p.id})");

            //var response = $"```" +
            //               $"Tournament: {tournament.name}\r\n" +
            //               $"Planned Start Date: {tournament.start_at}\r\n" +
            //               $"Actual Start Date: {tournament.started_at}\r\n" +
            //               $"Participants:\r\n" +
            //               String.Join(Environment.NewLine, participants) + "\r\n" +
            //               $"```";


            var builder = new EmbedBuilder();

            builder.WithTitle("Challonge Tournament Registered!")
                .WithDescription(tournament.name)
                .WithAuthor(Context.User.Username)
                .WithColor(Color.Orange)
                .WithFooter("South African FGC")
                .WithTimestamp(new DateTimeOffset(DateTime.Now))
                .WithUrl(tournament.full_challonge_url)
                .WithThumbnailUrl("https://i2.wp.com/s3.amazonaws.com/challonge_app/misc/challonge_fireball_gray.png")
                .AddField("Game", tournament.game_name, true)
                .AddField("Status", tournament.state ?? "Unknown", true)
                .AddField("Planned Start Date", String.IsNullOrWhiteSpace(tournament.start_at) 
                    ? "Unknown"
                    : DateTime.Parse(tournament.start_at).ToString(CultureInfo.CurrentCulture))
                .AddField($"Participants ({participants.Count()})", String.Join(Environment.NewLine, participants));

            await Context.Channel.SendMessageAsync("", false, builder.Build());
        }

        private async Task<ChallongeTournament> ShowTournamentAsync(string tournamentId)
        {
            var tournament = await Task.Run(() => _api.ShowTournament(tournamentId));
            
            return tournament;
        }

        private async Task<IEnumerable<ChallongeParticipant>> ShowParticipantsAsync(string tournamentId)
        {
            return await Task.Run(() => _api.AllParticipants(tournamentId));
        }
    }
}
