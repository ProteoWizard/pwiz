using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ImageConverter
{
    public static class EmfUtilities
    {
        public static void ConvertToPng(string path, string outputPath)
        {
            using (Metafile emf = new Metafile(path))
            using (Bitmap bmp = new Bitmap(emf.Width, emf.Height))
            {
                bmp.SetResolution(emf.HorizontalResolution, emf.VerticalResolution);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(emf, 0, 0);
                    BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                    //convert image format
                    var src = new FormatConvertedBitmap();
                    src.BeginInit();
                    src.Source = bitmapSource;
                    src.DestinationFormat = PixelFormats.Bgra32;
                    src.EndInit();

                    //copy to bitmap
                    Bitmap bitmap = new Bitmap(src.PixelWidth, src.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var data = bitmap.LockBits(new Rectangle(System.Drawing.Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    src.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
                    bitmap.UnlockBits(data);
                    bitmap.Save(outputPath);
                }
            }
        }
    }
}
