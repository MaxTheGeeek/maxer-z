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

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
                p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

            builder.Services.AddControllers().AddApplicationPart(typeof(Program).Assembly);
            builder.Services.AddLogging();

            // Database setup
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MaxerZ", "maxerz.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));

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

            // Auto-migrate database on startup
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
            }

            app.UseCors();
            
            // Serve Angular static files from wwwroot or wwwroot/browser in production
            var browserPath = Path.Combine(builder.Environment.WebRootPath, "browser");
            if (Directory.Exists(browserPath))
            {
                app.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(browserPath)
                });
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(browserPath)
                });
            }
            else if (Directory.Exists(builder.Environment.WebRootPath))
            {
                app.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(builder.Environment.WebRootPath)
                });
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(builder.Environment.WebRootPath)
                });
            }
            else
            {
                app.UseDefaultFiles();
                app.UseStaticFiles();
            }

            app.MapControllers();

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
