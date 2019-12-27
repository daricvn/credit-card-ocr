using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace OCRNET.Utility {
    public class ImageDenoising {
        private const float MIN_BRIGHTNESS = 0;
        private const float MAX_BRIGHTNESS = 1;

        public static Bitmap NormalizeImageBrightness( Bitmap image ) {
            float minBrightness = MAX_BRIGHTNESS;
            float maxBrightness = MIN_BRIGHTNESS;

            /* Get the brightness range of the image. */
            for ( int x = 0; x < image.Width; x++ ) {
                for ( int y = 0; y < image.Height; y++ ) {
                    float pixelBrightness = image.GetPixel(x, y).GetBrightness();
                    minBrightness = Math.Min(minBrightness, pixelBrightness);
                    maxBrightness = Math.Max(maxBrightness, pixelBrightness);
                }
            }

            /* Normalize the image brightness. */
            for ( int x = 0; x < image.Width; x++ ) {
                for ( int y = 0; y < image.Height; y++ ) {
                    Color pixelColor = image.GetPixel(x, y);
                    float normalizedPixelBrightness = (pixelColor.GetBrightness() - minBrightness) / (maxBrightness - minBrightness);
                    Color normalizedPixelColor = ColorFromAhsb(pixelColor.A, pixelColor.GetHue(),
                    pixelColor.GetSaturation(), normalizedPixelBrightness);
                    image.SetPixel(x, y, normalizedPixelColor);
                }
            }
            return image;
        }
        public static Color ColorFromAhsb( int a, float h, float s, float b ) {
            if ( 0 > a || 255 < a ) {
                throw new Exception("a");
            }
            if ( 0f > h || 360f < h ) {
                throw new Exception("h");
            }
            if ( 0f > s || 1f < s ) {
                throw new Exception("s");
            }
            if ( 0f > b || 1f < b ) {
                throw new Exception("b");
            }

            if ( 0 == s ) {
                return Color.FromArgb(a, Convert.ToInt32(b * 255),
                  Convert.ToInt32(b * 255), Convert.ToInt32(b * 255));
            }

            float fMax, fMid, fMin;
            int iSextant, iMax, iMid, iMin;

            if ( 0.5 < b ) {
                fMax = b - (b * s) + s;
                fMin = b + (b * s) - s;
            }
            else {
                fMax = b + (b * s);
                fMin = b - (b * s);
            }

            iSextant = (int)Math.Floor(h / 60f);
            if ( 300f <= h ) {
                h -= 360f;
            }
            h /= 60f;
            h -= 2f * (float)Math.Floor(((iSextant + 1f) % 6f) / 2f);
            if ( 0 == iSextant % 2 ) {
                fMid = h * (fMax - fMin) + fMin;
            }
            else {
                fMid = fMin - h * (fMax - fMin);
            }

            iMax = Convert.ToInt32(fMax * 255);
            iMid = Convert.ToInt32(fMid * 255);
            iMin = Convert.ToInt32(fMin * 255);

            switch ( iSextant ) {
                case 1:
                    return Color.FromArgb(a, iMid, iMax, iMin);
                case 2:
                    return Color.FromArgb(a, iMin, iMax, iMid);
                case 3:
                    return Color.FromArgb(a, iMin, iMid, iMax);
                case 4:
                    return Color.FromArgb(a, iMid, iMin, iMax);
                case 5:
                    return Color.FromArgb(a, iMax, iMin, iMid);
                default:
                    return Color.FromArgb(a, iMax, iMid, iMin);
            }
        }
        public static Bitmap MedianFilter( Bitmap Image, int Size ) {
            System.Drawing.Bitmap TempBitmap = Image;
            System.Drawing.Bitmap NewBitmap = new System.Drawing.Bitmap(TempBitmap.Width, TempBitmap.Height);
            System.Drawing.Graphics NewGraphics = System.Drawing.Graphics.FromImage(NewBitmap);
            NewGraphics.DrawImage(TempBitmap, new System.Drawing.Rectangle(0, 0, TempBitmap.Width, TempBitmap.Height), new System.Drawing.Rectangle(0, 0, TempBitmap.Width, TempBitmap.Height), System.Drawing.GraphicsUnit.Pixel);
            NewGraphics.Dispose();
            Random TempRandom = new Random();
            int ApetureMin = -(Size / 2);
            int ApetureMax = (Size / 2);
            for ( int x = 0; x < NewBitmap.Width; ++x ) {
                for ( int y = 0; y < NewBitmap.Height; ++y ) {
                    List<int> RValues = new List<int>();
                    List<int> GValues = new List<int>();
                    List<int> BValues = new List<int>();
                    for ( int x2 = ApetureMin; x2 < ApetureMax; ++x2 ) {
                        int TempX = x + x2;
                        if ( TempX >= 0 && TempX < NewBitmap.Width ) {
                            for ( int y2 = ApetureMin; y2 < ApetureMax; ++y2 ) {
                                int TempY = y + y2;
                                if ( TempY >= 0 && TempY < NewBitmap.Height ) {
                                    Color TempColor = TempBitmap.GetPixel(TempX, TempY);
                                    RValues.Add(TempColor.R);
                                    GValues.Add(TempColor.G);
                                    BValues.Add(TempColor.B);
                                }
                            }
                        }
                    }
                    RValues.Sort();
                    GValues.Sort();
                    BValues.Sort();
                    Color MedianPixel = Color.FromArgb(RValues[RValues.Count / 2],
                        GValues[GValues.Count / 2],
                        BValues[BValues.Count / 2]);
                    NewBitmap.SetPixel(x, y, MedianPixel);
                }
            }
            return NewBitmap;
        }

        public static Bitmap Greyscale( Bitmap input ) {
            Bitmap greyscale = new Bitmap(input.Width, input.Height);
            for ( int x = 0; x < input.Width; x++ ) {
                for ( int y = 0; y < input.Height; y++ ) {
                    Color pixelColor = input.GetPixel(x, y);
                    //  0.3 · r + 0.59 · g + 0.11 · b
                    int grey = (int)(pixelColor.R * 0.3 + pixelColor.G * 0.59 + pixelColor.B * 0.11);
                    greyscale.SetPixel(x, y, Color.FromArgb(pixelColor.A, grey, grey, grey));
                }
            }
            return greyscale;
        }
    }
}
