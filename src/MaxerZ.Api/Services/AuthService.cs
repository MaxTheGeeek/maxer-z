using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MaxerZ.Api.Data;
using MaxerZ.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MaxerZ.Api.Services
{
    public class AuthService
    {
        private readonly AppDbContext _db;
        private readonly string _jwtSecret;

        public AuthService(AppDbContext db, IConfiguration configuration)
        {
            _db = db;
            _jwtSecret = configuration["JwtSecretKey"] ?? "MaxerZ_Production_Super_Secure_JWT_Secret_Key_2026!";
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            {
                return new AuthResponse { Success = false, Message = "Email and password are required." };
            }

            var normalizedEmail = req.Email.Trim().ToLowerInvariant();
            var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (existing != null)
            {
                return new AuthResponse { Success = false, Message = "An account with this email already exists." };
            }

            var user = new ApplicationUser
            {
                Email = normalizedEmail,
                FullName = req.FullName.Trim(),
                PasswordHash = HashPassword(req.Password),
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            return new AuthResponse
            {
                Success = true,
                Token = token,
                Message = "Registration successful.",
                User = MapUserDto(user)
            };
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            {
                return new AuthResponse { Success = false, Message = "Email and password are required." };
            }

            var normalizedEmail = req.Email.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user == null || !VerifyPassword(req.Password, user.PasswordHash))
            {
                return new AuthResponse { Success = false, Message = "Invalid email or password." };
            }

            var token = GenerateJwtToken(user);
            return new AuthResponse
            {
                Success = true,
                Token = token,
                Message = "Login successful.",
                User = MapUserDto(user)
            };
        }

        public async Task<AuthResponse> GoogleLoginAsync(GoogleAuthRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email))
            {
                return new AuthResponse { Success = false, Message = "Google account email is required." };
            }

            var normalizedEmail = req.Email.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user == null)
            {
                // Auto-register Google user account
                user = new ApplicationUser
                {
                    Email = normalizedEmail,
                    FullName = string.IsNullOrWhiteSpace(req.FullName) ? normalizedEmail.Split('@')[0] : req.FullName.Trim(),
                    PasswordHash = HashPassword("Google_OAuth_" + Guid.NewGuid().ToString()),
                    Role = "User",
                    CreatedAt = DateTime.UtcNow
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }

            var token = GenerateJwtToken(user);
            return new AuthResponse
            {
                Success = true,
                Token = token,
                Message = "Google authentication successful.",
                User = MapUserDto(user)
            };
        }

        public async Task<UserProfileDto?> GetProfileAsync(string userId)
        {
            var user = await _db.Users.FindAsync(userId);
            return user != null ? MapUserDto(user) : null;
        }

        public async Task<UserProfileDto?> UpdateProfileAsync(string userId, UserProfileDto dto)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return null;

            user.FullName = dto.FullName ?? user.FullName;
            user.Phone = dto.Phone ?? user.Phone;
            user.LinkedInUrl = dto.LinkedInUrl ?? user.LinkedInUrl;
            user.GitHubUrl = dto.GitHubUrl ?? user.GitHubUrl;
            user.Address = dto.Address ?? user.Address;

            await _db.SaveChangesAsync();
            return MapUserDto(user);
        }

        public string GenerateJwtToken(ApplicationUser user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSecret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                    new Claim(ClaimTypes.Role, user.Role ?? "User")
                }),
                Expires = DateTime.UtcNow.AddDays(30),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "MaxerZ_Salt_2026"));
            return Convert.ToBase64String(hashedBytes);
        }

        public static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        private static UserProfileDto MapUserDto(ApplicationUser u) => new()
        {
            Id = u.Id,
            Email = u.Email,
            FullName = u.FullName,
            Phone = u.Phone,
            LinkedInUrl = u.LinkedInUrl,
            GitHubUrl = u.GitHubUrl,
            Address = u.Address,
            Role = u.Role ?? "User"
        };
    }
}
