using System;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using MaxerZ.Api.Services;
using MaxerZ.Api.Services.Providers;
using MaxerZ.Api.Data;

namespace MaxerZ.Api
{
    public class Program
    {
        public static int ActivePort { get; set; }

        public static void Main(string[] args)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            PdfSharpCore.Fonts.GlobalFontSettings.FontResolver = new MaxerZFontResolver();

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
                p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

            builder.Services.AddControllers().AddApplicationPart(typeof(Program).Assembly);
            builder.Services.AddLogging();

            // Database setup: Supabase PostgreSQL (Production / Cloud) or SQLite (Local fallback)
            var postgresConnStr = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING") ??
                                  Environment.GetEnvironmentVariable("SUPABASE_DB_URL") ??
                                  builder.Configuration["SupabaseConnectionString"];

            if (!string.IsNullOrWhiteSpace(postgresConnStr))
            {
                builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(postgresConnStr));
            }
            else
            {
                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MaxerZ", "maxerz.db");
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
                builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));
            }

            // Settings (singleton — cached in memory)
            builder.Services.AddSingleton<SettingsService>();

            // HTTP clients with sensible timeouts
            builder.Services.AddHttpClient("openrouter", c =>
                c.Timeout = TimeSpan.FromSeconds(30));
            builder.Services.AddHttpClient("groq", c =>
                c.Timeout = TimeSpan.FromSeconds(25));
            builder.Services.AddHttpClient("ollama", c =>
                c.Timeout = TimeSpan.FromSeconds(60));

            // LLM Providers — all registered, orchestrator picks active ones at runtime
            builder.Services.AddSingleton<ILlmProvider, OpenRouterProvider>();
            builder.Services.AddSingleton<ILlmProvider, GroqProvider>();
            builder.Services.AddSingleton<ILlmProvider, OllamaProvider>();

            // Core services
            builder.Services.AddScoped<LlmOrchestrator>();
            builder.Services.AddScoped<PdfService>();
            builder.Services.AddScoped<TemplateService>();
            builder.Services.AddScoped<McpService>();
            builder.Services.AddScoped<AtsService>();
            builder.Services.AddScoped<AuthService>();

            // Determine correct wwwroot path for standard app vs. Mac Catalyst bundle
            var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
            if (!Directory.Exists(wwwrootPath))
            {
                var resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "wwwroot");
                if (Directory.Exists(resourcesPath))
                {
                    wwwrootPath = resourcesPath;
                }
            }
            builder.Environment.WebRootPath = wwwrootPath;

            var app = builder.Build();

            // Auto-create database tables on startup
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
                try
                {
                    var creator = (Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator)
                        Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
                        .GetService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>(db.Database);
                    creator.CreateTables();
                }
                catch
                {
                    // Ignore if tables already exist
                }
            }

            app.UseCors();
            
            // Serve Angular static files from wwwroot or wwwroot/browser in production
            var browserPath = Path.Combine(builder.Environment.WebRootPath, "browser");
            Microsoft.Extensions.FileProviders.IFileProvider fileProvider;
            if (Directory.Exists(browserPath))
            {
                fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(browserPath);
            }
            else if (Directory.Exists(builder.Environment.WebRootPath))
            {
                fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(builder.Environment.WebRootPath);
            }
            else
            {
                fileProvider = builder.Environment.ContentRootFileProvider;
            }

            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

            app.MapControllers();
            app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = fileProvider });

            // Find free port and write to temp file for MAUI to read
            var port = FindFreePort();
            ActivePort = port;
            var portFilePath = Path.Combine(Path.GetTempPath(), "maxerz_port.txt");
            File.WriteAllText(portFilePath, port.ToString());

            app.Urls.Add($"http://localhost:{port}");
            app.Run();
        }

        private static int FindFreePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
