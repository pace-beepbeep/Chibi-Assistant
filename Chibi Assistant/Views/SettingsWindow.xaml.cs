using Chibi_Assistant.Models;
using Chibi_Assistant.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using WpfAnimatedGif;

namespace Chibi_Assistant.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly List<CharacterTheme> _characters;
        private readonly UserSettings _settings;
        private readonly SettingsService _settingsService;

        /// <summary>
        /// Event yang dipanggil saat user menekan Simpan, agar MainWindow bisa apply perubahan.
        /// </summary>
        public event Action? OnSettingsSaved;

        public SettingsWindow(List<CharacterTheme> characters, UserSettings currentSettings, SettingsService settingsService)
        {
            InitializeComponent();

            _characters = characters;
            _settings = new UserSettings
            {
                SelectedCharacterId = currentSettings.SelectedCharacterId,
                IsTtsEnabled = currentSettings.IsTtsEnabled,
                TtsVolume = currentSettings.TtsVolume
            };
            _settingsService = settingsService;

            // Populate character combo
            CharacterCombo.ItemsSource = _characters;

            // Select current character
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].Id == _settings.SelectedCharacterId)
                {
                    CharacterCombo.SelectedIndex = i;
                    break;
                }
            }

            // Set TTS controls
            TtsToggle.IsChecked = _settings.IsTtsEnabled;
            VolumeSlider.Value = _settings.TtsVolume;
            VolumeLabel.Text = $"{_settings.TtsVolume}%";
            VolumeSlider.IsEnabled = _settings.IsTtsEnabled;
        }

        private void CharacterCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CharacterCombo.SelectedItem is CharacterTheme selected)
            {
                _settings.SelectedCharacterId = selected.Id;
                PreviewGreeting.Text = selected.Greeting;

                // Update preview GIF
                try
                {
                    var uri = new Uri($"pack://application:,,,/Assets/{selected.GifFileName}", UriKind.Absolute);
                    var image = new System.Windows.Media.Imaging.BitmapImage(uri);
                    ImageBehavior.SetAnimatedSource(PreviewImage, image);
                }
                catch
                {
                    ImageBehavior.SetAnimatedSource(PreviewImage, null);
                    PreviewGreeting.Text = "(GIF belum tersedia)";
                }
            }
        }

        private void TtsToggle_Changed(object sender, RoutedEventArgs e)
        {
            _settings.IsTtsEnabled = TtsToggle.IsChecked == true;
            VolumeSlider.IsEnabled = _settings.IsTtsEnabled;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _settings.TtsVolume = (int)VolumeSlider.Value;
            if (VolumeLabel != null)
                VolumeLabel.Text = $"{_settings.TtsVolume}%";
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.Save(_settings);
            OnSettingsSaved?.Invoke();
            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
