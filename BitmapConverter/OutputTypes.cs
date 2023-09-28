using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitmapConverter
{
    public interface IOutput
    {
        public string Name { get; }
        public string Description { get; }
        public string TypeName { get; }
        public string FormatColor (Color color, bool inverted) { return ""; }
    }

    public class OutputRGBInt : IOutput
    {
        public string Name => "RGBInt";

        public string Description => "24bpp RGB int";

        public string TypeName => "int";

        public string FormatColor (Color color, bool inverted)
        {
            if (inverted) color = color.Invert();
            return color.ToRGB().ToString();
        }
    }

    public class OutputMonoInt : IOutput
    {
        public string Name => "MonoInt";

        public string Description => "8bpp Mono int";

        public string TypeName => "byte";

        public string FormatColor (Color color, bool inverted)
        {
            float brightness = color.GetBrightness();
            if (inverted) brightness = 1.0f - brightness;
            return ((int)(brightness * 255)).ToString();
        }
    }

    public class OutputBool : IOutput
    {
        public string Name => "MonoBool";

        public string Description => "1bpp Mono bool";

        public string TypeName => "bool";

        public string FormatColor (Color color, bool inverted)
        {
            return color.GetBrightness() < .5 ? inverted.ToString() : (!inverted).ToString();
        }
    }
}