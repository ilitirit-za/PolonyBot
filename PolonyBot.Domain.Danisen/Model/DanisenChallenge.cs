using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Polony.Domain.Danisen.Model
{
    public class DanisenChallenge
    {
        public int ChallengeId { get; set; }
        public DanisenLeague DanisenLeague { get; set; }
        public DanisenRegistration PlayerOne { get; set; }
        public DanisenRegistration PlayerTwo { get; set; }
        public string ChallengeStatus { get; set; }
        public Player Winner { get; set; }
        public DateTime ChallengeIssued { get; set; }
    }
}
