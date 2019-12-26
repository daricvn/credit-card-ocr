using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace OCRConsole.Models {
    public class PointRect {
        public int AX;
        public int AY;
        public int BX;
        public int BY;
        public PointRect(int x, int y, int ex, int ey ) {
            AX = x;
            AY = y;
            BX = ex;
            BY = ey;
        }

        public Rect ToRect() {
            return new Rect(AX, AY, BX - AX, BY - AY);
        }

        public override bool Equals( object obj ) {
            if (obj is PointRect ) {
                var t = (PointRect)obj;
                return this.AX == t.AX && this.AY == t.AY && this.BX == t.BX && this.BY == t.BY;
            }
            return false;
        }

        public override int GetHashCode() {
            return (this.AX + this.AY + this.BX + this.BY).GetHashCode();
        }
    }
}
