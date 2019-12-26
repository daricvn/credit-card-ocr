using Numpy;
using OCRConsole.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Tesseract;

namespace OCRConsole {
    public class OCR: IDisposable {
        public const float MIN_CONFIDENCE = 0.6f;
        public const float SAFE_CONFIDENCE = 0.82f;
        private static readonly PageSegMode[] modePresets = new PageSegMode[] { PageSegMode.SingleBlock, PageSegMode.SingleLine, PageSegMode.SingleWord, PageSegMode.RawLine };

        private TesseractEngine EngineInstance = new TesseractEngine(@"./tessdata", "credit+credit2+eng", EngineMode.TesseractAndLstm);

        public void Dispose() {
            EngineInstance.Dispose();
        }

        public List<OcrResult> GetResult( Mat m, bool onlyNumbers = false ) {
            if ( onlyNumbers ) {
                EngineInstance.SetVariable("classify_bln_numeric_mode", "1");
            }
            else
                EngineInstance.SetVariable("classify_bln_numeric_mode", "0");
            var result = ProcessImage(EngineInstance, m, modePresets);
            return result;
        }

        protected List<OcrResult> ProcessImage(TesseractEngine engine, Mat img, PageSegMode[] presets ) {
            List<OcrResult> list = new List<OcrResult>(presets.Length);
            using ( var ms = img.ToMemoryStream()) {
                Bitmap m = new Bitmap(Bitmap.FromStream(ms));
                foreach ( var mode in presets )
                    using (var px = BitToPix.Instance.Convert(m))
                    using ( var result = engine.Process(px, mode) ) {
                        list.Add(new OcrResult(result.GetText(), result.GetMeanConfidence()));
                        if ( result.GetMeanConfidence() > SAFE_CONFIDENCE )
                            return list;
                    }
            }
            return list;
        }
    }
}
