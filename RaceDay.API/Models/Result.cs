using System.ComponentModel.DataAnnotations;

namespace RaceDay.API.Models
{
    public class Result
    {
        [Key]
        public int ResultId { get; set; }

        [Required]
        public int EnrolmentId { get; set; }

        [Required]
        public TimeSpan FinishTime { get; set; }

        [Required]
        public int FinishPosition { get; set; }

        [Required]
        public DateTime RecordedAt { get; set; }

        public Enrolment? Enrolment { get; set; }
    }
}