using System;
using System.ComponentModel.DataAnnotations;

namespace MaxerZ.Api.Models
{
    public class ApplicationUser
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string PasswordHash { get; set; } = "";

        public string FullName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string LinkedInUrl { get; set; } = "";
        public string GitHubUrl { get; set; } = "";
        public string WebsiteUrl { get; set; } = "";
        public string Address { get; set; } = "";

        public string Role { get; set; } = "User"; // "User" | "Admin"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
