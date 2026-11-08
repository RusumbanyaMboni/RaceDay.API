
using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    public class CategoryViewModel
    {
        public int CategoryId { get; set; }

        public int EventId { get; set; }

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(100)]
        [Display(Name = "Category Name")]
        public string CategoryName { get; set; } = string.Empty;

        [Range(0, 120,
            ErrorMessage = "Minimum age must be between 0 and 120.")]
        [Display(Name = "Minimum Age")]
        public int? MinimumAge { get; set; }

        [Range(0, 120,
            ErrorMessage = "Maximum age must be between 0 and 120.")]
        [Display(Name = "Maximum Age")]
        public int? MaximumAge { get; set; }

        [Required(ErrorMessage = "Distance is required.")]
        [Range(
            typeof(decimal),
            "0.01",
            "10000",
            ParseLimitsInInvariantCulture = true,
            ErrorMessage = "Distance must be between 0.01 and 10000 km.")]
        [Display(Name = "Distance (km)")]
        public decimal Distance { get; set; }

        public bool IsAgeRangeValid()
        {
            if (MinimumAge.HasValue &&
                MaximumAge.HasValue &&
                MinimumAge.Value > MaximumAge.Value)
            {
                return false;
            }

            return true;
        }
    }
}
