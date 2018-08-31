using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Challonge.Abstract;
using Challonge.Models;
using Challonge.Models.Match;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Challonge.Infrastructure
{
    public class ChallongeApi : IChallonge
    {

        //Got rid of this. Will be outside this class. private IConfigManager WebConfig;

        private string Username { get; set; }
        private string Key { get; set; }

        private string Url { get; set; }

        //public Result<Challonge> Challonge_ { get; private set; } //The private set is overkill, but just in case let's leave it there...

        //Gets the information from the web.config file
        public ChallongeApi(string username, string apikey)
        {
            //Username = username;
            //Key = apikey;

            Username = "ilitirit";
            Key = "ezntUopEWcSfXJuGv3VqyrKLyPcC2LlmNJYT7Ane";

//            Username = WebConfig.GetAppSetting("challongeUserName");
//          Key = WebConfig.GetAppSetting("challongeKey");
            Url = "https://" + Username + ":" + Key + "@api.challonge.com/v1/";

        }

        /**
         * public JsonResult Load(string apiMethod)
         * 
         * http://api.challonge.com/v1
         * 
         * Purpose: <summary>This is the main method for GET requests. This will load the 
         *                   Challonge page + whatever API Method you request. Leaving blank 
         *                   will display all tournaments
         *           </summary>
         * 
         * Parameters: 
         * apiMethod    =>  optional. API Methods from Challonge's REST API. 
         * 
         * Return value:
         * JSON => Whatever it is supposed to return.
         * 
         * */
        private string Load(string apiMethod = "", string query ="")
        {
            try
            {
                var credentials = new NetworkCredential(Username, Key);
                using (var handler = new HttpClientHandler { Credentials = credentials})
                using (var httpClient = new HttpClient(handler))
                {
                    string URL = Url + apiMethod + ".json"+query;
                    
                    //httpClient.Encoding = System.Text.Encoding.UTF8;

                    string toReturn = httpClient.GetStringAsync(URL).Result;

                    // SetResultOk();
                    return toReturn;
                }
            }

            catch (WebException ex)
            {
                // SetResultError(ex.Message);
                return "";
            }
        }

        private async Task<Result<string>> LoadAsync(string apiMethod = "", string query = "")
        {
            try
            {
                var credentials = new NetworkCredential(Username, Key);
                using (var handler = new HttpClientHandler { Credentials = credentials })
                using (var httpClient = new HttpClient(handler))
                {
                    var url = Url + apiMethod + ".json" + query;
                    var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    { 
                        var result = await response.Content.ReadAsStringAsync();
                        return new Success<string>(result);
                    }

                    return new Failure<string>(response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                return new Failure<string>(ex);
            }
        }

        /**
        *  private string Send(ChallongeTournament t)
        * 
        *  Purpose:<summary>Main Method for Communicating POST/DELETE/ commands to the Challonge API for creating
        *                   /deleting a PGTournament</summary>
        **/
        private string Send<T>(T data, string method = "POST", string parameters = "", bool sendJson = true, string headers ="")
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url + "tournaments" + parameters + ".json"+headers);
            request.Method = method;
            request.ContentType = "application/json";
            request.Credentials = new NetworkCredential(Username, Key);
            return ProcessRequest(data, sendJson, request);

        }
        /*
        private string Send<T>(T data, string method = "POST", string parameters = "", bool sendJson = true)
        {
            using (var httpClient = new HttpClient())
            {

            }
        }
        */

        private string ProcessRequest<T>(T data, bool sendJson, HttpWebRequest request)
        {
            try
            {
                
                using (var streamWriter = new StreamWriter(request.GetRequestStreamAsync().Result))
                {
                    //Will Send the Json if needed!
                    if (sendJson)
                    {
                        //Converts to a Challonge's readable object
                        string json = JsonConvert.SerializeObject(data);
                        streamWriter.Write(json);
                        streamWriter.Flush();
                    }
                }


                WebResponse response = request.GetResponseAsync().Result;
                using (Stream responseStream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(responseStream, Encoding.UTF8);
                    // SetResultOk();
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                WebResponse errorResponse = ex.Response;
                using (Stream responseStream = errorResponse.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(responseStream, Encoding.GetEncoding("utf-8"));
                    String errorText = reader.ReadToEnd();
                    // log errorText
                    // SetResultError(ex.Message);
                    return "";
                }
            }
                //In case it does not connect to the server
            
        }

        /**
        * private IList<T> ProcessResults<T>(JArray data)
        *
        * Purpose: <summary>Helper Method for Loading Tournaments and or users and processing them each by one</summary>
        **/
        private IList<T> ProcessResults<T, U, F>(F data) where T :IJSonWrapper where U : IJsonWrapper<T> where F: IEnumerable<JToken>
        {
            IList<JToken> results = (typeof(F) == typeof(IList<JToken>)) ? data.ToList() :  data.Children().ToList();

            var Results = new List<T>();
            foreach (var result in results)
            {
                    T tR = JsonConvert.DeserializeObject<U>(result.ToString()).json;
                    Results.Add(tR);
            }
            return Results;
        }

        private IList<T> ProcessNonWrappedResults<T, F>(F data) where T : IJSonWrapper where F : IEnumerable<JToken>
        {
            IList<JToken> results = (typeof(F) == typeof(IList<JToken>)) ? data.ToList() : data.Children().ToList();

            var Results = new List<T>();
            foreach (var result in results)
            {
                T tR = JsonConvert.DeserializeObject<T>(result.First.ToString());
                Results.Add(tR);
            }
            return Results;
        }


        /***
        *
        *  private T ProcessJson<T, U>(U data) where T : IWrapper where U : IJsonWrapper<T>
        *
        *  Purpose: <summary>Prevents a System.NullReferenceException from appearing when calling a property on a null object.</summary>
        
           Explanation: When something fails by connecting with the Challonge API, I have designed it to return an empty string. 
                        When the JSON parser  kicks in, it will return NULL. THe problem is that there are many scenarios in which I
                        return what the JSON parser's result + the json property of the object (which is usually the property
                        of the returning class). If the JSON parser's result was null, when I get the json property of the class I'd get a
                        System.NullReferenceException.

            Parameters: 
            T:      It's the main method returning class. 
            U:      The main method's Json processing class. 

            Return Value:
                    Null in case the U parameter is null
                    data.json   => the correct T value of the U parameter
        *
        **/

        private T ProcessJson<T, U>(U data) where T : IJSonWrapper where U : IJsonWrapper<T>
        {
            if (data == null)
            {
                return default(T);
            }
            else
            {
                return data.json;
            }

        }

        //============================ Tournaments ============================//

        /**
         * public IList<ChallongeTournament> LoadTournament(string apiMethod ="")
         * 
         * http://www.newtonsoft.com/json/help/html/SerializingJSONFragments.htm
         * 
         * Purpose: <summary>This is the main method for extracting the Tournaments. This will list all tournaments. The apiMethod will 
         *           </summary>
         * 
         * Parameters: 
         * apiMethod    =>  optional. API Methods from Challonge's REST API. 
         * 
         * Return value:
         * IList<ChallongeTournament> => The formatted JSOn in a list of ChallongeTournament so I can manipulate later on.
         * 
         * */
        public IList<ChallongeTournament> AllTournaments(string apiMethod = "tournaments")
        {
            JArray tournaments = JArray.Parse(Load(apiMethod));

            IList<ChallongeTournament> tournamentResults = ProcessResults<ChallongeTournament,JsonChallongeTournament, JArray>(tournaments);

            return tournamentResults;
        }


        /**
        * public JsonChallongeTournament CreateTournament(ChallongeTournament t)
        *
        * Purpose: <summary>Creates a PGTournament using Challonge's API.</summary>
        * 
        * Parameters:
        * TournamentCreation    The TournamentCreation object to be passed to the Challonge's Website
        * 
        * Return Value:
        * JsonChallongeTournament   The processed object returned by the API
        * null                      In case a problem occurred, it will be logged by the API
        */
        public ChallongeTournament CreateTournament(TournamentCreation t)
        {
            //Creates the wrapper for the send method
            var tournament = new JsonTournamentCreation();
                tournament.json = t;
                return ProcessJson<ChallongeTournament,JsonChallongeTournament>(
                    JsonConvert.DeserializeObject<JsonChallongeTournament>(
                        Send(tournament)
                        )
                     );
        }

        /**
        * public JsonChallongeTournament DeleteTournament(string tournamentUrl)
        *
        * Purpose: <summary>Deletes a PGTournament by providing the tournament's URL</summary>
        *
        * Return Value:
        * The JsonChallongeTournament object that contains the deleted tournament's info. 
        **/

        public ChallongeTournament DeleteTournament(string tournamentUrl)
        {
            //Sends an empty parameter instead of null due to the generic property
            return ProcessJson<ChallongeTournament,JsonChallongeTournament>(
                    JsonConvert.DeserializeObject<JsonChallongeTournament>(Send("", "DELETE", "/" + tournamentUrl, false)
                    )
                  );
            
        }

        public async Task<Result<ChallongeTournament>> ShowTournamentAsync(string tournamentUrl)
        {
            var jsonResult = await LoadAsync("tournaments/" + tournamentUrl);
            if (!jsonResult.Succeeded)
                return new Failure<ChallongeTournament>(jsonResult.Message);
            
            var jsonChallongeTournament = JsonConvert.DeserializeObject<JsonChallongeTournament>(jsonResult.Value);

            return new Success<ChallongeTournament>(ProcessJson<ChallongeTournament, JsonChallongeTournament>(jsonChallongeTournament));
        }

        public ChallongeTournament UpdateTournament(string tournamentUrl, TournamentCreation t)
        {
            var tournament = new JsonTournamentCreation();
            tournament.json = t;
            return ProcessJson<ChallongeTournament, JsonChallongeTournament>(
                JsonConvert.DeserializeObject<JsonChallongeTournament>(
                    Send(tournament, "PUT", "/" + tournamentUrl)
                    )
                 );
        }

        public ChallongeTournament CheckInTournament(string tournamentUrl)
        {
            return ProcessJson<ChallongeTournament, JsonChallongeTournament>(
                   JsonConvert.DeserializeObject<JsonChallongeTournament>(
                       Send("", "POST", "/" + tournamentUrl+"/process_check_ins", false)
                   )
                 );
        }

        public ChallongeTournament AbortCheckInTournament(string tournamentUrl)
        {
            return ProcessJson<ChallongeTournament, JsonChallongeTournament>(
                   JsonConvert.DeserializeObject<JsonChallongeTournament>(
                       Send("", "POST", "/" + tournamentUrl + "/abort_check_in", false)
                   )
                 );
        }

        public Tuple<ChallongeTournament,IEnumerable<MatchChallonge>> StartTournament(string tournamentUrl, bool includeMatches = true)
        {

           var result = (includeMatches) ? Send("", "POST", "/" + tournamentUrl + "/start", false, "?include_matches=1") : Send("", "POST", "/" + tournamentUrl + "/start", false);
            if(string.IsNullOrEmpty(result))
            {
                //This means it failed
                return null;
            }

            IList<JToken> result_parse = JObject.Parse(result)["tournament"]["matches"].ToList();
            IEnumerable<MatchChallonge> matches = (includeMatches) ? ProcessResults<MatchChallonge, JsonMatchChallonge, IList<JToken>>(result_parse) : Enumerable.Empty<MatchChallonge>();
            ChallongeTournament tournament = ProcessJson<ChallongeTournament, JsonChallongeTournament>(JsonConvert.DeserializeObject<JsonChallongeTournament>(result));
            return new Tuple<ChallongeTournament,IEnumerable<MatchChallonge>>(tournament,matches);
        }

        public ChallongeTournament FinalizeTournament(string tournamentUrl, bool includeParticipants = false)
        {
            string query = includeParticipants ? "?include_participants=1" : "";

            return ProcessJson<ChallongeTournament, JsonChallongeTournament>(
                   JsonConvert.DeserializeObject<JsonChallongeTournament>(
                       Send("", "POST", "/" + tournamentUrl + "/finalize", false, query)
                   )
                 );
        }
        public ChallongeTournament ResetTournament(string tournamentUrl)
        {
            return ProcessJson<ChallongeTournament, JsonChallongeTournament>(
                   JsonConvert.DeserializeObject<JsonChallongeTournament>(
                       Send("", "POST", "/" + tournamentUrl + "/reset", false)
                   )
                 );
        }



        //============================ Participants ============================//


        public IEnumerable<ChallongeParticipant> AllParticipants(string tournamentUrl)
        {
            JArray participants = JArray.Parse(Load("tournaments/" + tournamentUrl + "/participants"));
            var result = participants.ToString();
            IList<ChallongeParticipant> participantResults = ProcessNonWrappedResults<ChallongeParticipant, JArray>(participants);
            return participantResults;
            //return (JsonConvert.DeserializeObject<IList<ChallongeParticipant>>(participantResults)).participant;
        }

        public async Task<Result<IEnumerable<ChallongeParticipant>>> AllParticipantsAsync(string tournamentUrl)
        {
            var jsonResult = await LoadAsync("tournaments/" + tournamentUrl + "/participants");
            if (!jsonResult)
                return new Failure<IEnumerable<ChallongeParticipant>>(jsonResult.Message);

            var participants = JArray.Parse(jsonResult.Value);
            ;
            return new Success<IEnumerable<ChallongeParticipant>>(ProcessNonWrappedResults<ChallongeParticipant, JArray>(participants));
        }

        public ChallongeParticipant CreateParticipant(string tournamentUrl, CreateChallongeParticipant participant)
        {
            var jsonParticipant = new JsonCreateChallongeParticipant(){json = participant};
            var processedJson = JsonConvert.DeserializeObject<JsonChallongeParticipant>(Send(jsonParticipant, "POST", "/" + tournamentUrl + "/participants"));
            return ProcessJson<ChallongeParticipant,JsonChallongeParticipant>(processedJson);
            
        }

        public ChallongeParticipant ShowParticipant(string tournamentUrl, int participantID)
        {
            return ProcessJson<ChallongeParticipant,JsonChallongeParticipant>(
                JsonConvert.DeserializeObject<JsonChallongeParticipant>(Load("tournaments/" + tournamentUrl + "/participants/" + participantID.ToString()))
                );
        }

      /* NEEDS WORK - BulkCreateParticipants
        public IEnumerable<ChallongeParticipant> BulkCreateParticipants(string tournamentUrl, IEnumerable<CreateChallongeParticipant> participantList)
        {
            //Creates a JsonCreateChallongeParticipant list for sending it!
            IEnumerable<JsonCreateChallongeParticipant> jsonList = participantList.Select(x => new JsonCreateChallongeParticipant() { json = x });
            
            //Deserializes the 
            var deserialize = JsonConvert.DeserializeObject<IEnumerable<CreateChallongeParticipant>>(Send(participantList, "POST", "/" + tournamentUrl + "/participants/bulk_add"));

            return new List<ChallongeParticipant>(); //return deserialize.Select(x => ProcessJson<ChallongeParticipant, JsonChallongeParticipant>(x)).AsEnumerable();
        }
        */

        public ChallongeParticipant UpdateParticipant(string tournamentUrl, int participantID, ChallongeParticipant participant)
        {
            var jsonParticipant = new JsonChallongeParticipant() { json = participant };

            return ProcessJson<ChallongeParticipant, JsonChallongeParticipant>(JsonConvert.DeserializeObject<JsonChallongeParticipant>(Send(jsonParticipant,"PUT",
                                            "/" + tournamentUrl + "/participants/" + participantID.ToString())));
        }
        
        public ChallongeParticipant CheckInParticipant(string tournamentUrl, int participantID)
        {
            return ProcessJson<ChallongeParticipant, JsonChallongeParticipant>(
                JsonConvert.DeserializeObject<JsonChallongeParticipant>(
                        Send("", "POST", "/" + tournamentUrl + "/participants/" + participantID + "/check_in", false)
                        )
                );
        }

        public ChallongeParticipant UndoCheckInParticipant(string tournamentUrl, int participantID)
        {
            return ProcessJson<ChallongeParticipant, JsonChallongeParticipant>(
                JsonConvert.DeserializeObject<JsonChallongeParticipant>(
                        Send("", "POST","/" + tournamentUrl + "/participants/" + participantID + "/undo_check_in", false)
                        )
                );
        }

        public ChallongeParticipant DestroyParticipant(string tournamentUrl, int participantID)
        {
            return ProcessJson<ChallongeParticipant, JsonChallongeParticipant>(
                    JsonConvert.DeserializeObject<JsonChallongeParticipant>(
                        Send("", "DELETE", "/" + tournamentUrl+"/participants/"+participantID.ToString(), false)
                        )
                    );
        }

        public IEnumerable<ChallongeParticipant> RandomizeParticipant(string tournamentUrl)
        {
            IEnumerable<JsonChallongeParticipant> jsonList = JsonConvert.DeserializeObject<IEnumerable<JsonChallongeParticipant>>(
                                                                Send("", "POST", "/" + tournamentUrl + "/participants/randomize", false));
           return jsonList.Select(x => ProcessJson<ChallongeParticipant, JsonChallongeParticipant>(x)).AsEnumerable();                                                   
        }


        //============================ Matches ============================//

        public IEnumerable<MatchChallonge> AllMatches(string tournamentUrl, int? participantId = null)
        {
            string query = participantId == null ? "" : "?participant_id=" + participantId;

            IEnumerable<JsonMatchChallonge> jsonList = JsonConvert.DeserializeObject<IEnumerable<JsonMatchChallonge>>(
                                                               Load("tournaments/" + tournamentUrl + "/matches",query));
            return jsonList.Select(x => ProcessJson<MatchChallonge, JsonMatchChallonge>(x)).AsEnumerable();
        }


        public MatchChallonge ShowMatch(string tournamentUrl, int matchID)
        {
            return ProcessJson<MatchChallonge, JsonMatchChallonge>(
               JsonConvert.DeserializeObject<JsonMatchChallonge>(Load("tournaments/" + tournamentUrl + "/matches/" + matchID.ToString()))
               );
        }

        public MatchChallonge UpdateMatch(string tournamentUrl, int matchID, MatchUpdate mu)
        {
            //I return 
            var jsonMatch = new JsonMatchUpdate() { json = mu };

            return ProcessJson<MatchChallonge, JsonMatchChallonge>(
               JsonConvert.DeserializeObject<JsonMatchChallonge>(Send(jsonMatch, "PUT","/" + tournamentUrl + "/matches/" + matchID.ToString()))
               );
        }


        //============================ Attachments ============================//

        /**
        * According to: http://stackoverflow.com/a/4083908/1057052
        *
        * I shall first send the data, and then the file (if used);
        **/
        public MatchAttachmentChallonge CreateAttachment(string tournamentUrl, int matchID, CreateMatchAttachmentChallonge attachment, string filePath ="")
        {
            //Creates the Match:
            var jsonMatch = new JsonCreateMatchAttachmentChallonge() { json = attachment };
            var result =  ProcessJson<MatchAttachmentChallonge, JsonMatchAttachmentChallonge>(
               JsonConvert.DeserializeObject<JsonMatchAttachmentChallonge>(
                   Send(jsonMatch, "POST", "/" + tournamentUrl + "/matches/" + matchID.ToString()+"/attachments"))
               );
            
            //Receives the URL and then uploads the file
            /*
            Currently I'm not supporting File based upload for attachments! 

            if (!string.IsNullOrEmpty(filePath) && result != null)
            {
              result =   ProcessJson<MatchAttachmentChallonge, JsonMatchAttachmentChallonge>(
                 JsonConvert.DeserializeObject<JsonMatchAttachmentChallonge>(
                     Load(tournamentUrl + "/matches/" + matchID + "/attachments/" + result.id, "PUT", filePath))
                 );      
            }*/

            return result;
        }

        public IEnumerable<MatchAttachmentChallonge> AllMatchAttachments(string tournamentUrl, int matchID)
        {
            IEnumerable<JsonMatchAttachmentChallonge> jsonList = JsonConvert.DeserializeObject<IEnumerable<JsonMatchAttachmentChallonge>>(
                                                              Load("tournaments/" + tournamentUrl + "/matches/" + matchID.ToString() + "/attachments"));
            return jsonList.Select(x => ProcessJson<MatchAttachmentChallonge, JsonMatchAttachmentChallonge>(x)).AsEnumerable();
        }

        public MatchAttachmentChallonge ShowAttachment(string tournamentUrl, int matchID, int attachmentID)
        {
            return ProcessJson<MatchAttachmentChallonge, JsonMatchAttachmentChallonge>(
              JsonConvert.DeserializeObject<JsonMatchAttachmentChallonge>(
                  Load("tournaments/" + tournamentUrl + "/matches/" + matchID.ToString()+"/attachments/"+attachmentID))
              );
        }

        public MatchAttachmentChallonge UpdateAttachment(string tournamentUrl, int matchID, MatchAttachmentChallonge attachment)
        {
            return new MatchAttachmentChallonge();
        }

        public MatchAttachmentChallonge DeleteAttachment(string tournamentUrl, int matchID, int attachmentID)
        {
            return ProcessJson<MatchAttachmentChallonge, JsonMatchAttachmentChallonge>(
              JsonConvert.DeserializeObject<JsonMatchAttachmentChallonge>(
                  Send("","DELETE","/" + tournamentUrl + "/matches/" + matchID.ToString() + "/attachments/" + attachmentID,false))
              );
        }


    }
}



//MIT License (2015- 2016)
//Copryight Jose Asilis - (c) 2015. Last Modified Tuesday August 4th, 2015.
//Modification - April 5th, 2016. 
/*
 * Refactored a little bit the code. 
 * Removed Result<T> from the pipeline. 
 * 
 * Removed the IConfigManager so everybody can instantiate the class with just the strings.
 * 
 * */
 /**
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR 
 * PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
 * ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * */
