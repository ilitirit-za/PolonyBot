using System.Collections.Generic;
using Challonge.Abstract;
using Newtonsoft.Json;

namespace Challonge.Models
{

    //For json according to:http://stackoverflow.com/questions/26096259/convert-json-string-to-strongly-typed-class-object-in-c-sharp

    //This must be done like this to support the current flow of JSON.
    public class JsonChallongeTournament : IJsonWrapper<ChallongeTournament>
    {
        [JsonProperty("tournament")]
        public ChallongeTournament json { get; set; }
    }

    public class ChallongeTournament : IJSonWrapper
    {
        public string accept_attachments { get; set; }
        public string allow_participant_match_reporting { get; set; }
        public string anonymous_voting { get; set; }
        public string category { get; set; }
        public string check_in_duration { get; set; }
        public string completed_at { get; set; }
        public string created_at { get; set; }
        public string created_by_api { get; set; }
        public string credit_capped { get; set; }
        public string description { get; set; }
        public string game_id { get; set; }
        public string group_stages_enabled { get; set; }
        public string hide_forum { get; set; }
        public string hide_seeds { get; set; }
        public string hold_third_place_match { get; set; }
        public string id { get; set; }
        public string max_predictions_per_user { get; set; }
        public string name { get; set; }
        public string notify_users_when_matches_open { get; set; }
        public string notify_users_when_the_tournament_ends { get; set; }
        public string open_signup { get; set; }
        public string participants_count { get; set; }
        public string prediction_method { get; set; }
        public string predictions_opened_at { get; set; }
        [JsonProperty("private")]
        public string private_ { get; set; }
        public string progress_meter { get; set; }
        public string pts_for_bye { get; set; }
        public string pts_for_game_tie { get; set; }
        public string pts_for_game_win { get; set; }
        public string pts_for_match_tie { get; set; }
        public string pts_for_match_win { get; set; }
        public string quick_advance { get; set; }
        public string ranked_by { get; set; }
        public string require_score_agreement { get; set; }
        public string rr_pts_for_game_tie { get; set; }
        public string rr_pts_for_game_win { get; set; }
        public string rr_pts_for_match_tie { get; set; }
        public string rr_pts_for_match_win { get; set; }
        public string sequential_pairings { get; set; }
        public string show_rounds { get; set; }
        public string signup_cap { get; set; }
        public string start_at { get; set; }
        public string started_at { get; set; }
        public string started_checking_in_at { get; set; }
        public string state { get; set; }
        public string swiss_rounds { get; set; }
        public string teams { get; set; }
        public string[] tie_breaks { get; set; }
        public string tournament_type { get; set; }
        public string updated_at { get; set; }
        public string url { get; set; }
        public string description_source { get; set; }
        public string subdomain { get; set; }
        public string full_challonge_url { get; set; }
        public string live_image_url { get; set; }
        public string sign_up_url { get; set; }
        public string review_before_finalizing { get; set; }
        public string accepting_predictions { get; set; }
        public string participants_locked { get; set; }
        public string game_name { get; set; }
        public string participants_swappable { get; set; }
        public string team_convertable { get; set; }
        public string group_stages_were_started { get; set; }

        public IEnumerable<JsonChallongeParticipant> participants { get; set; }
           
    }

    
}