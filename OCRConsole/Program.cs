using OCRConsole.Core;
using OCRConsole.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Tesseract;

namespace OCRConsole {
    class Program {
        static void Main( string[] args ) {
            var path = "cards/card_2.png";
            CreditCardReader reader = new CreditCardReader(path);
            try {
                CreditCardReader.DebugMode = true;
                CreditCardReader.DebugPath = path;
                reader.Process();
                Console.Clear();
                Console.WriteLine(reader.CardName);
                Console.WriteLine(reader.CardNumber);
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
        }
    }
}
