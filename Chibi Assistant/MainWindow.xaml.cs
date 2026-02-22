using Chibi_Assistant;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChibiAssistant
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadShortcuts();
        }

        // Agar window bisa digeser (Drag)
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

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
                            // Kita simpan path di Tag agar mudah diakses
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

                // Pasang menu ke Gambar Chibi agar klik kanan muncul tepat di karakternya
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
    }
}