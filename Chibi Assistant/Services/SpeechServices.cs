using System;
using System.Speech.Synthesis;
using System.Threading.Tasks;

namespace ChibiAssistant.Services
{
    /// <summary>
    /// Text-to-Speech using System.Speech (built-in Windows, offline).
    /// No extra NuGet needed for .NET Framework.
    /// For .NET 6+: add NuGet "System.Speech"
    /// </summary>
    public class SpeechService : IDisposable
    {
        private readonly SpeechSynthesizer _synthesizer;
        private bool _isSpeaking;

        public event Action<bool>? OnSpeakingStateChanged;

        public SpeechService()
        {
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();

            // Try to find a female voice (sounds more Chibi-like)
            foreach (var voice in _synthesizer.GetInstalledVoices())
            {
                if (voice.VoiceInfo.Gender == VoiceGender.Female)
                {
                    _synthesizer.SelectVoice(voice.VoiceInfo.Name);
                    break;
                }
            }

            // Speaking speed — slightly faster feels energetic
            _synthesizer.Rate = 1;
            _synthesizer.Volume = 90;

            _synthesizer.SpeakStarted += (s, e) =>
            {
                _isSpeaking = true;
                OnSpeakingStateChanged?.Invoke(true);
            };

            _synthesizer.SpeakCompleted += (s, e) =>
            {
                _isSpeaking = false;
                OnSpeakingStateChanged?.Invoke(false);
            };
        }

        public async Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Strip emojis and special chars that TTS can't handle well
            string cleanText = CleanTextForSpeech(text);

            await Task.Run(() =>
            {
                _synthesizer.SpeakAsyncCancelAll();
                _synthesizer.Speak(cleanText);
            });
        }

        public void Stop()
        {
            _synthesizer.SpeakAsyncCancelAll();
        }

        private string CleanTextForSpeech(string text)
        {
            // Remove common emoji / markdown artifacts
            return System.Text.RegularExpressions.Regex.Replace(text,
                @"[^\u0000-\u007F\u00C0-\u024F\u1E00-\u1EFF]", " ").Trim();
        }

        public bool IsSpeaking => _isSpeaking;

        public void Dispose()
        {
            _synthesizer?.Dispose();
        }
    }
}