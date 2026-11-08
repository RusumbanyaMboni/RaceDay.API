
namespace RaceDay.MVC.Models
{
    public class LoginResponse
    {
        public string Message { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;

        public LoginUser User { get; set; } = new();
    }

    public class LoginUser
    {
        public int UserId { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public int RoleId { get; set; }

        public string Role { get; set; } = string.Empty;
    }
}
