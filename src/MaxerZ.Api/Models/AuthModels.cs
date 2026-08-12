using System.ComponentModel.DataAnnotations;

namespace MaxerZ.Api.Models
{
    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = "";

        public string FullName { get; set; } = "";
    }

    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";
    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; } = "";
        public string Message { get; set; } = "";
        public UserProfileDto? User { get; set; }
    }

    public class UserProfileDto
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string LinkedInUrl { get; set; } = "";
        public string GitHubUrl { get; set; } = "";
        public string Address { get; set; } = "";
        public string Role { get; set; } = "User";
    }
}
