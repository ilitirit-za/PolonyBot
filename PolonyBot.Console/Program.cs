using System;
using System.Threading.Tasks;
using Polony.NetCore.Core;
using System.IO;

namespace Polony.Console
{
    class Program
    {
        public static void Main(string[] args) => new Program().Start().GetAwaiter().GetResult();

        public async Task Start()
        {
            try
            { 
                var key = File.ReadAllText("PolonyBot.key").Trim();

                var polonyBot = new PolonyBot(key, '%');
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