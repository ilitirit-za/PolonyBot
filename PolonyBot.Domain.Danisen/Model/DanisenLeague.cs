using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Polony.Domain.Danisen.Model
{
    public class DanisenLeague
    {
        public int DanisenLeagueId { get; set; }
        public Game Game { get; set; }
        public int RankSetId { get; set; }
        public bool MultipleCharactersAllowed { get; set; }
        public bool Enabled { get; set; }
    }
}
