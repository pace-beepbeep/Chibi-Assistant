using System;
using System.IO;
using System.Text.Json;

namespace Chibi_Assistant.Services
{
    public class UserSettings
    {
        public string SelectedCharacterId { get; set; } = "march7th";
        public bool IsTtsEnabled { get; set; } = true;
        public int TtsVolume { get; set; } = 100;
    }

    public class SettingsService
    {
        private static readonly string SettingsFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChibiAssistant");

        private static readonly string SettingsFile =
            Path.Combine(SettingsFolder, "settings.json");

        public UserSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                    return new UserSettings();

                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
            catch
            {
                return new UserSettings();
            }
        }

        public void Save(UserSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                    Directory.CreateDirectory(SettingsFolder);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch
            {
                // Silently fail — settings are non-critical
            }
        }
    }
}
