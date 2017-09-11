using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Discord.Commands;

namespace PolonyBot.Modules.Glossary
{
    // Create a module with no prefix
    public class GlossaryModule : ModuleBase
    {
        public Dictionary<string, string> Glossary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private void LoadGlossary()
        {
            var text = System.IO.File.ReadAllText("glossary.txt");

            var splitText = text.Split(new[] { $"{Environment.NewLine}==" }, StringSplitOptions.None);

            foreach (var entry in splitText)
            {
                var entryDef = entry.Split(new[] { $"==" }, StringSplitOptions.RemoveEmptyEntries);
                var term = entryDef[0].Trim();
                var definition = entryDef[1].Trim();

                Glossary.Add(term, definition);
            }
        }

        public GlossaryModule()
        {
            LoadGlossary();
        }


        // ~say hello -> hello
        [Command("define"), Summary("Returns the definition of an FG term.")]
        public async Task Define([Remainder, Summary("The term")] string term)
        {
            var response = String.Empty;
            if (!Glossary.TryGetValue(term, out response))
            {
                var possible = Glossary.Keys.ToAsyncEnumerable()
                    .Select(key => new Tuple<string, int>(key, LevenshteinDistance.Compute(term, key)))
                    .OrderBy(tuple => tuple.Item2)
                    .Take(3)
                    .Select(tuple => tuple.Item1)
                    .Aggregate((current, next) => $"{current}, {next}")
                    .Result;

                response = $"No definition found for {term}.  Suggestions: {possible}";
            }

            await ReplyAsync($"```{response}```");
        }
    }
}
