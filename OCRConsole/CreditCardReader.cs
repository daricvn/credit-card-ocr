using OCRConsole.Core;
using OCRConsole.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OCRConsole {
    public class CreditCardReader {
        private readonly BoundRatio NumberArea = new Models.BoundRatio(0.01f, 0.42f, 0.98f, 0.71f);
        private readonly BoundRatio NameArea = new Models.BoundRatio(0.01f, 0.75f, 0.78f, 0.99f);
        private readonly float MinNumberAR = 5.5f;
        private readonly float MinNameAR = 2f;
        private readonly string OCR_A = "resources/ocra.png";
        private readonly string OCR_B = "resources/ocrb.png";
        /// <summary>
        /// Level of reader
        /// </summary>
        public int Level { get; set; } = 5;
        /// <summary>
        /// Padding level when picking the number
        /// </summary>
        public int Padding { get; set; } = 8;
        public CreditCardReader() {

        }
        public CreditCardReader(string source ) {
            ImageSource = source;
        }
        public string ImageSource { get; set; }
        public bool Completed { get; private set; } = false;
        public event EventHandler OnCompleted;

        static internal bool DebugMode { private get; set; } = false;
        static internal string DebugPath { set; private get; }
        public void Process(string imageSource = null) {
            if ( imageSource == null && !string.IsNullOrEmpty(ImageSource) )
                imageSource = ImageSource;
            Mat origin = EAST.Resize(new Mat(imageSource, ImreadModes.Color), 500);
            var allBoxes = EAST.DetectText(origin, NumberArea, 40, Padding);
            var tmpBoxes = allBoxes.Select(x => x.ToRect()).ToList();
            var boxes = EAST.MergeBoxes(allBoxes);
            List<Rect> filteredBoxes;
            var tm = new TemplateMatching();
            try {
                this.Completed = false;
                float ar;
                foreach ( var box in boxes ) {
                    ar = box.Width / (float)box.Height;
                    if ( ar > MinNumberAR ) {
                        filteredBoxes = tmpBoxes.Where(x => x.Y == box.AY).OrderBy(x=>x.X).ToList();
                        List<string> words = new List<string>(filteredBoxes.Count);
                        foreach ( var b in filteredBoxes ) {
                            var mat = new Mat(origin, b);
                            var list = TryProcess(mat, true);
                            list = list?.OrderByDescending(x => x.Confidence).ToList();
                            if ( list.Count > 0 ) {
                                if ( list.Select(x => x.Confidence).Max() < OCR.SAFE_CONFIDENCE ) {
                                    var matches = TryMatch(tm, origin, TemplateMatching.MatchingType.NUMERIC, new OpenCvSharp.Range(10, 220), new OpenCvSharp.Range(14, 38), boxes: b);
                                    list.AddRange(matches);
                                    list = list?.OrderByDescending(x => x.Confidence).ToList();
                                }
                                if ( list.FirstOrDefault().Confidence > OCR.MIN_CONFIDENCE ) {
                                    words.Add(list[0].Text.TrimStart().TrimEnd());
                                }
                            }
                        }
                        CardNumber = string.Join(" ", words);
                    }
                }
                allBoxes = EAST.DetectText(origin, NameArea, 26);
                if (allBoxes.Count == 0 ) 
                    allBoxes = EAST.DetectText(origin, NameArea, 20);
                tmpBoxes = allBoxes.Select(x => x.ToRect()).ToList();
                boxes = EAST.MergeBoxes(allBoxes);
                foreach ( var box in boxes ) {
                    ar = box.Width / (float)box.Height;
                    if ( ar > MinNameAR ) {
                        filteredBoxes = tmpBoxes.Where(x => x.Y == box.AY).OrderBy(x => x.X).ToList();
                        var mat = new Mat(origin, box.ToRect());
                        var list = TryProcess(mat);
                        list = list?.OrderByDescending(x => x.Confidence).ToList();
                        if ( list.Count > 0 ) {
                            if ( list.Select(x => x.Confidence).Max() < OCR.SAFE_CONFIDENCE ) {
                                var matches = TryMatch(tm, origin, TemplateMatching.MatchingType.TEXT, new OpenCvSharp.Range(6, 280), new OpenCvSharp.Range(6, 38), filteredBoxes);
                                list.AddRange(matches);
                                list = list?.OrderByDescending(x => x.Confidence).ToList();
                            }
                            if ( list.FirstOrDefault().Confidence > OCR.MIN_CONFIDENCE ) {
                                if ( list[0].Text != null && CheckCardName(list[0].Text) )
                                    CardName = list[0].Text.TrimStart().TrimEnd() ;
                            }
                        }
                    }
                }
                this.Completed = true;
                OnCompleted?.Invoke(this, new EventArgs());
            }
            catch (Exception e ) {
                throw e;
            }
        }

        public string CardNumber { get; private set; }
        public string CardName { get; private set; }
        protected List<OcrResult> TryMatch( TemplateMatching tm, Mat mat, TemplateMatching.MatchingType type, OpenCvSharp.Range width, OpenCvSharp.Range height,Rect boxes ) {
            List<Rect> list = new List<Rect>(1);
            list.Add(boxes);
            return TryMatch(tm, mat, type, width, height, list);
        }
        protected List<OcrResult> TryMatch(TemplateMatching tm, Mat mat, TemplateMatching.MatchingType type, OpenCvSharp.Range width, OpenCvSharp.Range height, List<Rect> boxes ) {
            tm.TemplateFontPath = OCR_A;
            tm.FillDictionary();
            List<OcrResult> result = new List<OcrResult>();
            result.Add(tm.Match(mat, type, null, width, height, processImage: false, padding:-1, boxes: boxes));
            if ( result.Select(x => x.Confidence).Max() > OCR.SAFE_CONFIDENCE )
                return result;
            tm.TemplateFontPath = OCR_B;
            tm.FillDictionary();
            result.Add(tm.Match(mat, type, null, width, height, processImage: false, padding: -1, boxes: boxes));
            return result;
        }

        protected List<OcrResult> TryProcess(Mat m , bool onlyNumbers=false) {
            using ( var OCR = new OCR() ) {
                int lv = 1;
                List<OcrResult> list = new List<OcrResult>();
                list.AddRange(OCR.GetResult(m, onlyNumbers));
                if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                    return list;
                }
                lv++; // 2
                if ( lv > Level ) return list;
                Cv2.FastNlMeansDenoising(m, m, 9);
                if ( DebugMode )
                    DebugImage(m, null, "denoised" + m.GetHashCode());
                list.AddRange(OCR.GetResult(m, onlyNumbers));
                if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                    return list;
                }
                lv++; // 3
                if ( lv > Level ) return list;
                var tmpMat = m.Clone();
                m = EdgeFilter.Process(m);
                if ( DebugMode )
                    DebugImage(m, null, "gray" + m.GetHashCode());
                list.AddRange(OCR.GetResult(m, onlyNumbers));
                if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                    return list;
                }
                lv++; // 4
                if ( lv > Level ) return list;
                m.Dilate(new Mat(3, 3, MatType.CV_8U));
                if ( DebugMode )
                    DebugImage(m, null, "dilate" + m.GetHashCode());
                list.AddRange(OCR.GetResult(m, onlyNumbers));
                if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                    return list;
                }
                m.Erode(new Mat(6, 6, MatType.CV_8U));
                if ( DebugMode )
                    DebugImage(m, null, "erode" + m.GetHashCode());
                list.AddRange(OCR.GetResult(m, onlyNumbers));
                if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                    return list;
                }
                lv++; // 5
                if ( lv > Level ) return list;
                var edged = EdgeFilter.Filter(MatToBitmap(tmpMat), FilterMatrix.Sobel3x3Horizontal, FilterMatrix.Sobel3x3Vertical, 87);
                var fm = BitmapToMat(edged);
                if ( DebugMode )
                    DebugImage(fm, null, "edge" + fm.GetHashCode());
                list.AddRange(OCR.GetResult(fm, onlyNumbers));
                return list;
            }
        }

        private static void DebugImage( Mat target, HashSet<PointRect> boxes, string additional ) {
            var originPath = DebugPath;
            var img = target.Clone();
            if (boxes !=null)
                foreach ( var box in boxes )
                    Cv2.Rectangle(img, box.ToRect(), new Scalar(0, 255, 0), 3);
            var targetPath = System.IO.Path.Join(System.IO.Path.GetDirectoryName(originPath), "result\\", System.IO.Path.GetFileNameWithoutExtension(originPath) + "_" + additional + System.IO.Path.GetExtension(originPath));
            img.SaveImage(targetPath);
        }

        private bool CheckCardNumber(string text ) {
            var regex = new Regex("[0-9]{0,4} [0-9]{0,4} [0-9]{0,4} [0-9]{0,4}");
            return regex.IsMatch(text);
        }
        private bool CheckCardName( string text ) {
            var regex = new Regex("[a-zA-Z'. ]+", RegexOptions.IgnoreCase);
            return regex.IsMatch(text);
        }

        private Bitmap MatToBitmap(Mat m ) {
            using ( var ms = m.ToMemoryStream() )
                return new Bitmap(Bitmap.FromStream(ms));
        }
        private Mat BitmapToMat(Bitmap m ) {
            using (var ms = new MemoryStream() ) {
                m.Save(ms, ImageFormat.Jpeg);
                return Mat.FromImageData(ms.ToArray());
            }
        }

        private HashSet<T> Clone<T>(HashSet<T> source) {
            T[] rects = new T[source.Count];
            source.CopyTo(rects);
            return rects.ToHashSet();
        }
    }
}
