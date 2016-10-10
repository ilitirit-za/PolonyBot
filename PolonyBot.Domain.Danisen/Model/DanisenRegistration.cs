using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Polony.Domain.Danisen.Model
{
    public class DanisenRegistration
    {
        public DanisenLeague DanisenLeague { get; set; }
        public Player Player { get; set; }
        public Rank Rank { get; set; }
        public bool Enabled { get; set; }
        public string Character { get; set; }
        public int Points { get; set; }
        public int RegistrationCode { get; set; }
    }
}
