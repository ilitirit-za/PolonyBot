using System;
using System.Collections.Generic;
using Challonge.Models;
using Challonge.Models.Match;
using Result = Challonge.Models.Result;

namespace Challonge.Abstract
{
    public interface IChallonge
    {
        Result Result { get; }
        IList<ChallongeTournament> AllTournaments(string apiMethod = "tournaments");
        ChallongeTournament CreateTournament(TournamentCreation t);
        ChallongeTournament DeleteTournament(string tournamentUrl);
        ChallongeTournament ShowTournament(string tournamentUrl);
        ChallongeTournament UpdateTournament(string tournamentUrl, TournamentCreation t);
        ChallongeTournament CheckInTournament(string tournamentUrl);
        Tuple<ChallongeTournament, IEnumerable<MatchChallonge>> StartTournament(string tournamentUrl, bool includeMatches = true);
        ChallongeTournament FinalizeTournament(string tournamentUrl, bool includeParticipants = false);
        IEnumerable<ChallongeParticipant> AllParticipants(string tournamentUrl);
        ChallongeParticipant CreateParticipant(string tournamentUrl, CreateChallongeParticipant participant);
        ChallongeParticipant ShowParticipant(string tournamentUrl, int participantID);
        //IEnumerable<ChallongeParticipant> BulkCreateParticipants(string tournamentUrl, IEnumerable<CreateChallongeParticipant> participantList);
        ChallongeParticipant UpdateParticipant(string tournamentUrl, int participantID, ChallongeParticipant participant);
        ChallongeParticipant CheckInParticipant(string tournamentUrl, int participantID);
        ChallongeParticipant UndoCheckInParticipant(string tournamentUrl, int participantID);
        ChallongeParticipant DestroyParticipant(string tournamentUrl, int participantID);
        IEnumerable<ChallongeParticipant> RandomizeParticipant(string tournamentUrl);
        IEnumerable<MatchChallonge> AllMatches(string tournamentUrl, int? participantId = null);
        MatchChallonge ShowMatch(string tournamentUrl, int matchID);
        MatchChallonge UpdateMatch(string tournamentUrl, int matchID, MatchUpdate mu);
        IEnumerable<MatchAttachmentChallonge> AllMatchAttachments(string tournamentUrl, int matchID);
        MatchAttachmentChallonge CreateAttachment(string tournamentUrl, int matchID, CreateMatchAttachmentChallonge attachment, string filePath = "");
        MatchAttachmentChallonge ShowAttachment(string tournamentUrl, int matchID, int attachmentID);
        MatchAttachmentChallonge UpdateAttachment(string tournamentUrl, int matchID, MatchAttachmentChallonge attachment);
        MatchAttachmentChallonge DeleteAttachment(string tournamentUrl, int matchID, int attachmentID);

    }
}