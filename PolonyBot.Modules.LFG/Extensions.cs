using System;
using System.Collections.Generic;
using System.Text;

namespace PolonyBot.Modules.LFG
{
    public static class Extensions
    {
        const int MaxStringLength = 1980;
        const string TruncatedIndicator = "... [truncated]";
        public static string AsDiscordResponse(this string text)
        {
            if (text == null)
                return "[NULL RESPONSE].  pwnBait pls.";

            text = text.Trim();
            if (text.Length > MaxStringLength)
                text = $"{text.Substring(0, MaxStringLength)}{TruncatedIndicator}";

            return text;
        }
    }
}
