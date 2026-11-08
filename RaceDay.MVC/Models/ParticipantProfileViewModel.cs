
using System;
using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    public class ParticipantProfileViewModel
    {
        // ==========================================
        // USER INFORMATION
        // ==========================================

        public int UserId { get; set; }

        public int RoleId { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        [Phone(ErrorMessage = "Enter a valid phone number.")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        public DateTime CreatedAt { get; set; }

        // ==========================================
        // AZURE BLOB STORAGE PROFILE PICTURE
        // ==========================================

        // The blob name is returned by GET /api/profile.
        // Example: users/5/unique-image-name.jpg
        //
        // This is not a public Azure image URL.

        public string? ProfilePictureBlobName { get; set; }

        // Indicates whether the user has a picture.
        // Returned by GET /api/profile.

        public bool HasProfilePicture { get; set; }
    }
}
