using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Polony.Domain.Danisen.Model
{
    public class Player
    {
        public int PlayerId { get; set; }
        public string DiscordUserId { get; set; }
        public string Name { get; set; }
    }
}
