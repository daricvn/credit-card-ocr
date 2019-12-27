using Numpy;
using OCRConsole.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OCRConsole.Core {
    public class TemplateMatching {
        public string TemplateFontPath = "resources/ocra.png";
        private const string CONTOURS_DICT = "0123456789QWERTYUIOPASDFGHJKLZXCVBNM/\\";
        private const int DIGIT_MAX_INDEX = 9;
        private List<Mat> _dict;
        private const float MIN_CONFIDENCE = 0.31f;

        public enum MatchingType {
            ALL,
            NUMERIC,
            TEXT
        }
        public void FillDictionary() {
            Mat mat = new Mat(TemplateFontPath);
            mat=mat.CvtColor(ColorConversionCodes.BGR2GRAY);
            mat=mat.Threshold(10, 255, ThresholdTypes.BinaryInv);
            var cnts = Cv2.FindContoursAsMat(mat, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            var list = new List<Mat<Point>>(cnts);
            list = list.OrderBy(x => x.BoundingRect().X).ToList(); // Sort by left - to - right
            Rect r;
            _dict = new List<Mat>(list.Count);
            foreach ( var c in list ) {
                r = c.BoundingRect();
                var roi = new Mat(mat, r);
                roi = roi.Resize(new Size(57, 88));
                _dict.Add(roi);
            }
        }
        

        public OcrResult Match(Mat source, MatchingType type, BoundRatio? bound =null, OpenCvSharp.Range? width=null, OpenCvSharp.Range? height=null, float minAspectRatio=0f, bool processImage = true, int padding=3, List<Rect> boxes=null) {
            if ( _dict == null )
                this.FillDictionary();
            if ( bound == null )
                bound = new BoundRatio(0, 0, 1, 1);
            if ( width == null )
                width = new OpenCvSharp.Range(0, 10000);
            if ( height == null )
                height = new OpenCvSharp.Range(0, 10000); 
            var rectKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(9, 3));
            var sqKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
            var mat = EAST.Resize(source.Clone(), 366);
            var w = mat.Width;
            var h = mat.Height;
            var gray = mat.CvtColor(ColorConversionCodes.BGR2GRAY);
            Mat filtered;
            if ( processImage ) {
                var tophat = gray.MorphologyEx(MorphTypes.TopHat, rectKernel);
                var gradX = tophat.Sobel(MatType.CV_32F, 1, 0, -1);
                float[,] data;
                gradX.GetRectangularArray(out data);
                NDarray grad = np.array(data);
                grad = np.absolute(grad);
                var (min, max) = (grad.min(), grad.max());
                var newGrad = (255 * ((grad - min) / (max - min)));
                newGrad = newGrad.astype(np.uint8);
                var outputData = newGrad.GetData<float>();
                var result = Make2DArray(outputData, data.GetLength(0), data.GetLength(1));
                filtered = new Mat(data.GetLength(0), data.GetLength(1), MatType.CV_8U, result);
                filtered = filtered.MorphologyEx(MorphTypes.Close, rectKernel);
                filtered = filtered.Threshold(0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                filtered = filtered.MorphologyEx(MorphTypes.Close, sqKernel);
            }
            else {
                filtered = EdgeFilter.Process(mat);
                Cv2.BitwiseNot(filtered, filtered);
            }
            List<Rect> locs;
            Rect r;
            if ( boxes == null ) {
                var cnts = Cv2.FindContoursAsMat(filtered, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                locs = new List<Rect>(cnts.Length);
                foreach ( var c in cnts ) {
                    r = c.BoundingRect();
                    var ar = r.Width / (float)r.Height;
                    var xar = r.X / (float)w;
                    var yar = r.Y / (float)h;
                    if ( ar > minAspectRatio && xar >= bound.Value.MinBoundX && xar <= bound.Value.MaxBoundX
                        && yar >= bound.Value.MinBoundY && yar <= bound.Value.MaxBoundY ) {
                        if ( IsInRange(r.Width, width.Value) && IsInRange(r.Height, height.Value) ) {
                            locs.Add(r);
                        }
                    }
                }
            }
            else {
                locs = boxes.Select(x=>EAST.Resize(x, source.Width, 366)).ToList();
            }
            locs = locs.OrderBy(x => x.X).ToList();
            List<Mat<Point>> digitCnts;
            List<double> scores = new List<double>();
            StringBuilder output = new StringBuilder();
            bool valid = false;
            int count = 0;
            double sumScores = 0;
            foreach (var rect in locs ) {
                StringBuilder groupOutput = new StringBuilder();
                var group = new Mat(gray, ExpandRect(rect, padding));
                group.SaveImage(string.Format("test_{0}.png",rect.X));
                group = group.Threshold(0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                digitCnts = Cv2.FindContoursAsMat(group.Clone(), RetrievalModes.External, ContourApproximationModes.ApproxTC89KCOS).ToList();
                digitCnts = digitCnts.OrderBy(x => x.BoundingRect().X).ToList(); // Sort by left to right
                valid = false;
                foreach (var c in digitCnts ) {
                    r = c.BoundingRect();
                    var roi = new Mat(group, r);
                    roi = roi.Resize(new Size(60, 91));
                    scores.Clear();
                    for (var i=0; i<_dict.Count; i++ ) {
                        var matResult = roi.MatchTemplate(_dict[i], TemplateMatchModes.CCoeffNormed);
                        matResult.MinMaxLoc(out double _, out double score);
                        scores.Add(score);
                    }
                    if ( scores.Count > 0 ) {
                        var maxScore = scores.Max();
                        if (maxScore>= MIN_CONFIDENCE ) {
                            var index = scores.IndexOf(maxScore);
                            if ( index < CONTOURS_DICT.Length && (
                                type == MatchingType.ALL ||
                                    (type == MatchingType.NUMERIC && index <= DIGIT_MAX_INDEX) ||
                                    (type == MatchingType.TEXT && index > DIGIT_MAX_INDEX)
                                ) ) {
                                valid = true;
                                groupOutput.Append(CONTOURS_DICT[index]);
                            }
                            else valid = false;
                        }
                        else {
                            maxScore = maxScore / 2; // Decrease weight dramastically
                        }
                        sumScores += maxScore;
                        count++;
                    }
                }
                if (valid && groupOutput.Length > 0 ) {
                    if ( output.Length > 0 )
                        output.Append(" ");
                    output.Append(groupOutput.ToString());
                }
            }
            OcrResult ocr = new OcrResult(output.ToString(), (float) (sumScores / count));
            return ocr;
        }

        private static bool IsInRange(int value, OpenCvSharp.Range range ) {
            return value >= range.Start && value <= range.End;
        }
        private static Rect ExpandRect(Rect rect, int v ) {
            var x = rect.X - v;
            var y = rect.Y - v;
            var ex = (rect.Width + rect.X) + v;
            var ey = (rect.Height + rect.Y) + v;
            return new Rect(x, y, ex - x, ey - y);
        }

        private static T[,] Make2DArray<T>( T[] input, int height, int width ) {
            T[,] output = new T[height, width];
            for ( int i = 0; i < height; i++ ) {
                for ( int j = 0; j < width; j++ ) {
                    output[i, j] = input[i * width + j];
                }
            }
            return output;
        }
    }
}
