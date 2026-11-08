
namespace RaceDay.MVC.Models
{
    public class OrganiserDashboardViewModel
    {
        public string FirstName { get; set; } = "Organiser";

        public int TotalEvents { get; set; }

        public int TotalEnrolments { get; set; }

        public int ConfirmedEnrolments { get; set; }

        public int TotalResults { get; set; }

        public bool StatisticsAvailable { get; set; } = true;
    }
}
