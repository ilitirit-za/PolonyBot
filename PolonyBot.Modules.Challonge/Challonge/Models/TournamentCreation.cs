using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using Challonge.Abstract;
using Newtonsoft.Json;

namespace Challonge.Models
{
    //More info: http://api.challonge.com/v1/documents/tournaments/create


    public class JsonTournamentCreation : IJsonWrapper<TournamentCreation>
    {
        [JsonProperty("tournament")]
        public TournamentCreation json { get; set; }
    }

    /**
    * public class TournamentCreation
    *
    * Purpose: Model of Challonge's Creation Format. 
    *
    *
    * Several Notes:
    *
    * 1.    I have some enums set that will be converted to their string counterpart
    *       This is to prevent unneeded callbacks to the Database. 
    * 2.    I have added a constructor so default values are set to the properties. I have done so, so in the
    *       the future, if needed, we can extend the web app's functionality 
    **/

    public class TournamentCreation : IJSonWrapper
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        //Prevent converting to Json
        [JsonIgnore]
        public tournament_type type{ get; set; }
        [JsonProperty("tournament_type")]
        public string Tournament_Type { get; set; }
        [JsonProperty("url")]
        public string Url { get; set; }
        public string subdomain { get; set; }
        public string description { get; set; }
        public bool open_signup { get; set; }
        public bool hold_third_place_match { get; set; }
        public decimal pts_for_match_win { get; set; }
        public decimal pts_for_match_tie { get; set; }
        public decimal pts_for_game_win { get; set; }
        public decimal pts_for_game_tie { get; set; }
        public decimal pts_for_bye { get; set; }

        /**
         * From Challonge: 
         * We recommend limiting the number of rounds to less than 
         * two-thirds the number of players. Otherwise, an impossible 
         * pairing situation can be reached and your tournament may end 
         * before the desired number of rounds are played.
         */
        public int swiss_rounds { get; set; }
        //Prevent converting to Json
        [JsonIgnore]
        public ranked _ranked_by { get; set; }
        public string ranked_by { get; set; }
        //==================================
        public decimal rr_pts_for_match_win  { get; set; }
        public decimal rr_pts_for_match_tie { get; set; }
        public decimal rr_pts_for_game_win  { get; set; }
        public decimal rr_pts_for_game_tie { get; set; }
        public bool accept_attachments { get; set; }
        public bool hide_forum { get; set; }
        public bool show_rounds { get; set; }

        //Needs to be done like this because private is a reserved word
        [JsonProperty("private")]
        public bool private_ { get; set; }
        public bool notify_users_when_matches_open { get; set; }
        public bool notify_users_when_the_tournament_ends { get; set; }
        public bool sequential_pairings { get; set; }
        [JsonIgnore]
        public int? Signup_cap { get; set; }
        public string signup_cap { get; set; }
        public DateTime start_at { get; set; }
        public int check_in_duration { get; set; }

        public enum ranked
        {
            match_wins,
            game_wins,
            points_scored,
            points_difference,
            custom
        }

        public enum tournament_type
        {
            single_elimination,
            double_elimiantion,
            round_robin,
            swiss
        }

        /**
        * Although painful, this will alleviate future pains 
        * Sets default to all properties, Except required.
        **/
        /**
         * <summary>
         *  Sets the default values for Tournament Creation
         * </summary>
         * <param name="name">
         *  The tournament Name
         * </param>
         * <param name="signupCap">
         *  Max number of participants. 
         * </param>
         * <param name="url">
         *  The Url of the tournament. Will default to a random Guid.
         * </param>
         * */
        public TournamentCreation(string name, int? signupCap = null, string url = "")
        {
            Name = name;
            type = tournament_type.single_elimination;
            //Will use a Guid as default for the URL. 
            Url = string.IsNullOrEmpty(url) ?  Guid.NewGuid().ToString().Replace("-", "") : url;
            signup_cap = signupCap.ToString() ?? "";
            subdomain = "";
            open_signup = false;
            hold_third_place_match = false;
            //pts_for_match_win = 1;
            pts_for_match_tie = (decimal)0.5;
            pts_for_game_win = 0;
            pts_for_game_tie = 0;
            pts_for_bye = 1;
            _ranked_by = ranked.points_scored;
            rr_pts_for_match_win = 1;
            rr_pts_for_match_tie = (decimal)0.5;
            accept_attachments = false;
            hide_forum = false;
            show_rounds = false;
            private_ = false;
            notify_users_when_matches_open = false;
            notify_users_when_the_tournament_ends = false;
            sequential_pairings = false;
            check_in_duration = 60;
            start_at = DateTime.Today.AddDays(1);
        }

        /**
        *  public void SetDefinedValues()
        *
        *  Purpose: 
        *  <summary>
        *   Gets the current enumeration and assigns them the index.
        *  </summary>
        *  
        *  Paramters: N/A
        *  Return Value: N/A
        **/
        [OnSerializing]
        public void SetDefinedValues(StreamingContext ct)
        {
               Tournament_Type = type.ToString().Replace("_", " ");
               ranked_by = _ranked_by.ToString().Replace("_", " ");
        }

    }


}