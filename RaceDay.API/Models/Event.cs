
using System.ComponentModel.DataAnnotations;

namespace RaceDay.API.Models
{
    public class Event
    {
        [Key]
        public int EventId { get; set; }

        [Required]
        public int OrganiserId { get; set; }

        [Required]
        [MaxLength(150)]
        public string EventName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        [MaxLength(255)]
        public string Location { get; set; } = string.Empty;

        [Required]
        public decimal Distance { get; set; }

        [Required]
        [MaxLength(20)]
        public string EventType { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; }

        // ==========================================
        // AZURE BLOB STORAGE - EVENT IMAGE
        // ==========================================

        // Stores the Azure blob name, not a public URL.
        // Example: events/3/unique-image-name.jpg
        //
        // Nullable so existing events can continue
        // working without an uploaded image.

        [MaxLength(500)]
        public string? EventImageBlobName { get; set; }

        // ==========================================
        // NAVIGATION PROPERTIES
        // ==========================================

        public User? Organiser { get; set; }

        public ICollection<Category> Categories { get; set; }
            = new List<Category>();

        public ICollection<Enrolment> Enrolments { get; set; }
            = new List<Enrolment>();
    }
}
