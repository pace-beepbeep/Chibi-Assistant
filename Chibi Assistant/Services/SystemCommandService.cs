using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Chibi_Assistant.Services
{
    public class SystemCommandService
    {
        // Daftar alias → path/nama aplikasi yang umum
        private readonly Dictionary<string, List<string>> _appAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // ── Browser ──
            ["brave"]       = new() { "brave.exe", @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe" },
            ["chrome"]      = new() { "chrome.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe", @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" },
            ["firefox"]     = new() { "firefox.exe", @"C:\Program Files\Mozilla Firefox\firefox.exe" },
            ["edge"]        = new() { "msedge.exe", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" },
            ["opera"]       = new() { "opera.exe" },

            // ── Chat & Social ──
            ["discord"]     = new() { "discord.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Discord\Update.exe --processStart Discord.exe") },
            ["telegram"]    = new() { "telegram.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Telegram Desktop\Telegram.exe") },
            ["whatsapp"]    = new() { "whatsapp.exe" },

            // ── Game ──
            ["valorant"]    = new() { @"C:\Riot Games\Riot Client\RiotClientServices.exe --launch-product=valorant --launch-patchline=live" },
            ["steam"]       = new() { "steam.exe", @"C:\Program Files (x86)\Steam\steam.exe", @"C:\Program Files\Steam\steam.exe" },
            ["epic games"]  = new() { @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe", @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe" },

            // ── Coding ──
            ["vscode"]      = new() { "code.exe", "code" },
            ["visual studio code"] = new() { "code.exe", "code" },
            ["visual studio"] = new() { "devenv.exe" },
            ["notepad"]     = new() { "notepad.exe" },
            ["notepad++"]   = new() { @"C:\Program Files\Notepad++\notepad++.exe", @"C:\Program Files (x86)\Notepad++\notepad++.exe" },

            // ── Utilitas ──
            ["explorer"]    = new() { "explorer.exe" },
            ["file explorer"] = new() { "explorer.exe" },
            ["task manager"] = new() { "taskmgr.exe" },
            ["calculator"]  = new() { "calc.exe" },
            ["cmd"]         = new() { "cmd.exe" },
            ["terminal"]    = new() { "wt.exe", "cmd.exe" },
            ["powershell"]  = new() { "powershell.exe" },
            ["paint"]       = new() { "mspaint.exe" },
            ["spotify"]     = new() { "spotify.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Spotify\Spotify.exe") },
            ["word"]        = new() { "winword.exe" },
            ["excel"]       = new() { "excel.exe" },
            ["powerpoint"]  = new() { "powerpnt.exe" },

            // ── Multimedia ──
            ["obs"]         = new() { @"C:\Program Files\obs-studio\bin\64bit\obs64.exe", @"C:\Program Files (x86)\obs-studio\bin\64bit\obs64.exe" },
            ["vlc"]         = new() { @"C:\Program Files\VideoLAN\VLC\vlc.exe", @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe" },
        };

        /// <summary>
        /// Cek apakah teks adalah system command. Mengembalikan (true, response) jika berhasil.
        /// </summary>
        public (bool isCommand, string response) TryExecute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return (false, "");

            string text = input.Trim().ToLower();

            // ── OPEN / BUKA command ──
            if (text.StartsWith("open ") || text.StartsWith("buka "))
            {
                string appName = text.Substring(text.IndexOf(' ') + 1).Trim();
                return TryOpenApp(appName);
            }

            // ── CLOSE / TUTUP command ──
            if (text.StartsWith("close ") || text.StartsWith("tutup "))
            {
                string appName = text.Substring(text.IndexOf(' ') + 1).Trim();
                return TryCloseApp(appName);
            }

            // ── SEARCH / CARI command ──
            if (text.StartsWith("search ") || text.StartsWith("cari "))
            {
                string query = text.Substring(text.IndexOf(' ') + 1).Trim();
                return TrySearchWeb(query);
            }

            // ── SHUTDOWN / RESTART ──
            if (text == "shutdown" || text == "matikan komputer")
            {
                return (true, "Hmm, Chibi tidak boleh matikan komputer sembarangan ya Nonoo~ Bahaya deh! 😤");
            }

            if (text == "restart" || text == "restart komputer")
            {
                return (true, "Chibi tidak mau restart komputer, nanti Chibi hilang dong! 😭");
            }

            return (false, "");
        }

        private (bool, string) TryOpenApp(string appName)
        {
            // Cek di alias dulu
            if (_appAliases.TryGetValue(appName, out var paths))
            {
                foreach (var path in paths)
                {
                    try
                    {
                        // Cek apakah path berisi argumen (misal Riot Client)
                        string fileName;
                        string arguments = "";

                        if (path.Contains(" --") || path.Contains(" /"))
                        {
                            int argStart = path.IndexOf(" --");
                            if (argStart < 0) argStart = path.IndexOf(" /");
                            fileName = path.Substring(0, argStart);
                            arguments = path.Substring(argStart + 1);
                        }
                        else
                        {
                            fileName = path;
                        }

                        // Cek apakah file ada (jika path absolut)
                        if (fileName.Contains("\\") && !File.Exists(fileName))
                            continue;

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fileName,
                            Arguments = arguments,
                            UseShellExecute = true
                        });

                        return (true, $"Siap Nonoo~! Chibi sudah bukakan {appName} untuk kamu! 🚀✨");
                    }
                    catch
                    {
                        continue; // Coba path berikutnya
                    }
                }
            }

            // Fallback: coba langsung jalankan nama app sebagai command
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = appName,
                    UseShellExecute = true
                });
                return (true, $"Oke Nonoo~! Chibi coba bukakan {appName}! ✨");
            }
            catch { }

            // Fallback 2: coba cari di Start Menu
            try
            {
                var startMenuPaths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                };

                foreach (var menuPath in startMenuPaths)
                {
                    if (!Directory.Exists(menuPath)) continue;

                    var shortcuts = Directory.GetFiles(menuPath, "*.lnk", SearchOption.AllDirectories);
                    var match = shortcuts.FirstOrDefault(s =>
                        Path.GetFileNameWithoutExtension(s).Contains(appName, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = match,
                            UseShellExecute = true
                        });
                        return (true, $"Ketemu! Chibi bukakan {Path.GetFileNameWithoutExtension(match)} ya~ 🎯✨");
                    }
                }
            }
            catch { }

            return (true, $"Duh, Chibi tidak bisa menemukan '{appName}' di komputer kamu, Nonoo 😢 Coba cek namanya lagi ya!");
        }

        private (bool, string) TryCloseApp(string appName)
        {
            try
            {
                // Cari proses berdasarkan nama
                var processes = Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains(appName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (processes.Count == 0)
                {
                    return (true, $"Hmm, Chibi tidak menemukan {appName} yang sedang berjalan, Nonoo 🤔");
                }

                foreach (var proc in processes)
                {
                    try { proc.CloseMainWindow(); } catch { }
                }

                return (true, $"Oke! Chibi sudah tutup {appName} ya, Nonoo~ 👋");
            }
            catch
            {
                return (true, $"Chibi gagal menutup {appName} 😅 Mungkin perlu ditutup manual, Nonoo!");
            }
        }

        private (bool, string) TrySearchWeb(string query)
        {
            try
            {
                string url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                return (true, $"Chibi carikan '{query}' di Google ya, Nonoo~ 🔍✨");
            }
            catch
            {
                return (true, "Chibi gagal buka browser untuk searching 😢");
            }
        }

        /// <summary>
        /// Tambah alias custom untuk app.
        /// </summary>
        public void AddAlias(string alias, string path)
        {
            if (_appAliases.ContainsKey(alias))
                _appAliases[alias].Insert(0, path);
            else
                _appAliases[alias] = new List<string> { path };
        }
    }
}
