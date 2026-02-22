using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using Whisper.net;
using Whisper.net.Ggml;

namespace Chibi_Assistant.Services
{
    public class WhisperService : IDisposable
    {
        private WhisperFactory? _whisperFactory;
        private WhisperProcessor? _processor;
        private WaveInEvent? _waveIn;
        private MemoryStream? _recordingStream;
        private WaveFileWriter? _waveWriter;
        private bool _isRecording;
        private bool _initialized;

        private string _modelPath = "";

        public event Action<string>? OnTranscribed;
        public event Action<bool>? OnRecordingStateChanged;

        // Event untuk lapor progress download (0-100)
        public event Action<int, string>? OnDownloadProgress;

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper-base.bin");

            if (!File.Exists(_modelPath))
            {
                OnDownloadProgress?.Invoke(0, "Mulai download model Whisper (~150MB)...");

                using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Base);

                // Download dengan tracking progress
                const long totalSize = 148 * 1024 * 1024; // ~148MB estimasi
                using var fileStream = File.Create(_modelPath);
                var buffer = new byte[81920]; // 80KB chunks
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await modelStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;

                    int percent = (int)Math.Min(100, totalRead * 100 / totalSize);
                    string mb = $"{totalRead / 1024 / 1024} MB";
                    OnDownloadProgress?.Invoke(percent, $"Downloading... {mb} / ~148 MB ({percent}%)");
                }

                OnDownloadProgress?.Invoke(100, "Download selesai! Memuat model...");
            }
            else
            {
                OnDownloadProgress?.Invoke(100, "Model ditemukan, memuat...");
            }

            _whisperFactory = WhisperFactory.FromPath(_modelPath);
            _processor = _whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            _initialized = true;
            OnDownloadProgress?.Invoke(100, "Siap!");
        }

        public void StartRecording()
        {
            if (_isRecording) return;

            _recordingStream = new MemoryStream();
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1)
            };
            _waveWriter = new WaveFileWriter(_recordingStream, _waveIn.WaveFormat);

            _waveIn.DataAvailable += (s, e) =>
            {
                _waveWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            };

            _waveIn.StartRecording();
            _isRecording = true;
            OnRecordingStateChanged?.Invoke(true);
        }

        public async Task StopRecordingAndTranscribeAsync()
        {
            if (!_isRecording || _waveIn == null) return;

            _waveIn.StopRecording();
            _isRecording = false;
            OnRecordingStateChanged?.Invoke(false);

            await Task.Delay(200);
            _waveWriter?.Flush();

            if (_recordingStream == null || _recordingStream.Length == 0) return;

            _recordingStream.Position = 0;
            string result = await TranscribeAsync(_recordingStream);

            if (!string.IsNullOrWhiteSpace(result))
                OnTranscribed?.Invoke(result.Trim());

            _waveWriter?.Dispose();
            _waveWriter = null;
            _recordingStream?.Dispose();
            _recordingStream = null;
        }

        private async Task<string> TranscribeAsync(Stream audioStream)
        {
            if (_processor == null)
                throw new InvalidOperationException("Whisper belum di-init.");

            var sb = new System.Text.StringBuilder();
            await foreach (var segment in _processor.ProcessAsync(audioStream))
                sb.Append(segment.Text);

            return sb.ToString();
        }

        public void Dispose()
        {
            _waveIn?.Dispose();
            _waveWriter?.Dispose();
            _recordingStream?.Dispose();
            _processor?.Dispose();
            _whisperFactory?.Dispose();
        }
    }
}