using System;

namespace SnipShot.Models
{
    /// <summary>
    /// Información de color capturado con diferentes formatos de representación
    /// </summary>
    public class ColorInfo
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public ColorInfo() { }

        public ColorInfo(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        /// <summary>
        /// Formato Hexadecimal (#RRGGBB)
        /// </summary>
        public string HEX => $"#{R:X2}{G:X2}{B:X2}";

        /// <summary>
        /// Formato RGB (rgb(r, g, b))
        /// </summary>
        public string RGB => $"rgb({R}, {G}, {B})";

        /// <summary>
        /// Formato HSL (hsl(h, s%, l%))
        /// </summary>
        public string HSL
        {
            get
            {
                var (h, s, l) = RGBtoHSL(R, G, B);
                return $"hsl({h:F0}, {s:F0}%, {l:F0}%)";
            }
        }

        /// <summary>
        /// Obtiene el valor del color en el formato especificado
        /// </summary>
        public string GetFormatted(ColorFormat format)
        {
            return format switch
            {
                ColorFormat.HEX => HEX,
                ColorFormat.RGB => RGB,
                ColorFormat.HSL => HSL,
                _ => HEX
            };
        }

        /// <summary>
        /// Convierte RGB a HSL
        /// </summary>
        private static (double h, double s, double l) RGBtoHSL(byte r, byte g, byte b)
        {
            double rd = r / 255.0;
            double gd = g / 255.0;
            double bd = b / 255.0;

            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double delta = max - min;

            double h = 0;
            double s = 0;
            double l = (max + min) / 2.0;

            if (delta != 0)
            {
                s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

                if (max == rd)
                {
                    h = ((gd - bd) / delta) + (gd < bd ? 6 : 0);
                }
                else if (max == gd)
                {
                    h = ((bd - rd) / delta) + 2;
                }
                else
                {
                    h = ((rd - gd) / delta) + 4;
                }

                h /= 6.0;
            }

            return (h * 360, s * 100, l * 100);
        }

        /// <summary>
        /// Obtiene el color como string para usar en XAML
        /// </summary>
        public string ToXamlColor() => $"#{R:X2}{G:X2}{B:X2}";
    }
}
