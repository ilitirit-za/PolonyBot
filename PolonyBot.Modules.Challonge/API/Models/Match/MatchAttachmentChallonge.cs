using System;
using Challonge.Abstract;
using Newtonsoft.Json;

namespace Challonge.Models.Match
{
    public class JsonMatchAttachmentChallonge : IJsonWrapper<MatchAttachmentChallonge>
    {
        [JsonProperty("match_attachment")]
        public MatchAttachmentChallonge json { get; set; }
    }


    public class MatchAttachmentChallonge: IJSonWrapper
    {
        public int id { get; set; }
        public int match_id { get; set; }
        public int user_id { get; set; }
        public string description { get; set; }
        public string url { get; set; }
        public string original_file_name { get; set; }
        public DateTime? created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string asset_file_name { get; set; }
        public string asset_content_type { get; set; }
        public int? asset_file_size { get; set; }
        public string asset_url { get; set; }
    }



    public class JsonCreateMatchAttachmentChallonge : IJsonWrapper<CreateMatchAttachmentChallonge>
    {
        [JsonProperty("match_attachment")]
        public CreateMatchAttachmentChallonge json { get; set; }
    }
    public class CreateMatchAttachmentChallonge : IJSonWrapper
    {
        public string description { get; set; }
        public string url { get; set; }
    }

}