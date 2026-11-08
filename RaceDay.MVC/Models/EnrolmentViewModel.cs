
using System;
using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    public class EnrolmentViewModel
    {
        public int EnrolmentId { get; set; }

        public int ParticipantId { get; set; }

        public int EventId { get; set; }

        [Display(Name = "Event Name")]
        public string? EventName { get; set; }

        public int CategoryId { get; set; }

        [Display(Name = "Category")]
        public string? CategoryName { get; set; }

        [Display(Name = "Registration Date")]
        public DateTime EnrolmentDate { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;
    }
}
