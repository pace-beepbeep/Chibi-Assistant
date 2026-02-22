using Chibi_Assistant;
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

namespace ChibiAssistant
{
    public partial class MainWindow : Window
    {

        private static readonly HttpClient client = new HttpClient();
        private const string ApiKey = "AIzaSyCgIU2khsoARuUKjdhI6rzs0k_bYeyEoJg";
        // Menyimpan riwayat pesan agar otomatis update di UI
        public ObservableCollection<ChatMessage> Messages { get; set; } = new ObservableCollection<ChatMessage>();

        public MainWindow()
        {
            InitializeComponent();
            LoadShortcuts(); // Panggil fungsi shortcut buatanmu

            // Hubungkan list UI dengan data Messages
            ChatHistoryList.ItemsSource = Messages;
        }

        // --- BAGIAN 1: KONTROL WINDOW & DRAG ---

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    // Kalau di-double click, buka/tutup chat
                    ChatPopup.IsOpen = !ChatPopup.IsOpen;

                    // Sapa Nonoo kalau baru pertama buka
                    if (ChatPopup.IsOpen && Messages.Count == 0)
                    {
                        Messages.Add(new ChatMessage { Sender = "Chibi", Message = "Halo Nonoo~ Ada yang bisa Chibi bantu hari ini? 💕", IsAI = true });
                    }
                }
                else
                {
                    // Kalau klik biasa ditahan, drag windownya
                    this.DragMove();
                }
            }
        }

        private void CloseChat_Click(object sender, RoutedEventArgs e)
        {
            ChatPopup.IsOpen = false;
        }

        // --- BAGIAN 2: LOGIKA SHORTCUT MENU (Dari kodemu sebelumnya) ---

        private void LoadShortcuts()
        {
            try
            {
                // Membaca konfigurasi dari JSON
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts.json");
                if (!File.Exists(jsonPath)) return;

                string jsonString = File.ReadAllText(jsonPath);
                var shortcuts = JsonSerializer.Deserialize<List<ShortcutItem>>(jsonString);

                // Membuat Context Menu secara dinamis
                ContextMenu contextMenu = new ContextMenu();

                if (shortcuts != null)
                {
                    foreach (var item in shortcuts)
                    {
                        MenuItem menuItem = new MenuItem
                        {
                            Header = item.Name,
                            Tag = item.Path
                        };

                        menuItem.Click += (s, e) => OpenShortcut(item.Path);
                        contextMenu.Items.Add(menuItem);
                    }
                }

                // Tambahkan separator dan tombol exit yang keren
                contextMenu.Items.Add(new Separator());
                MenuItem exitMenu = new MenuItem { Header = "❌ Tutup Chibi" };
                exitMenu.Click += (s, e) => Application.Current.Shutdown();
                contextMenu.Items.Add(exitMenu);

                // Pasang menu ke Gambar Chibi
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
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Duh, gagal buka {path} nih! 🥺\nCek apakah aplikasinya sudah terinstall ya.\nError: {ex.Message}");
            }
        }

        // --- BAGIAN 3: LOGIKA CHAT AI ---

        private async void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            await ProcessSendMessage();
        }

        private async void ChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await ProcessSendMessage();
            }
        }

        private async Task ProcessSendMessage()
        {
            string userText = ChatInput.Text.Trim();
            if (string.IsNullOrEmpty(userText)) return;

            // 1. Tambahkan pesan Nonoo ke UI
            Messages.Add(new ChatMessage { Sender = "Nonoo", Message = userText, IsAI = false });
            ChatInput.Text = ""; // Kosongkan input

            // Tampilkan loading indicator
            TypingIndicator.Visibility = Visibility.Visible;
            ChatInput.IsEnabled = false;
            SendBtn.IsEnabled = false;

            // 2. Minta jawaban dari AI
            string aiResponse = await GetAIResponse(userText);

            // 3. Tambahkan jawaban AI ke UI
            Messages.Add(new ChatMessage { Sender = "Chibi", Message = aiResponse, IsAI = true });

            // Sembunyikan loading
            TypingIndicator.Visibility = Visibility.Collapsed;
            ChatInput.IsEnabled = true;
            SendBtn.IsEnabled = true;
            ChatInput.Focus();
        }

        // Simulasi fungsi API sementara
        private async Task<string> GetAIResponse(string message)
        {
            try
            {
                // Gunakan model Gemini 1.5 Flash (Cepat & Pintar)
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={ApiKey}";

                // Kita kasih "System Prompt" rahasia biar AI-nya sadar dia itu Chibi
                string prompt = $"Kamu adalah asisten desktop bernama Chibi. Sifatmu ceria, imut, agak tsundere tapi perhatian, dan selalu memanggil usernya dengan sebutan 'Nonoo'. Jawablah pesan ini dengan singkat, lucu, dan gunakan emoji: {message}";

                // Format request JSON sesuai standar API Gemini
                var requestBody = new
                {
                    contents = new[]
                    {
                new { parts = new[] { new { text = prompt } } }
            }
                };

                // Ubah jadi JSON string
                string jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                // Tembak API-nya! 🚀
                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                // Ambil hasil balasannya
                string responseJson = await response.Content.ReadAsStringAsync();

                // Ekstrak text balasannya dari JSON (Parsing)
                using (JsonDocument doc = JsonDocument.Parse(responseJson))
                {
                    var root = doc.RootElement;
                    var text = root.GetProperty("candidates")[0]
                                   .GetProperty("content")
                                   .GetProperty("parts")[0]
                                   .GetProperty("text").GetString();

                    // Hapus whitespace atau enter berlebih
                    return text?.Trim() ?? "Chibi bingung mau jawab apa 🥺";
                }
            }
            catch (Exception ex)
            {
                // Kalau internet mati atau API error
                return $"Waduh, Chibi lagi pusing nih! (Error: {ex.Message}) 🥺";
            }
        }
    }
}