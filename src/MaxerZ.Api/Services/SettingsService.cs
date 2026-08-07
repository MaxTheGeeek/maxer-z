using System;
using System.IO;
using System.Text.Json;
using MaxerZ.Api.Models;

namespace MaxerZ.Api.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;
        private readonly string _mcpPath;
        private AppSettings _cache;
        private McpConfig _mcpCache;

        public SettingsService()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MaxerZ");
            Directory.CreateDirectory(dir);
            _settingsPath = Path.Combine(dir, "settings.json");
            _mcpPath = Path.Combine(dir, "mcp.json");
            _cache = Load<AppSettings>(_settingsPath) ?? new AppSettings();
            if (_cache.Profile.Addresses == null || _cache.Profile.Addresses.Count == 0)
            {
                _cache.Profile.Addresses = new List<string> { _cache.Profile.Address };
            }
            _mcpCache = Load<McpConfig>(_mcpPath) ?? new McpConfig();
        }

        public AppSettings Get() => _cache;

        public void Save(AppSettings s)
        {
            _cache = s;
            Write(_settingsPath, s);
        }

        public McpConfig GetMcpConfig() => _mcpCache;

        public void SaveMcpConfig(McpConfig c)
        {
            _mcpCache = c;
            Write(_mcpPath, c);
        }

        private static T? Load<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)); }
            catch { return null; }
        }

        private static void Write<T>(string path, T obj) =>
            File.WriteAllText(path, JsonSerializer.Serialize(obj,
                new JsonSerializerOptions { WriteIndented = true }));
    }
}
