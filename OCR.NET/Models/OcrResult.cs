using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR.NET.Models {
    public class OcrResult {
        public string Text { get; set; }
        public float Confidence { get; set; }
        public OcrResult( string text, float conf ) {
            Text = text;
            Confidence = conf;
        }
    }
}
