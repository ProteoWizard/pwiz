using System;

namespace ColorGenerator
{
    /// <summary>
    /// This class contains complex LUV color calculations, which we use to generate colors
    /// with distances calculated in LUV coordinates, good for human perception.
    /// This code was downloaded from https://github.com/THEjoezack/ColorMine.
    /// </summary>
    static class LuvColor
    {
        /// <summary>
        /// Defines how comparison methods may be called
        /// </summary>
        public interface IColorSpaceComparison
        {
            /// <summary>
            /// Returns the difference between two colors given based on the specified defined in the called class.
            /// </summary>
            /// <param name="a"></param>
            /// <param name="b"></param>
            /// <returns>Score based on similarity, the lower the score the closer the colors</returns>
            double Compare(IColorSpace a, IColorSpace b);
        }

        public delegate double ComparisonAlgorithm(IColorSpace a, IColorSpace b);

        /// <summary>
        /// Defines the public methods for all color spaces
        /// </summary>
        public interface IColorSpace
        {
            /// <summary>
            /// Initialize settings from an Rgb object
            /// </summary>
            /// <param name="color"></param>
            void Initialize(IRgb color);

            /// <summary>
            /// Convert the color space to Rgb, you should probably using the "To" method instead. Need to figure out a way to "hide" or otherwise remove this method from the public interface.
            /// </summary>
            /// <returns></returns>
            IRgb ToRgb();

            /// <summary>
            /// Convert any IColorSpace to any other IColorSpace.
            /// </summary>
            /// <typeparam name="T">IColorSpace type to convert to</typeparam>
            /// <returns></returns>
            T To<T>() where T : IColorSpace, new();

            /// <summary>
            /// Determine how close two IColorSpaces are to each other using a passed in algorithm
            /// </summary>
            /// <param name="compareToValue">Other IColorSpace to compare to</param>
            /// <param name="comparer">Algorithm to use for comparison</param>
            /// <returns>Distance in 3d space as double</returns>
            double Compare(IColorSpace compareToValue, IColorSpaceComparison comparer);
        }

        /// <summary>
        /// Abstract ColorSpace class, defines the To method that converts between any IColorSpace.
        /// </summary>
        public abstract class ColorSpace : IColorSpace
        {
            public abstract void Initialize(IRgb color);
            public abstract IRgb ToRgb();

            /// <summary>
            /// Convienience method for comparing any IColorSpace
            /// </summary>
            /// <param name="compareToValue"></param>
            /// <param name="comparer"></param>
            /// <returns>Single number representing the difference between two colors</returns>
            public double Compare(IColorSpace compareToValue, IColorSpaceComparison comparer)
            {
                return comparer.Compare(this, compareToValue);
            }

            /// <summary>
            /// Convert any IColorSpace to any other IColorSpace
            /// </summary>
            /// <typeparam name="T">Must implement IColorSpace, new()</typeparam>
            /// <returns></returns>
            public T To<T>() where T : IColorSpace, new()
            {
                if (typeof(T) == GetType())
                {
                    return (T)MemberwiseClone();
                }

                var newColorSpace = new T();
                newColorSpace.Initialize(ToRgb());

                return newColorSpace;
            }
        }
        public interface IRgb : IColorSpace
        {
            double R { get; set; }
            double G { get; set; }
            double B { get; set; }
        }

        internal static class RgbConverter
        {
            internal static void ToColorSpace(IRgb color, IRgb item)
            {
                item.R = color.R;
                item.G = color.G;
                item.B = color.B;
            }

            internal static IRgb ToColor(IRgb item)
            {
                return item;
            }
        }

        public class Rgb : ColorSpace, IRgb
        {
            public double R { get; set; }
            public double G { get; set; }
            public double B { get; set; }

            public override void Initialize(IRgb color)
            {
                RgbConverter.ToColorSpace(color, this);
            }

            public override IRgb ToRgb()
            {
                return RgbConverter.ToColor(this);
            }
        }

        public interface IXyz : IColorSpace
        {
            double X { get; set; }
            double Y { get; set; }
            double Z { get; set; }
        }

        public class Xyz : ColorSpace, IXyz
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }

            public override void Initialize(IRgb color)
            {
                XyzConverter.ToColorSpace(color, this);
            }

            public override IRgb ToRgb()
            {
                return XyzConverter.ToColor(this);
            }
        }

        public interface ILuv : IColorSpace
        {
            double L { get; set; }
            double U { get; set; }
            double V { get; set; }
        }

        public class Luv : ColorSpace, ILuv
        {
            public double L { get; set; }
            public double U { get; set; }
            public double V { get; set; }

            public override void Initialize(IRgb color)
            {
                LuvConverter.ToColorSpace(color, this);
            }

            public override IRgb ToRgb()
            {
                return LuvConverter.ToColor(this);
            }
        }

        internal static class LuvConverter
        {
            internal static void ToColorSpace(IRgb color, ILuv item)
            {
                var xyz = new Xyz();
                var white = XyzConverter.WhiteReference;
                xyz.Initialize(color);

                var y = xyz.Y / XyzConverter.WhiteReference.Y;
                item.L = y > XyzConverter.Epsilon ? 116.0 * XyzConverter.CubicRoot(y) - 16.0 : XyzConverter.Kappa * y;

                var targetDenominator = GetDenominator(xyz);
                var referenceDenominator = GetDenominator(white);
                // ReSharper disable CompareOfFloatsByEqualityOperator
                var xTarget = targetDenominator == 0
                    ? 0
                    : ((4.0 * xyz.X / targetDenominator) - (4.0 * white.X / referenceDenominator));
                var yTarget = targetDenominator == 0
                    ? 0
                    : ((9.0 * xyz.Y / targetDenominator) - (9.0 * white.Y / referenceDenominator));
                // ReSharper restore CompareOfFloatsByEqualityOperator

                item.U = 13.0 * item.L * xTarget;
                item.V = 13.0 * item.L * yTarget;
            }

            internal static IRgb ToColor(ILuv item)
            {
                var white = XyzConverter.WhiteReference;
                const double c = -1.0 / 3.0;
                var uPrime = (4.0 * white.X) / GetDenominator(white);
                var vPrime = (9.0 * white.Y) / GetDenominator(white);
                var a = (1.0 / 3.0) * ((52.0 * item.L) / (item.U + 13 * item.L * uPrime) - 1.0);
// ReSharper disable once InconsistentNaming
                var imteL_16_116 = (item.L + 16.0) / 116.0;
                var y = item.L > XyzConverter.Kappa * XyzConverter.Epsilon
                    ? imteL_16_116 * imteL_16_116 * imteL_16_116
                    : item.L / XyzConverter.Kappa;
                var b = -5.0 * y;
                var d = y * ((39.0 * item.L) / (item.V + 13.0 * item.L * vPrime) - 5.0);
                var x = (d - b) / (a - c);
                var z = x * a + b;
                var xyz = new Xyz
                {
                    X = 100 * x,
                    Y = 100 * y,
                    Z = 100 * z
                };
                return xyz.ToRgb();
            }

            private static double GetDenominator(IXyz xyz)
            {
                return xyz.X + 15.0 * xyz.Y + 3.0 * xyz.Z;
            }
        }

        internal static class XyzConverter
        {
            #region Constants/Helper methods for Xyz related spaces
            internal static IXyz WhiteReference { get; private set; }
            internal const double Epsilon = 0.008856; // Intent is 216/24389
            internal const double Kappa = 903.3; // Intent is 24389/27
            static XyzConverter()
            {
                WhiteReference = new Xyz
                {
                    X = 95.047,
                    Y = 100.000,
                    Z = 108.883
                };
            }

            internal static double CubicRoot(double n)
            {
                return Math.Pow(n, 1.0 / 3.0);
            }
            #endregion

            internal static void ToColorSpace(IRgb color, IXyz item)
            {
                var r = PivotRgb(color.R / 255.0);
                var g = PivotRgb(color.G / 255.0);
                var b = PivotRgb(color.B / 255.0);

                // Observer. = 2°, Illuminant = D65
                item.X = r * 0.4124 + g * 0.3576 + b * 0.1805;
                item.Y = r * 0.2126 + g * 0.7152 + b * 0.0722;
                item.Z = r * 0.0193 + g * 0.1192 + b * 0.9505;
            }

            internal static IRgb ToColor(IXyz item)
            {
                // (Observer = 2°, Illuminant = D65)
                var x = item.X / 100.0;
                var y = item.Y / 100.0;
                var z = item.Z / 100.0;

                var r = x * 3.2406 + y * -1.5372 + z * -0.4986;
                var g = x * -0.9689 + y * 1.8758 + z * 0.0415;
                var b = x * 0.0557 + y * -0.2040 + z * 1.0570;

                r = r > 0.0031308 ? 1.055 * Math.Pow(r, 1 / 2.4) - 0.055 : 12.92 * r;
                g = g > 0.0031308 ? 1.055 * Math.Pow(g, 1 / 2.4) - 0.055 : 12.92 * g;
                b = b > 0.0031308 ? 1.055 * Math.Pow(b, 1 / 2.4) - 0.055 : 12.92 * b;

                return new Rgb
                {
                    R = ToRgb(r),
                    G = ToRgb(g),
                    B = ToRgb(b)
                };
            }

            private static double ToRgb(double n)
            {
                var result = 255.0 * n;
                if (result < 0) return 0;
                if (result > 255) return 255;
                return result;
            }

            private static double PivotRgb(double n)
            {
                return (n > 0.04045 ? Math.Pow((n + 0.055) / 1.055, 2.4) : n / 12.92) * 100.0;
            }
        }
    }
}
