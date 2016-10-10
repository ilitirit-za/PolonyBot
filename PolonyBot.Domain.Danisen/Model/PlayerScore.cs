using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Polony.Domain.Danisen.Model
{
    public class PlayerScore
    {
        public string PlayerName { get; set; }
        public string GameName { get; set; }
        public string Character { get; set; }
        public string Rank { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public double WinRate { get; set; }
        public int LeaguePoints { get; set; }
    }
}
