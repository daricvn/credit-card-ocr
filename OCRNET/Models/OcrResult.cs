using System;
using System.Collections.Generic;
using System.Text;

namespace OCRNET.Models {
    public class OcrResult {
        public string Text { get; set; }
        public float Confidence { get; set; }
        public OcrResult(string text, float conf ) {
            Text = text;
            Confidence = conf;
        }
    }
}
