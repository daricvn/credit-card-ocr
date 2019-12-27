# Credit Card OCR.NET
A project to show how to use Tesseract OCR with high accuracy.

### Used algorithms:
These are how a credit is read from image:
- Use EAST model and open CV to detect text, result are Rects.
- Calculate all points and rebound rects' area.
- Extract text and attempt to read text by:
- - Tesseract OCR
- - Template Match OpenCV