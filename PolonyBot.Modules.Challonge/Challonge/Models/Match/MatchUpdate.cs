using Challonge.Abstract;
using Newtonsoft.Json;

namespace Challonge.Models.Match
{
    public class JsonMatchUpdate : IJsonWrapper<MatchUpdate>
    {
        [JsonProperty("match")]
        public MatchUpdate json { get; set; }
    }
    public class MatchUpdate
    {
        public int winner_id { get; set; }
        public string scores_csv { get; set; }
        public int player1_votes { get; set; }
        public int player2_votes { get; set; }
    }
}
