
using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    public class EventViewModel
    {
        public int EventId { get; set; }

        public int OrganiserId { get; set; }

        [Required(ErrorMessage = "Event name is required.")]
        [StringLength(150)]
        [Display(Name = "Event Name")]
        public string EventName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Event date is required.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Event Date")]
        public DateTime EventDate { get; set; }

        [Required(ErrorMessage = "Location is required.")]
        [StringLength(255)]
        public string Location { get; set; } = string.Empty;

        [Range(
            typeof(decimal),
            "0.01",
            "10000",
            ParseLimitsInInvariantCulture = true,
            ErrorMessage = "Distance must be between 0.01 and 10000 km.")]
        [Display(Name = "Distance (km)")]
        public decimal Distance { get; set; }

        [Required(ErrorMessage = "Please select an event type.")]
        [StringLength(20)]
        [Display(Name = "Event Type")]
        public string EventType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // ==========================================
        // AZURE BLOB STORAGE - EVENT IMAGE
        // ==========================================

        // Internal blob reference from RaceDay.API.
        // Example: events/10/unique-image-name.jpg
        // This is NOT a public Azure URL.

        public string? EventImageBlobName { get; set; }

        // Indicates whether an event image exists.
        // Returned by GET /api/Event and GET /api/Event/{id}.

        public bool HasEventImage { get; set; }
    }
}
