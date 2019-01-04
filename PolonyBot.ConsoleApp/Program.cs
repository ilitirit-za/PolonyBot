using System;
using System.IO;
using System.Threading.Tasks;

namespace PolonyBot.ConsoleApp
{
    class Program
    {
        public static void Main() => new Program().Start().GetAwaiter().GetResult();

        public async Task Start()
        {
            try
            {
                var key = File.ReadAllText("PolonyBot.key").Trim();

                var polonyBot = new Core.PolonyBot(key);
                await polonyBot.Start().ConfigureAwait(false);
                await Task.Delay(-1).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not run: {e.Message}");
                Console.WriteLine($"Stack Trace: {e.StackTrace}");
            }
        }
    }
}
