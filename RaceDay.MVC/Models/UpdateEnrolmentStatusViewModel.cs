
using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    public class UpdateEnrolmentStatusViewModel
    {
        public int EnrolmentId { get; set; }

        public int EventId { get; set; }

        public int ParticipantId { get; set; }

        public string? ParticipantEmail { get; set; }

        public string? EventName { get; set; }

        public string? CategoryName { get; set; }

        public string CurrentStatus { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a status.")]
        [Display(Name = "New Status")]
        public string NewStatus { get; set; } = string.Empty;
    }
}
