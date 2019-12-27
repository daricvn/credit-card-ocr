using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR.NET.Models {
    public struct BoundRatio {
        public float MinBoundX { get; set; }
        public float MinBoundY { get; set; }
        public float MaxBoundX { get; set; }
        public float MaxBoundY { get; set; }
        public BoundRatio( float x, float y, float ex, float ey ) {
            MinBoundX = x;
            MinBoundY = y;
            MaxBoundX = ex;
            MaxBoundY = ey;
        }
    }
}
