using System;
using System.Collections.Generic;
using System.Text;

namespace PolonyBot.Core.Configuration
{
    public class PolonyBotSettings
    {
        public char CommandPrefix { get; set; }
        public List<string> Modules { get; set; } = new List<string>();
    }
}
