using Challonge.Models;
using PolonyBot.Modules.Challonge.DAL.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PolonyBot.Modules.Challonge.DAL.Extensions
{
    public static class ModelExtensions
    {
        public static Tournament ToTournament(this ChallongeTournament challongeTournament)
        {
            // TODO: Pass participants in?
            return new Tournament
            {
                Id = challongeTournament.id,
                Url = challongeTournament.full_challonge_url,
                Name = challongeTournament.name,
                Game = challongeTournament.game_name,
                Status = challongeTournament.state,
                PlannedStartDate = challongeTournament.start_at,
                StartedAt = challongeTournament.started_at,
            };

        }

        public static Participant ToParticipant(this ChallongeParticipant challongeParticipant)
        {
            return new Participant
            {
                Id = challongeParticipant.id,
                Name = challongeParticipant.display_name,
            };
        }
    }
}
