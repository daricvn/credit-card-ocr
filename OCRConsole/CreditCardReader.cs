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

namespace OCRConsole {
    public class CreditCardReader {
        private readonly BoundRatio NumberArea = new Models.BoundRatio(0.01f, 0.42f, 0.98f, 0.71f);
        private readonly BoundRatio NameArea = new Models.BoundRatio(0.01f, 0.75f, 0.78f, 0.99f);
        /// <summary>
        /// Level of reader
        /// </summary>
        public int Level { get; set; } = 5;
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
            var boxes = EAST.MergeBoxes(EAST.DetectText(origin, NumberArea, 40));
            try {
                this.Completed = false;
                foreach ( var box in boxes ) {
                    var mat = new Mat(origin, box.ToRect());
                    using ( var ms = new MemoryStream() ) {
                        mat.WriteToStream(ms);
                        var list = TryProcess(mat, true);
                        if ( list.Count>0 && list.FirstOrDefault().Confidence>OCR.MIN_CONFIDENCE) {
                            CardNumber = list[0].Text;
                        }
                        if ( DebugMode )
                            DebugImage(mat, null, "name" + mat.GetHashCode());
                    }
                }
                boxes = EAST.MergeBoxes(EAST.DetectText(origin, NameArea, 26));
                foreach ( var box in boxes ) {
                    var mat = new Mat(origin, box.ToRect());
                    using ( var ms = new MemoryStream() ) {
                        mat.WriteToStream(ms);
                        var list = TryProcess(mat);
                        if ( list.Count > 0 && list.FirstOrDefault().Confidence > OCR.MIN_CONFIDENCE ) {
                            if ( list[0].Text!=null && CheckCardName(list[0].Text) )
                                CardName = list[0].Text;
                        }
                        if ( DebugMode )
                            DebugImage(mat, null, "name" + mat.GetHashCode());
                    }
                }
                this.Completed = true;
                OnCompleted?.Invoke(this, new EventArgs());
            }
            catch (Exception e ) {
                Console.WriteLine(e);
                throw e;
            }
        }

        public string CardNumber { get; set; }
        public string CardName { get; set; }


        protected List<OcrResult> TryProcess(Mat m , bool onlyNumbers=false) {
            using ( var OCR = new OCR() ) {
                int lv = 1;
                List<OcrResult> list = new List<OcrResult>();
                list.AddRange(OCR.GetResult(m, onlyNumbers));
                if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                    list.OrderByDescending(x => x.Confidence);
                    return list;
                }
                lv++; // 2
                if ( lv > Level ) return list;
                Cv2.FastNlMeansDenoising(m, m, 9);
                DebugImage(m, null, "denoised" + m.GetHashCode());
                list.AddRange(OCR.GetResult(m, onlyNumbers));
                if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                    list.OrderByDescending(x => x.Confidence);
                    return list;
                }
                lv++; // 3
                if ( lv > Level ) return list;
                var tmpMat = m.Clone();
                m = EdgeFilter.Process(m);
                DebugImage(m, null, "gray" + m.GetHashCode());
                list.AddRange(OCR.GetResult(m, onlyNumbers));
                if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                    list.OrderByDescending(x => x.Confidence);
                    return list;
                }
                lv++; // 4
                if ( lv > Level ) return list;
                m.Dilate(new Mat(3, 3, MatType.CV_8U));
                DebugImage(m, null, "dilate" + m.GetHashCode());
                list.AddRange(OCR.GetResult(m, onlyNumbers));
                if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                    list.OrderByDescending(x => x.Confidence);
                    return list;
                }
                m.Erode(new Mat(6, 6, MatType.CV_8U));
                DebugImage(m, null, "erode" + m.GetHashCode());
                list.AddRange(OCR.GetResult(m, onlyNumbers));
                if ( list.FirstOrDefault(x => x.Confidence > OCR.SAFE_CONFIDENCE) != null ) {
                    list.OrderByDescending(x => x.Confidence);
                    return list;
                }
                lv++; // 5
                if ( lv > Level ) return list;
                var edged = EdgeFilter.Filter(MatToBitmap(tmpMat), FilterMatrix.Sobel3x3Horizontal, FilterMatrix.Sobel3x3Vertical, 87);
                var fm = BitmapToMat(edged);
                DebugImage(fm, null, "edge" + fm.GetHashCode());
                list.AddRange(OCR.GetResult(fm, onlyNumbers));
                list.OrderByDescending(x => x.Confidence);
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
    }
}
