using NumSharp;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace OCRConsole.Utility {
    public class ImLib {
        public static Mat EmbossedFilter(Mat source , OpenCvSharp.Range? canny=null) {
            if ( canny == null )
                canny = new OpenCvSharp.Range(10, 60);
            // Convert to gray
            var mat = source.Clone();
            mat.Resize(512);
            var gray = mat.CvtColor(ColorConversionCodes.BGR2GRAY);
            var img = gray.GaussianBlur(new OpenCvSharp.Size(7, 7), 0);
            var edged = img.Canny(canny.Value.Start, canny.Value.End);
            var dilate = edged.Dilate(null, iterations: 1);
            dilate.SaveImage("D:/test.png");
            var mask = new Mat(mat.Size(), MatType.CV_8U);
            mask.SetTo(255);

            var cnts = Cv2.FindContoursAsMat(dilate.Clone(), RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            foreach (var c in cnts ) {
                if ( Cv2.ContourArea(c as Mat) < 260 )
                    Cv2.DrawContours(mask, new Mat[] { c }, -1, 0, -1);
                var r = c.BoundingRect();
                if (r.Width> r.Height )
                    Cv2.DrawContours(mask, new Mat[] { c }, -1, 0, -1);
            }
            var im = new Mat();
            Cv2.BitwiseAnd(dilate.Clone(), dilate.Clone(), im, mask);
            //var imd = im.Dilate(null, iterations: 1);
            var th = im.Threshold(10, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
            return th;
        }

        public static Mat CreateMat<T>(NDArray im, MatType type) where T : unmanaged {
            var input = im.ToMuliDimArray<T>();
            var mat = new Mat(input.GetLength(0), input.GetLength(1), type, input);
            return mat;
        }

        public static T[,] Make2DArray<T>( T[] input, int height, int width ) {
            T[,] output = new T[height, width];
            for ( int i = 0; i < height; i++ ) {
                for ( int j = 0; j < width; j++ ) {
                    output[i, j] = input[i * width + j];
                }
            }
            return output;
        }
        public static bool IsInRange( int value, OpenCvSharp.Range range ) {
            return value >= range.Start && value <= range.End;
        }

        public static Mat BitmapToMat( Bitmap m ) {
            using ( var ms = new MemoryStream() ) {
                m.Save(ms, ImageFormat.Jpeg);
                return Mat.FromImageData(ms.ToArray());
            }
        }
        public static Bitmap MatToBitmap( Mat m ) {
            using ( var ms = m.ToMemoryStream() )
                return new Bitmap(Bitmap.FromStream(ms));
        }
    }
}
