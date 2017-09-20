using System.Threading.Tasks;
using Polony.NetCore.Core;

namespace Polony.Console
{
    class Program
    {
        public static void Main(string[] args) => new Program().Start().GetAwaiter().GetResult();

        public async Task Start()
        {
            var polonyBot = new PolonyBot("MjI5OTMzNTczMjk2MDk1MjMy.DKRv_g.CtAgnuT2zNt85QRXmu6yhPpGfCo");
            await polonyBot.Start();
            await Task.Delay(-1);
        }
    }
}