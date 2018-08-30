using Challonge.Models;
using PolonyBot.Modules.Challonge.DAL;
using PolonyBot.Modules.Challonge.DAL.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PolonyBot.Modules.Challonge
{
    public class ChallongeDao
    {
        public Task<int> RegisterTournamentAsync(Tournament tournament)
        {
            var dbContext = new ChallongeDbContextFactory().CreateDbContext(null);
            var savedTournament = dbContext.Tournaments.Add(tournament).Entity;
            return dbContext.SaveChangesAsync();
        }
    }
}
