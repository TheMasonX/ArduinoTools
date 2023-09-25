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

        public static int GetIndex (this BitmapImage image, int x, int y)
        {
            return x + y * (int)image.Width;
        }


        public static List<Color> GetPixels (this BitmapImage image)
        {
            List<Color> data = new();
            if (image is null) return data; //No Image

            using (Bitmap bitmap = image.BitmapImageToBitmap())
            {
                if (bitmap is null) return data; //No Bitmap

                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        data.Add(bitmap.GetPixel(x, y));
                    }
                }
            }

            return data;
        }

        public static Bitmap BitmapImageToBitmap (this BitmapImage bitmapImage)
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
