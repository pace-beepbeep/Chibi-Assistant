using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chibi_Assistant.Models
{
    public class ChatMessage
    {
        public string Sender { get; set; } = string.Empty; // "Nonoo" atau "Chibi" 
        public string Message { get; set; } = string.Empty;
        public bool IsAI { get; set; } // True = Chibi, False = Nonoo

        // Property bantuan untuk mengatur alignment di UI (Kanan untuk User, Kiri untuk AI)
        public string Alignment => IsAI ? "Left" : "Right"; 
        public string BackgroundColor => IsAI ? "#2D2D30" : "#007ACC";
    }
}
