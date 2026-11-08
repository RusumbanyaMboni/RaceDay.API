
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    public class ParticipantEventDetailsViewModel
    {
        public EventViewModel Event { get; set; }
            = new EventViewModel();

        public List<CategoryViewModel> Categories { get; set; }
            = new List<CategoryViewModel>();

        [Required(ErrorMessage = "Please select a category.")]
        [Range(1, int.MaxValue,
            ErrorMessage = "Please select a valid category.")]
        [Display(Name = "Race Category")]
        public int SelectedCategoryId { get; set; }
    }
}
