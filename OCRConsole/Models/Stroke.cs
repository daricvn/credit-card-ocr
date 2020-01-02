using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace OCRConsole.Models {
    public struct Stroke {
        public Point Location;
        public float Width;
        public Stroke(int x, int y, float w ) {
            Location = new Point(x, y);
            Width = w;
        }
    }
}
