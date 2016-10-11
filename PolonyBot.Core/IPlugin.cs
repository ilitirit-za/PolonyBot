using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Polony.Core
{
    public interface IPlugin
    {
        string Name { get; }
        string Description { get; }
        string Author { get; }

        void Start();
        void Stop();
        void ModuleTick();
    }
}
