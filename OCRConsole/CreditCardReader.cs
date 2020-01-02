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
        private readonly BoundRatio NumberArea = new Models.BoundRatio(0.00f, 0.35f, 0.98f, 0.71f);
        private readonly BoundRatio NameArea = new Models.BoundRatio(0.00f, 0.55f, 0.78f, 0.99f);
        private BoundRatio DateArea = new Models.BoundRatio(0.05f, 0.50f, 0.85f, 0.88f);
        private readonly float MinNumberAR = 4f;
        private readonly float MinNameAR = 2.35f;
        private readonly float MinDateAR = 1.7f;
        private readonly string OCR_A = "resources/ocra.png";
        private readonly string OCR_B = "resources/ocrb.png";
        /// <summary>
        /// Level of reader
        /// </summary>
        public int Level { get; set; } = 5;
        /// <summary>
        /// Padding level when picking the number
        /// </summary>
        public int Padding { get; set; } = 4;
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
            //DebugImage(SWT.DetectText(EAST.ResizeH(origin, 176), true), null, "swt");
            var allBoxes = EAST.DetectText(origin, NumberArea, 18, Padding);
            if ( DebugMode )
                DebugImage(origin, allBoxes, "area");
            var tmpBoxes = allBoxes.Select(x => x.ToRect()).ToList();
            var boxes = EAST.MergeBoxes(allBoxes).OrderBy(x=>x.AY).ToHashSet();
            List<Rect> filteredBoxes;
            OCR.DebugMode = DebugMode;
            OCR.DebugPath = DebugPath;
            var tm = new TemplateMatching();
            try {
                this.Completed = false;
                LastNamConfidence = 0;
                float ar;
                foreach ( var box in boxes ) {
                    ar = box.Width / (float)box.Height;
                    if ( ar > MinNumberAR ) {
                        filteredBoxes = tmpBoxes.Where(x => x.Y == box.AY).OrderBy(x=>x.X).ToList();
                        using ( var ocr = new OCR() ) {
                            // First card number should need more prefix padding.
                            if (filteredBoxes.FirstOrDefault()!=null) {
                                var fb = filteredBoxes.First();
                                fb.X = fb.X <= 5 ? 1 : fb.X - 5;
                                fb.Width = fb.Width + 4;
                                filteredBoxes[0] = fb;
                            }
                            var list = ocr.Process(origin, filteredBoxes.ToArray(), true);
                            if (list.Count>0 && list.Select(x => x.Confidence).Max() < OCR.SAFE_CONFIDENCE ) {
                                var matches = TryMatch(tm, origin, TemplateMatching.MatchingType.NUMERIC, new OpenCvSharp.Range(10, 220), new OpenCvSharp.Range(14, 38), boxes: filteredBoxes);
                                list.AddRange(matches);
                                //list = list?.OrderByDescending(x => x.Confidence).ToList();
                            }
                            var first = list.OrderByDescending(x=> CheckCardNumber(x.Text)).ThenByDescending(x=>x.Confidence).FirstOrDefault();
                            if ( ContainsNumber(first.Text) && (CheckCardNumber(first.Text) || string.IsNullOrWhiteSpace(CardNumber)) ) {
                                CardNumber = first.Text; //string.Join(" ", words);
                                MinDateY = box.BY - Padding;
                            }
                        }
                    }
                }
                allBoxes = EAST.DetectText(origin, NameArea, 21, padding: Padding);
                if (allBoxes.Count == 0 ) 
                    allBoxes = EAST.DetectText(origin, NameArea, 16, padding: Padding);
                if ( DebugMode )
                    DebugImage(origin, allBoxes, "areaname");
                tmpBoxes = allBoxes.Select(x => x.ToRect()).ToList();
                boxes = EAST.MergeBoxes(allBoxes);
                foreach ( var box in boxes ) {
                    ar = box.Width / (float)box.Height;
                    if ( ar > MinNameAR ) {
                        filteredBoxes = tmpBoxes.Where(x => x.Y == box.AY).OrderBy(x => x.X).ToList();
                        using ( var ocr = new OCR() ) {
                            var list = ocr.Process(origin, filteredBoxes.ToArray(), true);
                            if ( list.Count > 0 && list.Select(x => x.Confidence).Max() < OCR.SAFE_CONFIDENCE ) {
                                var matches = TryMatch(tm, origin, TemplateMatching.MatchingType.TEXT, new OpenCvSharp.Range(6, 280), new OpenCvSharp.Range(6, 38), boxes: filteredBoxes);
                                list.AddRange(matches);
                                list = list?.OrderByDescending(x => x.Confidence).ToList();
                            }
                            var first= list.FirstOrDefault(); //string.Join(" ", words);
                            if (first !=null && first.Confidence > LastNamConfidence && CheckCardName(first.Text)) {
                                CardName = first.Text.TrimStart().TrimEnd();
                                LastNamConfidence = first.Confidence;
                                MaxDateY = box.AY + Padding;
                                if ( first.Confidence > OCR.SAFE_CONFIDENCE )
                                    LastNamConfidence *= 1.03f;
                            }
                        }
                    }
                }

                // Read date
                DateArea.MinBoundY = (MinDateY / (float)origin.Height) - 0.02f;
                DateArea.MaxBoundY = (MaxDateY / (float)origin.Height) + 0.02f;
                var dateBoxes = EAST.DetectText(origin, DateArea, 11, padding: Padding).OrderBy(x=>x.AX).ToList();
                if ( DebugMode )
                    DebugImage(origin, allBoxes, "date");
                //boxes = EAST.MergeBoxes(allBoxes);
                for ( var i=0; i< dateBoxes.Count; i++ ) {
                    var box = dateBoxes[i];
                    if (i< dateBoxes.Count - 1 ) {
                        if ( box.BX > (dateBoxes[i + 1].AX*1.1) && box.AY == dateBoxes[i + 1].AY )
                            box.BX = dateBoxes[i + 1].BX;
                    }
                    ar = box.Width / (float)box.Height;
                    if ( ar > MinDateAR && box.AY >= MinDateY && box.BY <= MaxDateY ) {
                        if ( DebugMode )
                            DebugImage(origin, box, "date_"+i);
                        //filteredBoxes = tmpBoxes.Where(x => x.Y == box.AY).OrderBy(x => x.X).ToList();
                        using ( var ocr = new OCR() ) {
                            ocr.SetBlacklistCharacters("|");
                            var list = ocr.Process(origin, new Rect[] { box.ToRect() }, false, x => CheckCardDate(x.Text));
                            list = list.OrderByDescending(x => CheckCardDate(x.Text)).ThenByDescending(x => x.Confidence).ToList();
                            var first = list.FirstOrDefault(); //string.Join(" ", words);
                            if ( first != null && first.Confidence >= OCR.MIN_CONFIDENCE && CheckCardDate(first.Text) ) {
                                CardDate = first.Text.TrimStart().TrimEnd();
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
        public string CardDate { get; private set; }
        private float LastNamConfidence { get; set; }
        private int MinDateY { get; set; }
        private int MaxDateY { get; set; }
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

        private static void DebugImage( Mat target, HashSet<PointRect> boxes, string additional ) {
            var originPath = DebugPath;
            var img = target.Clone();
            if (boxes !=null)
                foreach ( var box in boxes )
                    Cv2.Rectangle(img, box.ToRect(), new Scalar(0, 255, 0), 3);
            var targetPath = System.IO.Path.Join(originPath, System.IO.Path.GetFileNameWithoutExtension(originPath) + "_" + additional + ".png");
            img.SaveImage(targetPath);
        }

        private static void DebugImage( Mat target, PointRect box, string additional ) {
            var originPath = DebugPath;
            var img = target.Clone();
            if (box!=null)
               Cv2.Rectangle(img, box.ToRect(), new Scalar(0, 255, 0), 3);
            var targetPath = System.IO.Path.Join(originPath, System.IO.Path.GetFileNameWithoutExtension(originPath) + "_" + additional + ".png");
            img.SaveImage(targetPath);
        }

        private bool CheckCardNumber(string text ) {
            var regex = new Regex("[0-9]{1,5} [0-9]{1,5} [0-9]{1,5} [0-9]{1,5}");
            return text.Length>8 && regex.IsMatch(text);
        }
        private bool CheckCardName( string text ) {
            var regex = new Regex("[a-zA-Z'.]+", RegexOptions.IgnoreCase);
            return regex.IsMatch(text) && text.Trim().Length>2;
        }
        private bool CheckCardDate( string text ) {
            var regex = new Regex("[0-9]{1,2}[\\/][0-9]{1,4}", RegexOptions.IgnoreCase);
            return regex.IsMatch(text) && text.Trim().Length > 2;
        }
        private bool ContainsNumber(string text ) {
            foreach ( char c in text )
                if ( char.IsDigit(c) )
                    return true;
            return false;
        }
    }
}
