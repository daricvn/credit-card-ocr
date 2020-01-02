using OCRConsole.Models;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OCRConsole {
    class EAST {
        private const int W = 416;
        private const int H = 416;
        private const float ConfidentThreshold = 0.52f;
        private const string EAST_PATH = "resources/east_model.pb";
        /// <summary>
        /// Read text from image.
        /// </summary>
        /// <see cref="https://github.com/opencv/opencv/blob/master/samples/dnn/text_detection.cpp"/>
        /// <param name="fileName">Name of the image file.</param>
        /// <returns>Scanned text.</returns>
        public static HashSet<PointRect> DetectText( Mat img, BoundRatio? bound=null, int minHeight=20, int padding = 10, int heightThreshold = 12) {
            // Load network.
            if ( bound == null )
                bound = new BoundRatio(0, 0, 1, 1);
            using ( Net net = CvDnn.ReadNet(Path.GetFullPath(EAST_PATH)) ){
                int[] originSize = new int[] { img.Width, img.Height };
                float wratio = originSize[0] / (float) W;
                float hratio = originSize[1] / (float) H;
                var proc = Resize(img,W);
                int x;
                int y;
                int ex;
                int ey;
                int h;
                // Prepare input image
                using ( var blob = CvDnn.BlobFromImage(proc, 1.0, new Size(W, H), new Scalar(123.68, 116.78, 103.94), true, false) ) {
                    // Forward Pass
                    // Now that we have prepared the input, we will pass it through the network. There are two outputs of the network.
                    // One specifies the geometry of the Text-box and the other specifies the confidence score of the detected box.
                    // These are given by the layers :
                    //   feature_fusion/concat_3
                    //   feature_fusion/Conv_7/Sigmoid
                    var outputBlobNames = new string[] { "feature_fusion/Conv_7/Sigmoid", "feature_fusion/concat_3" };
                    var outputBlobs = outputBlobNames.Select(_ => new Mat()).ToArray();

                    net.SetInput(blob);
                    net.Forward(outputBlobs, outputBlobNames);
                    Mat scores = outputBlobs[0];
                    Mat geometry = outputBlobs[1];

                    // Decode predicted bounding boxes (decode the positions of the text boxes along with their orientation)
                    Decode(scores, geometry, ConfidentThreshold, out var boxes, out var confidences);

                    // Apply non-maximum suppression procedure for filtering out the false positives and get the final predictions
                    CvDnn.NMSBoxes(boxes, confidences, ConfidentThreshold, 0.3f, out var indices);

                    // Render detections.
                    //Point2f ratio = new Point2f((float)proc.Cols / W, (float)proc.Rows / H);
                    List<PointRect> rects = new List<PointRect>();
                    RotatedRect box;
                    float upXR;
                    float upYR;
                    float lowXR;
                    float lowYR;
                    for ( var i = 0; i < indices.Length; ++i ) {
                        box = boxes[indices[i]];
                        //Point2f[] vertices = box.Points();

                        //for ( int j = 0; j < 4; ++j ) {
                        //    vertices[j].X *= ratio.X;
                        //    vertices[j].Y *= ratio.Y;
                        //}
                        var rect = box.BoundingRect();
                        x = (int) Math.Round(wratio * (float)rect.X) - padding;
                        y = (int)Math.Round(hratio * (float)rect.Y) - padding;
                        ex = (int)Math.Round(wratio * (float)(rect.X + rect.Width)) + padding;
                        ey = (int)Math.Round(hratio * (float)(rect.Y+ rect.Height)) + padding;
                        h = ey - y;
                        upXR = (float)x / originSize[0];
                        upYR = (float)y / originSize[1];
                        lowXR = (float)ex / originSize[0];
                        lowYR = (float)ey / originSize[1];
                        if (upXR >= bound.Value.MinBoundX && upYR>= bound.Value.MinBoundY && lowXR<= bound.Value.MaxBoundX && lowYR<= bound.Value.MaxBoundY
                            && h>=minHeight)
                            rects.Add(new PointRect(x, y, ex, ey));
                    }
                    rects.OrderBy(x => x.AY);
                    for (var i=0; i< rects.Count-1; i++)
                        for (var j=i+1; j < rects.Count; j++)
                            if (Math.Abs(rects[j].AY - rects[i].AY)<= heightThreshold ) {
                                if ( rects[j].AY > rects[i].AY )
                                    rects[j].AY = rects[i].AY;
                                else
                                    rects[i].AY = rects[j].AY;
                                if ( rects[j].BY > rects[i].BY )
                                    rects[i].BY = rects[j].BY;
                                else
                                    rects[j].BY = rects[i].BY;
                            }

                    rects.OrderBy(x => x.AY).ThenBy(x => x.AX);
                    //HashSet<PointRect> filteredRects = rects.ToHashSet();
                    return rects.ToHashSet();
                    //foreach ( PointRect r in filteredRects )
                    //    Cv2.Rectangle(img, new Point(r.AX, r.AY), new Point(r.BX, r.BY), new Scalar(0, 255, 0), 3);

                    //// Optional - Save detections
                    ////img.SaveImage(Path.Combine(Path.GetDirectoryName(fileName), $"{Path.GetFileNameWithoutExtension(fileName)}_east.jpg"));

                    //// return GetText(img, ...)
                    //return string.Empty;
                }
            }
        }

        public static HashSet<PointRect> MergeBoxes(HashSet<PointRect> source ) {
            var box = new List<PointRect>(source);
            for (var i=box.Count-2; i >= 0; i-- )
                for (var j=box.Count-1; j>i; j-- )
                    if (i< box.Count && j< box.Count && box[i].AY==box[j].AY && box[i].BY == box[j].BY ) {
                        if ( box[i].AX < box[j].AX && box[i].BX < box[j].BX) {
                            box[i].BX = box[j].BX;
                            box.RemoveAt(j);
                        }
                        else
                        if (box[j].AX < box[i].AX && box[j].BX < box[i].BX ) {
                            box[j].BX = box[i].BX;
                            box.RemoveAt(i);
                        }
                        else
                        if (box[j].AX> box[i].AX && box[j].BX< box[i].BX ) {
                            box.RemoveAt(j);
                        }
                        else if (box[i].AX>box[j].AX && box[i].BX < box[j].BX ) {
                            box.RemoveAt(i);
                        }
                    }
            return box.ToHashSet();
        }
        public static Rect Resize( Rect source, int originWidth, int width ) {
            float porfotion = width / (float) originWidth;
            var x = (int)Math.Round((float) source.X * porfotion);
            var y = (int)Math.Round((float) source.Y * porfotion);
            var w = (int)Math.Round((float)source.Width * porfotion);
            var h = (int)Math.Round((float)source.Height * porfotion);
            return new Rect(x, y, w, h);
        }

        public static Mat Resize(Mat source, int width ) {
            float porfotion = width / (float) source.Width;
            int newHeight = (int)Math.Round( (float) source.Height * porfotion);
            return source.Resize(new Size(width, newHeight));
        }
        public static Mat ResizeH( Mat source, int height ) {
            float porfotion = height / (float)source.Height;
            int newW = (int)Math.Round((float)source.Width * porfotion);
            return source.Resize(new Size(newW, height));
        }

        private static unsafe void Decode( Mat scores, Mat geometry, float confThreshold, out IList<RotatedRect> boxes, out IList<float> confidences ) {
            boxes = new List<RotatedRect>();
            confidences = new List<float>();

            if ( (scores == null || scores.Dims != 4 || scores.Size(0) != 1 || scores.Size(1) != 1) ||
                (geometry == null || geometry.Dims != 4 || geometry.Size(0) != 1 || geometry.Size(1) != 5) ||
                (scores.Size(2) != geometry.Size(2) || scores.Size(3) != geometry.Size(3)) ) {
                return;
            }

            int height = scores.Size(2);
            int width = scores.Size(3);

            for ( int y = 0; y < height; ++y ) {
                var scoresData = new ReadOnlySpan<float>((void*)scores.Ptr(0, 0, y), height);
                var x0Data = new ReadOnlySpan<float>((void*)geometry.Ptr(0, 0, y), height);
                var x1Data = new ReadOnlySpan<float>((void*)geometry.Ptr(0, 1, y), height);
                var x2Data = new ReadOnlySpan<float>((void*)geometry.Ptr(0, 2, y), height);
                var x3Data = new ReadOnlySpan<float>((void*)geometry.Ptr(0, 3, y), height);
                var anglesData = new ReadOnlySpan<float>((void*)geometry.Ptr(0, 4, y), height);

                for ( int x = 0; x < width; ++x ) {
                    var score = scoresData[x];
                    if ( score >= confThreshold ) {
                        float offsetX = x * 4.0f;
                        float offsetY = y * 4.0f;
                        float angle = anglesData[x];
                        float cosA = (float)Math.Cos(angle);
                        float sinA = (float)Math.Sin(angle);
                        float x0 = x0Data[x];
                        float x1 = x1Data[x];
                        float x2 = x2Data[x];
                        float x3 = x3Data[x];
                        float h = x0 + x2;
                        float w = x1 + x3;
                        Point2f offset = new Point2f(offsetX + (cosA * x1) + (sinA * x2), offsetY - (sinA * x1) + (cosA * x2));
                        Point2f p1 = new Point2f((-sinA * h) + offset.X, (-cosA * h) + offset.Y);
                        Point2f p3 = new Point2f((-cosA * w) + offset.X, (sinA * w) + offset.Y);
                        RotatedRect r = new RotatedRect(new Point2f(0.5f * (p1.X + p3.X), 0.5f * (p1.Y + p3.Y)), new Size2f(w, h), (float)(-angle * 180.0f / Math.PI));
                        boxes.Add(r);
                        confidences.Add(score);
                    }
                }
            }
        }
    }
}
