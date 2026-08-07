using Microsoft.EntityFrameworkCore;
using MaxerZ.Api.Models;

namespace MaxerZ.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<CoverLetterRecord> CoverLetters { get; set; } = null!;
        public DbSet<ResumeRecord> Resumes { get; set; } = null!;
    }
}
