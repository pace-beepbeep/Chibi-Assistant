using Chibi_Assistant.Models;
using Chibi_Assistant.Services;
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

namespace ChibiAssistant
{
    public partial class MainWindow : Window
    {
        // ── HTTP & API ────────────────────────────────────────────
        private static readonly HttpClient client = new HttpClient();
        private const string ApiKey = "AIzaSyB9TDbLr2h4iw8dS3KKu7Xpfj5l2XqAZQQ";

        // ── Chat messages (binding ke UI) ─────────────────────────
        public ObservableCollection<ChatMessage> Messages { get; set; } = new();

        // ── Whisper Service ───────────────────────────────────────
        private readonly WhisperService _whisper;
        private bool _isListening = false;
        private bool _isBusy = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadShortcuts();

            ChatHistoryList.ItemsSource = Messages;

            // Init Whisper
            _whisper = new WhisperService();
            _whisper.OnTranscribed += OnVoiceTranscribed;
            _whisper.OnDownloadProgress += OnDownloadProgress;
            _whisper.OnRecordingStateChanged += OnRecordingStateChanged;

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        // ── Init: Download Whisper model saat startup ─────────────
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _whisper.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Whisper gagal init: {ex.Message}\nVoice recognition tidak aktif.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
                        Messages.Add(new ChatMessage { Sender = "Chibi", Message = "Halo Nonoo~ Ada yang bisa Chibi bantu hari ini? 💕", IsAI = true });
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
                        MenuItem menuItem = new MenuItem { Header = item.Name, Tag = item.Path };
                        menuItem.Click += (s, e) => OpenShortcut(item.Path);
                        contextMenu.Items.Add(menuItem);
                    }
                }

                contextMenu.Items.Add(new Separator());
                MenuItem exitMenu = new MenuItem { Header = "❌ Tutup Chibi" };
                exitMenu.Click += (s, e) => Application.Current.Shutdown();
                contextMenu.Items.Add(exitMenu);

                ChibiImage.ContextMenu = contextMenu;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal memuat shortcut: " + ex.Message);
            }
        }

        private void OpenShortcut(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Duh, gagal buka {path} nih! 🥺\nError: {ex.Message}");
            }
        }

        // ── DOWNLOAD PROGRESS ─────────────────────────────────────
        private void OnDownloadProgress(int percent, string message)
        {
            Dispatcher.Invoke(() =>
            {
                // Tampilkan progress sebagai pesan Chibi di chat
                // Update pesan terakhir kalau masih downloading, atau tambah baru
                if (Messages.Count > 0 && !Messages[Messages.Count - 1].IsAI == false)
                {
                    Messages[Messages.Count - 1] = new ChatMessage
                    {
                        Sender = "Chibi",
                        Message = $"⬇️ {message}",
                        IsAI = true
                    };
                }
                else
                {
                    Messages.Add(new ChatMessage { Sender = "Chibi", Message = $"⬇️ {message}", IsAI = true });
                }
            });
        }

        // ── MIC BUTTON ────────────────────────────────────────────
        private async void MicBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            if (!_isListening)
            {
                // Mulai rekam
                _whisper.StartRecording();
                _isListening = true;
            }
            else
            {
                // Stop & transcribe
                _isListening = false;
                _isBusy = true;
                VoiceStatusText.Text = "Memproses suara...";
                await _whisper.StopRecordingAndTranscribeAsync();
                _isBusy = false;
            }
        }

        // ── WHISPER EVENTS ────────────────────────────────────────
        private void OnRecordingStateChanged(bool isRecording)
        {
            Dispatcher.Invoke(() =>
            {
                if (isRecording)
                {
                    // Tampilkan voice status bar
                    VoiceStatusBar.Visibility = Visibility.Visible;
                    VoiceStatusText.Text = "Mendengarkan...";
                    MicBtn.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));
                }
                else
                {
                    VoiceStatusBar.Visibility = Visibility.Collapsed;
                    MicBtn.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3D));
                }
            });
        }

        private async void OnVoiceTranscribed(string text)
        {
            // Isi ChatInput dengan hasil transcribe lalu kirim
            await Dispatcher.InvokeAsync(() =>
            {
                ChatInput.Text = text;
                VoiceStatusBar.Visibility = Visibility.Collapsed;
            });

            await ProcessSendMessage();
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

            Messages.Add(new ChatMessage { Sender = "Nonoo", Message = userText, IsAI = false });
            ChatInput.Text = "";

            TypingIndicator.Visibility = Visibility.Visible;
            ChatInput.IsEnabled = false;
            SendBtn.IsEnabled = false;
            MicBtn.IsEnabled = false;
            _isBusy = true;

            string aiResponse = await GetAIResponse(userText);
            Messages.Add(new ChatMessage { Sender = "Chibi", Message = aiResponse, IsAI = true });

            // TTS: Chibi bicara
            _ = Task.Run(() => SpeakText(aiResponse));

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
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={ApiKey}";
                string prompt = $"Kamu adalah asisten desktop bernama Chibi. Sifatmu ceria, imut, agak tsundere tapi perhatian, dan selalu memanggil usernya dengan sebutan 'Nonoo'. Jawablah pesan ini dengan singkat, lucu, dan gunakan emoji: {message}";

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

        // ── TEXT TO SPEECH (Windows built-in) ────────────────────
        private void SpeakText(string text)
        {
            try
            {
                using var synth = new System.Speech.Synthesis.SpeechSynthesizer();
                synth.SetOutputToDefaultAudioDevice();
                synth.Rate = 1;

                // Bersihkan emoji agar TTS tidak error
                string clean = System.Text.RegularExpressions.Regex.Replace(text,
                    @"[^\u0000-\u007F\u00C0-\u024F]", " ").Trim();

                synth.Speak(clean);
            }
            catch { /* TTS gagal, skip saja */ }
        }

        // ── CLEANUP ───────────────────────────────────────────────
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _whisper.Dispose();
        }
    }
}