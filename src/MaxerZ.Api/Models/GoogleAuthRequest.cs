using System.ComponentModel.DataAnnotations;

namespace MaxerZ.Api.Models
{
    public class GoogleAuthRequest
    {
        public string IdToken { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        public string FullName { get; set; } = "";
        public string GoogleUserId { get; set; } = "";
    }
}
