using System;
using System.Collections.Generic;
using System.Text;

namespace OCRConsole.Models {
    public struct SimpleGradients<T> {
        public T X;
        public T Y;
        public SimpleGradients(T x, T y ) {
            X = x;
            Y = y;
        }
    }
}
