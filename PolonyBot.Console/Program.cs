using System.Threading.Tasks;
using Polony.NetCore.Core;

namespace Polony.Console
{
    class Program
    {
        public static void Main(string[] args) => new Program().Start().GetAwaiter().GetResult();

        public async Task Start()
        {
            var polonyBot = new PolonyBot("MjI5OTMzNTczMjk2MDk1MjMy.Ct1XmA.0X1Y8PnTrtEDF6nlHRXnqLVNmkI");
            await polonyBot.Start();
            await Task.Delay(-1);
        }
    }
}