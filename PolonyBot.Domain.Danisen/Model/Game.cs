using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Polony.Domain.Danisen.Model
{
    public class Game
    {
        public Game(int gameId, string name, string shortName)
        {
            GameId = gameId;
            Name = name;
            ShortName = shortName;
        }

        public int GameId { get; private set; }
        public string Name { get; private set; }
        public string ShortName { get; private set; }
    }
}
