using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Polony.Domain.Danisen.Model
{
    public class Rank
    {
        public int RankId { get; set; }

        public int RankSetId { get; set; }

        public string Name { get; set; }

        public int Level { get; set; }

        public int PromotionScore { get; set; }

        public int DemotionScore { get; set; }

        public int UpperChallengeLimit { get; set; }

        public int LowerChallengeLimit { get; set; }

        public bool Unlocked { get; set; }
    }
}
