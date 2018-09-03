using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace MultiWatcher.Utils
{
    static class BitmapConverter
    {
        static MemoryStream ms;

        static BitmapConverter()
        {
            ms = new MemoryStream();
        }
        
        public static BitmapSource ConvertBitmapToBitmapSource(Image source)
        {
            Bitmap bitmap = new Bitmap(source);
            IntPtr ip = bitmap.GetHbitmap();
            BitmapSource bs = null;
            try
            {
                bs = Imaging.CreateBitmapSourceFromHBitmap(ip, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                NativeMethods.DeleteObject(ip);
                bitmap.Dispose();
                bitmap = null;
                source.Dispose();
                source = null;
            }

            return bs;
        }

        public static BitmapImage ConvertBitmapToBitmapImage(Image source)
        {
            try
            {
                var bi = new BitmapImage();
                ms.SetLength(0);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                source.Save(ms, ImageFormat.Jpeg);
                ms.Position = 0;
                bi.BeginInit();
                ms.Seek(0, SeekOrigin.Begin);
                bi.StreamSource = ms;
                bi.EndInit();

                return bi;
            }
            finally
            { 
                source.Dispose();
                source = null;
            }
        }
    }
}
