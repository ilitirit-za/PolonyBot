using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace PolonyBot.Modules.Challonge.DAL.Models
{
    [Table("Tournament")]
    public class Tournament
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string Game { get; set; }
        public string Status { get; set; }
        public string PlannedStartDate { get; set; }
        public string StartedAt { get; set; }
        public string RegisteredBy { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<Participant> Participants { get; set; }
    }
}
