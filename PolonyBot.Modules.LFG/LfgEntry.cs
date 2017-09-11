using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace PolonyBot.Modules.LFG
{

    public class LfgEntry
    {
        public string Game { get; set; }
        public IUser User { get; set; }
        public bool AutoMention { get; set; }
        public DateTime Expiry { get; set; }
        public DateTime LastMentioned { get; set; }
    }
}
