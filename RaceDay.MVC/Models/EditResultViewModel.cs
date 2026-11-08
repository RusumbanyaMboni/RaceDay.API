
using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    public class EditResultViewModel
    {
        public int ResultId { get; set; }

        public int EnrolmentId { get; set; }

        public int EventId { get; set; }

        public string EventName { get; set; } = string.Empty;

        public string ParticipantName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Finish time is required.")]
        [Display(Name = "Finish Time (HH:MM:SS)")]
        public string FinishTime { get; set; } = string.Empty;

        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "Finish position must be greater than zero.")]
        [Display(Name = "Finish Position")]
        public int FinishPosition { get; set; }
    }
}
