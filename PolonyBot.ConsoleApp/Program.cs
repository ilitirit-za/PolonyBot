using System;
using System.IO;
using System.Threading.Tasks;

namespace PolonyBot.ConsoleApp
{
    class Program
    {
        public static void Main(string[] args) => new Program().Start().GetAwaiter().GetResult();

        public async Task Start()
        {
            try
            {
                var key = File.ReadAllText("PolonyBot.key").Trim();

                var polonyBot = new PolonyBot.Core.PolonyBot(key, '%');
                await polonyBot.Start();
                await Task.Delay(-1);
            }
            catch (Exception e)
            {
                System.Console.WriteLine($"Could not run: {e.Message}");
            }
        }
    }
}
