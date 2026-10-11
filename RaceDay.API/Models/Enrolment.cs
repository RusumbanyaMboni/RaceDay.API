using System.ComponentModel.DataAnnotations;

namespace RaceDay.API.Models
{
    public class Enrolment
    {
        [Key]
        public int EnrolmentId { get; set; }

        [Required]
        public int ParticipantId { get; set; }

        [Required]
        public int EventId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public DateTime EnrolmentDate { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = string.Empty;

        public User? Participant { get; set; }

        public Event? Event { get; set; }

        public Category? Category { get; set; }

        public Result? Result { get; set; }
    }
}