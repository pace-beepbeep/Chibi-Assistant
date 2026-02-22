using System;
using System.IO;
using System.Text.Json;
using Vosk;
using NAudio.Wave;

namespace Chibi_Assistant.Services
{
    public class VoskService : IDisposable
    {
        private Model _model;
        private VoskRecognizer _recognizer;
        private WaveInEvent? _waveIn;
        private bool _isListening = false; // Guard agar DataAvailable tidak crash saat stop
        public event Action<string>? OnResult;
        public event Action<string>? OnPartialResult;

        public VoskService(string modelPath)
        {
            Vosk.Vosk.SetLogLevel(0);
            _model = new Model(modelPath);
            _recognizer = new VoskRecognizer(_model, 16000.0f);
        }

        public void StartListening()
        {
            // Reset recognizer agar sesi baru bersih
            _recognizer?.Dispose();
            _recognizer = new VoskRecognizer(_model, 16000.0f);

            _waveIn = new WaveInEvent { WaveFormat = new WaveFormat(16000, 1) };
            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _isListening = true;
            _waveIn.StartRecording();
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            // Guard: jangan akses recognizer kalau sudah stop
            if (!_isListening) return;

            try
            {
                if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                {
                    var text = ExtractText(_recognizer.Result());
                    if (!string.IsNullOrEmpty(text)) OnResult?.Invoke(text);
                }
                else
                {
                    var partial = ExtractPartialText(_recognizer.PartialResult());
                    if (!string.IsNullOrEmpty(partial)) OnPartialResult?.Invoke(partial);
                }
            }
            catch
            {
                // Abaikan error saat processing audio agar app tidak crash
            }
        }

        public void StopListening()
        {
            _isListening = false; // Matikan guard DULUAN agar DataAvailable berhenti

            try
            {
                _waveIn?.StopRecording();
            }
            catch { /* Abaikan error saat stop recording */ }

            try
            {
                var finalResult = ExtractText(_recognizer.FinalResult());
                if (!string.IsNullOrEmpty(finalResult)) OnResult?.Invoke(finalResult);
            }
            catch { /* Abaikan error saat ambil final result */ }

            try
            {
                _waveIn?.Dispose();
            }
            catch { /* Abaikan error saat dispose */ }

            _waveIn = null;
        }

        private string ExtractText(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return "";
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? "";
                }
                return "";
            }
            catch
            {
                return "";
            }
        }

        private string ExtractPartialText(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return "";
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("partial", out var partialElement))
                {
                    return partialElement.GetString() ?? "";
                }
                return "";
            }
            catch
            {
                return "";
            }
        }

        public void Dispose()
        {
            _isListening = false;
            _waveIn?.Dispose();
            _recognizer?.Dispose();
            _model?.Dispose();
        }
    }
}