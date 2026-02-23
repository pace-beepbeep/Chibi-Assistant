using Chibi_Assistant.Models;
using Chibi_Assistant.Services;
using Chibi_Assistant.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAnimatedGif;

namespace ChibiAssistant
{
    public partial class MainWindow : Window
    {
        // ── HTTP & API ────────────────────────────────────────────
        private static readonly HttpClient client = new HttpClient();
        // Catatan: Sebaiknya API Key jangan ditaruh di kode langsung ya Nonoo, tapi untuk sekarang oke deh!
        private const string ApiKey = "AIzaSyBHAgKy1vgYptaID6L2l7JeBabTcv7-THU";

        // ── Chat messages (binding ke UI) ─────────────────────────
        public ObservableCollection<ChatMessage> Messages { get; set; } = new();

        // ── Voice Service ──────────────────────────────────────────
        private VoskService _vosk;
        private SystemCommandService _commandService = new();
        private bool _isRecording = false; // Penanda apakah mic sedang aktif
        private bool _isBusy = false;      // Penanda asisten sedang berpikir (loading)

        // ── Character Theme & Settings ─────────────────────────────
        private List<CharacterTheme> _characters = new();
        private CharacterTheme _currentCharacter;
        private SettingsService _settingsService = new();
        private UserSettings _userSettings;

        public MainWindow()
        {
            InitializeComponent();

            // Load settings & characters
            _userSettings = _settingsService.Load();
            LoadCharacters();
            ApplyCharacterTheme();

            LoadShortcuts();

            ChatHistoryList.ItemsSource = Messages;

            // Inisialisasi Vosk
            try
            {
                string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "Vosk-EN");

                // Cek apakah folder model ada, jika tidak ada Chibi kasih peringatan
                if (!Directory.Exists(modelPath))
                {
                    MessageBox.Show("Folder model Vosk tidak ditemukan di: " + modelPath + "\nPastikan kamu sudah mengekstrak modelnya ya, Nonoo!");
                    return;
                }

                _vosk = new VoskService(modelPath);

                // Event saat Vosk mendeteksi suara (final result)
                // PENTING: Pakai BeginInvoke (non-blocking) agar tidak deadlock dengan UI thread
                _vosk.OnResult += (text) =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            // Append teks baru agar kalimat panjang tidak hilang
                            if (!string.IsNullOrWhiteSpace(ChatInput.Text))
                                ChatInput.Text += " " + text;
                            else
                                ChatInput.Text = text;
                        }
                        catch { /* Abaikan error UI */ }
                    });
                };

                // Event saat Vosk mengirim partial result (real-time preview)
                _vosk.OnPartialResult += (partial) =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            VoiceStatusText.Text = !string.IsNullOrEmpty(partial)
                                ? $"🎤 {partial}"
                                : "Chibi is listening...";
                        }
                        catch { /* Abaikan error UI */ }
                    });
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Gagal inisialisasi Vosk: " + ex.Message);
            }
        }

        // ── CHARACTER THEME SYSTEM ────────────────────────────────
        private void LoadCharacters()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "characters.json");
                if (!File.Exists(jsonPath))
                {
                    // Fallback: buat default character
                    _characters = new List<CharacterTheme>
                    {
                        new CharacterTheme
                        {
                            Id = "march7th",
                            DisplayName = "March 7th (Evernight)",
                            GifFileName = "evernight-march-7th.gif",
                            Personality = "Kamu adalah asisten desktop bernama Chibi. Sifatmu ceria, imut, agak tsundere tapi perhatian, dan selalu memanggil usernya dengan sebutan 'Nonoo'. Jawablah pesan dengan singkat, lucu, dan gunakan emoji sesekali.",
                            Greeting = "Halo Nonoo~ Ada yang bisa Chibi bantu hari ini? 💕"
                        }
                    };
                    return;
                }

                string json = File.ReadAllText(jsonPath);
                _characters = JsonSerializer.Deserialize<List<CharacterTheme>>(json) ?? new List<CharacterTheme>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Gagal memuat characters.json: " + ex.Message);
                _characters = new List<CharacterTheme>();
            }
        }

        private void ApplyCharacterTheme()
        {
            // Cari karakter yang dipilih
            _currentCharacter = _characters.Find(c => c.Id == _userSettings.SelectedCharacterId)
                                ?? (_characters.Count > 0 ? _characters[0] : null);

            if (_currentCharacter == null) return;

            // Set animated GIF
            try
            {
                var uri = new Uri($"pack://application:,,,/Assets/{_currentCharacter.GifFileName}", UriKind.Absolute);
                var image = new System.Windows.Media.Imaging.BitmapImage(uri);
                ImageBehavior.SetAnimatedSource(ChibiImage, image);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Gagal memuat GIF karakter: {ex.Message}");
            }
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(_characters, _userSettings, _settingsService);
            settingsWindow.Owner = this;
            settingsWindow.OnSettingsSaved += () =>
            {
                // Reload settings dan apply
                _userSettings = _settingsService.Load();
                ApplyCharacterTheme();
            };
            settingsWindow.ShowDialog();
        }

        // ── STARTUP POSITION ───────────────────────────────────
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Posisikan chibi tepat di pojok kanan bawah layar (di atas taskbar)
            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Right - ChibiImage.Width - 20;  // 200px chibi + sedikit margin
            this.Top = workArea.Bottom - ChibiImage.Height - 100;
        }

        // ── DRAG & DOUBLE CLICK ───────────────────────────────────
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    ChatPopup.IsOpen = !ChatPopup.IsOpen;
                    if (ChatPopup.IsOpen && Messages.Count == 0)
                    {
                        string greeting = _currentCharacter?.Greeting ?? "Halo~ Ada yang bisa dibantu? 💕";
                        Messages.Add(new ChatMessage { Sender = "Chibi", Message = greeting, IsAI = true });
                    }
                }
                else
                {
                    this.DragMove();
                }
            }
        }

        private void CloseChat_Click(object sender, RoutedEventArgs e)
        {
            ChatPopup.IsOpen = false;
        }

        // ── MIC BUTTON (Logika Saklar) ──────────────────────────────
        private async void MicBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_vosk == null) return;

            try
            {
                if (!_isRecording)
                {
                    // Mulai Mendengarkan — bersihkan input dulu
                    ChatInput.Text = "";
                    _vosk.StartListening();
                    _isRecording = true;

                    VoiceStatusBar.Visibility = Visibility.Visible;
                    VoiceStatusText.Text = "Chibi is listening...";
                    MicBtn.Content = "🛑";
                    MicBtn.Background = Brushes.Red;
                }
                else
                {
                    // Berhenti Mendengarkan — jalankan di background thread agar tidak deadlock
                    _isRecording = false;

                    // Reset UI dulu (karena kita di UI thread)
                    VoiceStatusBar.Visibility = Visibility.Collapsed;
                    VoiceStatusText.Text = "";
                    MicBtn.Content = "🎤";
                    MicBtn.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3D));

                    // Stop Vosk di background thread agar tidak blocking UI
                    await Task.Run(() =>
                    {
                        try { _vosk.StopListening(); }
                        catch { /* Abaikan error saat stop */ }
                    });

                    // Kasih jeda sedikit (300ms) biar Vosk selesai memproses sisa suara terakhir
                    await Task.Delay(300);

                    // Kirim pesan ke Gemini (Pastikan pakai await)
                    await ProcessSendMessage();
                }
            }
            catch (Exception ex)
            {
                _isRecording = false;
                VoiceStatusBar.Visibility = Visibility.Collapsed;
                MicBtn.Content = "🎤";
                MicBtn.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3D));
                MessageBox.Show("Duh, Chibi kaget! Ada masalah sama mic-nya: " + ex.Message);
            }
        }

        // ── SEND MESSAGE ──────────────────────────────────────────
        private async void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            await ProcessSendMessage();
        }

        private async void ChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await ProcessSendMessage();
        }

        private async Task ProcessSendMessage()
        {
            string userText = ChatInput.Text.Trim();
            if (string.IsNullOrEmpty(userText) || _isBusy) return;

            // Tambah pesan user ke UI
            Messages.Add(new ChatMessage { Sender = "Nonoo", Message = userText, IsAI = false });
            ChatInput.Text = "";

            // ── Cek System Command dulu (open, close, search, dll) ──
            var (isCommand, commandResponse) = _commandService.TryExecute(userText);
            if (isCommand)
            {
                Messages.Add(new ChatMessage { Sender = "Chibi", Message = commandResponse, IsAI = true });
                if (_userSettings.IsTtsEnabled)
                    _ = Task.Run(() => SpeakText(commandResponse));
                return;
            }

            // Set Loading
            _isBusy = true;
            TypingIndicator.Visibility = Visibility.Visible;
            ChatInput.IsEnabled = false;
            SendBtn.IsEnabled = false;
            MicBtn.IsEnabled = false;

            // Ambil respon dari Gemini
            string aiResponse = await GetAIResponse(userText);

            // Tambah respon Chibi ke UI
            Messages.Add(new ChatMessage { Sender = "Chibi", Message = aiResponse, IsAI = true });

            // TTS: Chibi bicara (Tanpa menunggu prosesnya selesai) — hanya jika TTS aktif
            if (_userSettings.IsTtsEnabled)
                _ = Task.Run(() => SpeakText(aiResponse));

            // Reset UI
            TypingIndicator.Visibility = Visibility.Collapsed;
            ChatInput.IsEnabled = true;
            SendBtn.IsEnabled = true;
            MicBtn.IsEnabled = true;
            _isBusy = false;
            ChatInput.Focus();
        }

        // ── GEMINI AI ─────────────────────────────────────────────
        private async Task<string> GetAIResponse(string message)
        {
            try
            {
                // Menggunakan gemini-1.5-flash (lebih baru/cepat dibanding 2.5 yang belum rilis umum)
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={ApiKey}";

                // Gunakan personality dari karakter yang dipilih
                string personality = _currentCharacter?.Personality
                    ?? "Kamu adalah asisten desktop bernama Chibi. Sifatmu ceria, imut, agak tsundere tapi perhatian, dan selalu memanggil usernya dengan sebutan 'Nonoo'. Jawablah pesan ini dengan singkat, lucu, dan gunakan emoji:";

                string prompt = $"{personality} {message}";

                var requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } }
                };

                string jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(responseJson);

                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                return text?.Trim() ?? "Chibi bingung mau jawab apa 🥺";
            }
            catch (Exception ex)
            {
                return $"Waduh, Chibi lagi pusing nih! (Error: {ex.Message}) 🥺";
            }
        }

        // ── TEXT TO SPEECH ────────────────────────────────────────
        private void SpeakText(string text)
        {
            try
            {
                using var synth = new System.Speech.Synthesis.SpeechSynthesizer();
                synth.SetOutputToDefaultAudioDevice();
                synth.Volume = _userSettings.TtsVolume;

                // Bersihkan emoji agar TTS Windows tidak bingung
                string cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"[^\u0000-\u007F]", "");
                synth.Speak(cleanText);
            }
            catch { /* Ignore error TTS */ }
        }

        // ── SHORTCUTS ─────────────────────────────────────────────
        private void LoadShortcuts()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts.json");
                if (!File.Exists(jsonPath)) return;

                string jsonString = File.ReadAllText(jsonPath);
                var shortcuts = JsonSerializer.Deserialize<List<ShortcutItem>>(jsonString);

                ContextMenu contextMenu = new ContextMenu();
                if (shortcuts != null)
                {
                    foreach (var item in shortcuts)
                    {
                        MenuItem menuItem = new MenuItem { Header = item.Name };
                        menuItem.Click += (s, e) => OpenShortcut(item.Path);
                        contextMenu.Items.Add(menuItem);
                    }
                }

                contextMenu.Items.Add(new Separator());

                // ⚙ Menu Pengaturan
                MenuItem settingsMenu = new MenuItem { Header = "⚙ Pengaturan" };
                settingsMenu.Click += (s, e) => OpenSettings();
                contextMenu.Items.Add(settingsMenu);

                contextMenu.Items.Add(new Separator());

                MenuItem exitMenu = new MenuItem { Header = "❌ Tutup Chibi" };
                exitMenu.Click += (s, e) => Application.Current.Shutdown();
                contextMenu.Items.Add(exitMenu);

                ChibiImage.ContextMenu = contextMenu;
            }
            catch (Exception ex) { MessageBox.Show("Gagal memuat shortcut: " + ex.Message); }
        }

        private void OpenShortcut(string path)
        {
            try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show($"Gagal buka {path}: {ex.Message}"); }
        }
    }
}