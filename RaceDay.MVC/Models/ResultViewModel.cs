
using System;
using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    public class ResultViewModel
    {
        public int ResultId { get; set; }

        public int EnrolmentId { get; set; }

        public int ParticipantId { get; set; }

        [Display(Name = "Participant")]
        public string? ParticipantName { get; set; }

        public int EventId { get; set; }

        [Display(Name = "Event")]
        public string? EventName { get; set; }

        public int CategoryId { get; set; }

        [Display(Name = "Category")]
        public string? CategoryName { get; set; }

        [Required]
        [Display(Name = "Finish Time")]
        public TimeSpan FinishTime { get; set; }

        [Required]
        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "Finishing position must be greater than zero.")]
        [Display(Name = "Finish Position")]
        public int FinishPosition { get; set; }

        [Display(Name = "Recorded At")]
        public DateTime RecordedAt { get; set; }
    }
}
