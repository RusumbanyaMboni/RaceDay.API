
using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    public class ChangeCategoryViewModel
    {
        public int EnrolmentId { get; set; }

        public int EventId { get; set; }

        public string EventName { get; set; } = string.Empty;

        public int CurrentCategoryId { get; set; }

        public string CurrentCategoryName { get; set; } = string.Empty;

        [Range(1, int.MaxValue,
            ErrorMessage = "Please select a valid category.")]
        [Display(Name = "New Category")]
        public int SelectedCategoryId { get; set; }

        public List<CategoryViewModel> Categories { get; set; }
            = new List<CategoryViewModel>();
    }
}
