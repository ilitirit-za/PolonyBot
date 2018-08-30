using Challonge.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PolonyBot.Modules.Challonge.DAL.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PolonyBot.Modules.Challonge.DAL
{
    public class ChallongeDbContext : DbContext
    {
        public ChallongeDbContext(DbContextOptions<ChallongeDbContext> options) : base(options) { }

        public DbSet<Tournament> Tournaments { get; set; }

    }

    public class ChallongeDbContextFactory : IDesignTimeDbContextFactory<ChallongeDbContext>
    {
        public ChallongeDbContext CreateDbContext(string[] args)
        {
            // TODO: Pass ConnectionString (and Storage Type?) to method
            var optionsBuilder = new DbContextOptionsBuilder<ChallongeDbContext>().UseSqlite("Data Source = PolonyBot.db");
            return new ChallongeDbContext(optionsBuilder.Options);
        }
    }
}
