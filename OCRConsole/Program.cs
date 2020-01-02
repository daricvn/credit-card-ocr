using OCRConsole.Utility;
using OpenCvSharp;
using System;
using System.IO;

namespace OCRConsole {
    class Program {
        static void Main( string[] args ) {
            var path = "cards/uk.jpg";
            CreditCardReader reader = new CreditCardReader(path);
            try {
                var debugPath = "cards/result";
                //var mat = new Mat("D:/img005.jpg");
                //mat = EAST.Resize(mat,(750));
                //ImLib.EmbossedFilter(mat).SaveImage("D:/ger1_test.png");
                //return;
                CreditCardReader.DebugMode = true;
                CreditCardReader.DebugPath = debugPath;
                if ( !Directory.Exists(debugPath) )
                    Directory.CreateDirectory(debugPath);
                foreach ( var file in new DirectoryInfo(debugPath).GetFiles() )
                    file.Delete();

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
