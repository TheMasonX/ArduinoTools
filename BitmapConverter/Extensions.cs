using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace BitmapConverter
{
    public static class Extensions
    {
        public static string StripExtension (this string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return ""; //Invalid fileName

            Regex regex = new Regex(@"\..*");
            return regex.Replace(fileName, "");
        }

        public static int ToRGB (this Color color)
        {
            return ((color.R & 0x0ff) << 16) | ((color.G & 0x0ff) << 8) | (color.B & 0x0ff);
        }

        public static Bitmap BitmapImage2Bitmap (this BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                Bitmap bitmap = new(outStream);

                return new Bitmap(bitmap);
            }
        }
    }
}
