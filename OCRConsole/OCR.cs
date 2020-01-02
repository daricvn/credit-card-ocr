using OCRConsole.Core;
using OCRConsole.Models;
using OCRConsole.Utility;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Tesseract;

namespace OCRConsole {
    public class OCR: IDisposable {
        public static int Level = 5;
        static internal bool DebugMode { private get; set; } = false;
        static internal string DebugPath { set; private get; }

        public const float MIN_CONFIDENCE = 0.5f;
        public const float SAFE_CONFIDENCE = 0.84f;
        private static readonly PageSegMode[] modePresets = new PageSegMode[] { PageSegMode.SingleBlock, PageSegMode.SingleLine, PageSegMode.SingleWord, PageSegMode.RawLine };

        private TesseractEngine EngineInstance = new TesseractEngine(@"./tessdata", "credit+credit2+eng", EngineMode.TesseractAndLstm);

        public void Dispose() {
            EngineInstance.Dispose();
        }
        public void SetBlacklistCharacters( string value ) {
            EngineInstance.SetVariable("tessedit_char_blacklist", value);
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


        public List<OcrResult> Process(Mat origin, OpenCvSharp.Rect[] alignedBoxes, bool onlyNumbers = false , bool safeMode=true) {
            var aligned = alignedBoxes.OrderBy(x => x.Y).ThenBy(x => x.X).ToList();
            var line = aligned[0].Y;
            var result = new OcrResult("", 0);
            var count = 0;
            List<string> words = new List<string>();

            foreach ( var box in aligned ) {
                count++;
                var mat = origin[box];
                var list = this.TryProcess(mat, onlyNumbers);
                list = list.OrderByDescending(x => x.Confidence).ToList();
                if ( list.Count > 0 && (!safeMode || list.Any(x => x.Confidence > MIN_CONFIDENCE)) ) {
                    var first = list.First();
                    words.Add(first.Text.TrimEnd());
                    if ( first.Confidence >= SAFE_CONFIDENCE + 0.01f )
                        result.Confidence += first.Confidence;
                    else
                        result.Confidence += (float) (first.Confidence*0.5);
                }
            }
            result.Text = string.Join(" ", words);
            result.Confidence /= (float) count;
            var ocrList = new List<OcrResult>(2);
            ocrList.Add(result);
            if ( safeMode && result.Confidence > SAFE_CONFIDENCE )
                return ocrList;
            ocrList.Add(ProcessBlock(origin, alignedBoxes, onlyNumbers, safeMode));
            ocrList = ocrList.OrderByDescending(x => x.Confidence).ToList();
            return ocrList;
        }

        public OcrResult ProcessBlock(Mat origin, OpenCvSharp.Rect[] alignedBoxes, bool onlyNumbers = false, bool safeMode = true, int preferedIndex = 0) {
            var aligned = alignedBoxes.OrderBy(x => x.Y).ThenBy(x=>x.X);
            var mergedBlocks = EAST.MergeBoxes(CreatePointList(aligned.ToArray()));
            var result = new OcrResult("", 0);
            var count = 0;
            var index = 0;
            foreach (var block in mergedBlocks ) {
                count++;
                var mat = origin[block.ToRect()];
                var list = this.TryProcess(mat, onlyNumbers);
                list = list.OrderByDescending(x => x.Confidence).ToList();
                if ( list.Count > 0 && (!safeMode || list.Any(x=>x.Confidence> MIN_CONFIDENCE))) {
                    if ( list.Count <= preferedIndex )
                        index = preferedIndex;
                    result.Text += list[index]?.Text;
                    result.Confidence += list[index].Confidence;
                }
            }
            result.Confidence = result.Confidence / (float)count;
            return result;
        }

        public List<OcrResult> Process<TKey>( Mat origin, OpenCvSharp.Rect[] alignedBoxes, bool onlyNumbers = false, Func<OcrResult, TKey> expectedOutput = null ) {
            var aligned = alignedBoxes.OrderBy(x => x.Y).ThenBy(x => x.X).ToList();
            var line = aligned[0].Y;
            var result = new OcrResult("", 0);
            var count = 0;
            List<string> words = new List<string>();

            foreach ( var box in aligned ) {
                count++;
                var mat = origin[box];
                var list = this.TryProcess(mat, onlyNumbers);
                list = list.OrderByDescending(expectedOutput).ThenByDescending(x => x.Confidence).ToList();
                if ( list.Count > 0 && (list.Any(x => x.Confidence > MIN_CONFIDENCE)) ) {
                    var first = list.First();
                    words.Add(first.Text.TrimEnd());
                    if ( first.Confidence >= SAFE_CONFIDENCE + 0.01f )
                        result.Confidence += first.Confidence;
                    else
                        result.Confidence += (float)(first.Confidence * 0.5);
                }
            }
            result.Text = string.Join(" ", words);
            result.Confidence /= (float)count;
            var ocrList = new List<OcrResult>(2);
            ocrList.Add(result);
            if ( result.Confidence > SAFE_CONFIDENCE )
                return ocrList;
            ocrList.Add(ProcessBlock(origin, alignedBoxes, onlyNumbers, expectedOutput));
            ocrList = ocrList.OrderByDescending(x => x.Confidence).ToList();
            return ocrList;
        }
        public OcrResult ProcessBlock<TKey>( Mat origin, OpenCvSharp.Rect[] alignedBoxes, bool onlyNumbers = false, Func<OcrResult, TKey> expectedOutput=null ) {
            var aligned = alignedBoxes.OrderBy(x => x.Y).ThenBy(x => x.X);
            var mergedBlocks = EAST.MergeBoxes(CreatePointList(aligned.ToArray()));
            var result = new OcrResult("", 0);
            var count = 0;
            var index = 0;
            foreach ( var block in mergedBlocks ) {
                count++;
                var mat = origin[block.ToRect()];
                var list = this.TryProcess(mat, onlyNumbers);
                list = list.OrderByDescending(expectedOutput).ThenByDescending(x => x.Confidence).ToList();
                if ( list.Count > 0 && (list.Any(x => x.Confidence > MIN_CONFIDENCE)) ) {
                    result.Text += list[index]?.Text;
                    result.Confidence += list[index].Confidence;
                }
            }
            result.Confidence = result.Confidence / (float)count;
            return result;
        }

        public List<OcrResult> TryProcess( Mat m, bool onlyNumbers = false ) {
            //using ( var OCR = new OCR() ) {
            int lv = 0;
            List<OcrResult> list = new List<OcrResult>();
            list.AddRange(this.GetResult(m, onlyNumbers));
            if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                return list;
            }
            lv++; // 1
            if ( lv > Level ) return list;
            Cv2.FastNlMeansDenoising(m, m, 9);
            if ( DebugMode )
                DebugImage(m, null, "denoised" + m.GetHashCode());
            list.AddRange(this.GetResult(m, onlyNumbers));
            if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                return list;
            }
            lv++; // 2
            if ( lv > Level ) return list;
            var tmpMat = m.Clone();
            var gray = m.CvtColor(ColorConversionCodes.BGR2GRAY);
            m = EdgeFilter.Process(m);
            if ( DebugMode )
                DebugImage(m, null, "gray" + m.GetHashCode());
            list.AddRange(this.GetResult(m, onlyNumbers));
            if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                return list;
            }
            lv++; // 3
            if ( lv > Level ) return list;
            m.Dilate(new Mat(3, 3, MatType.CV_8U));
            if ( DebugMode )
                DebugImage(m, null, "dilate" + m.GetHashCode());
            list.AddRange(this.GetResult(m, onlyNumbers));
            if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                return list;
            }
            m.Erode(new Mat(6, 6, MatType.CV_8U));
            if ( DebugMode )
                DebugImage(m, null, "erode" + m.GetHashCode());
            list.AddRange(this.GetResult(m, onlyNumbers));
            if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                return list;
            }
            lv++; // 4
            if ( lv > Level ) return list;
            var edged = gray.Canny(40, 90).Dilate(null, iterations: 1);//EdgeFilter.Filter(ImLib.MatToBitmap(tmpMat), FilterMatrix.Sobel3x3Horizontal, FilterMatrix.Sobel3x3Vertical, 87);
            if ( DebugMode )
                DebugImage((edged), null, "edge" + edged.GetHashCode());
            list.AddRange(this.GetResult(edged, onlyNumbers));
            lv++; // 5
            if ( lv > Level ) return list;
            var sobel = EdgeFilter.Filter(ImLib.MatToBitmap(gray), FilterMatrix.Sobel3x3Horizontal, FilterMatrix.Sobel3x3Vertical, 87);
            var fm = ImLib.BitmapToMat(sobel);
            if ( DebugMode )
                DebugImage((fm), null, "sobel" + fm.GetHashCode());
            list.AddRange(this.GetResult(fm, onlyNumbers));

            return list;
            //}
        }

        private Mat RedrawContours(Mat input) {
            var mask = new Mat(input.Size(), MatType.CV_8U);
            var cnts = Cv2.FindContoursAsMat(input, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            foreach ( var c in cnts ) {
                if ( Cv2.ContourArea(c as Mat) < 200 )
                    Cv2.DrawContours(mask, new Mat[] { c }, -1, 0, -1);
                var r = c.BoundingRect();
                if (r.Width > r.Height )
                    Cv2.DrawContours(mask, new Mat[] { c }, -1, 0, -1);
            }
            var result = new Mat();
            Cv2.BitwiseAnd(input.Clone(), input.Clone(), result, mask: mask);
            return result;
        }   

        private void DebugImage( Mat target, HashSet<PointRect> boxes, string additional ) {
            var originPath = DebugPath;
            var img = target.Clone();
            if ( boxes != null )
                foreach ( var box in boxes )
                    Cv2.Rectangle(img, box.ToRect(), new Scalar(0, 255, 0), 3);
            var targetPath = System.IO.Path.Join(originPath, System.IO.Path.GetFileNameWithoutExtension(originPath) + "_" + additional + ".png");
            img.SaveImage(targetPath);
        }

        private HashSet<PointRect> CreatePointList( OpenCvSharp.Rect[] rects ) {
            HashSet<PointRect> list = new HashSet<PointRect>(rects.Length);
            foreach ( var rect in rects )
                list.Add(new PointRect(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height));
            return list;
        }
    }
}
