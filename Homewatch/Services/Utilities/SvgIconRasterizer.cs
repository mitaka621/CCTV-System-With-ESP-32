using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Globalization;
using System.Numerics;
using System.Xml.Linq;

namespace Homewatch.Core.Utilities
{
    public static class SvgIconRasterizer
    {
        private const float _defaultViewBoxSize = 24f;

        public static byte[] RasterizeToPng(string svgMarkup, string colorHex, int sizeInPixels)
        {
            var root = ParseRoot(svgMarkup);
            var viewBox = ReadViewBox(root);
            var scale = sizeInPixels / Math.Max(viewBox.Width, viewBox.Height);
            var offsetX = (sizeInPixels - viewBox.Width * scale) / 2f;
            var offsetY = (sizeInPixels - viewBox.Height * scale) / 2f;

            var transform = Matrix4x4.CreateTranslation(-viewBox.X, -viewBox.Y, 0)
                * Matrix4x4.CreateScale(scale, scale, 1)
                * Matrix4x4.CreateTranslation(offsetX, offsetY, 0);

            var color = Color.ParseHex(colorHex);
            var shapes = new List<(IPath Path, float Opacity, IntersectionRule Rule)>();

            CollectShapes(root, 1f, IntersectionRule.NonZero, shapes);

            using var image = new Image<Rgba32>(sizeInPixels, sizeInPixels);

            foreach (var shape in shapes)
            {
                var drawingOptions = new DrawingOptions
                {
                    IntersectionRule = shape.Rule
                };

                var brush = Brushes.Solid(color.WithAlpha(shape.Opacity));
                var path = shape.Path.Transform(transform);

                image.Mutate(context => context.Paint(drawingOptions, canvas => canvas.Fill(brush, path)));
            }

            using var stream = new MemoryStream();

            image.SaveAsPng(stream);

            return stream.ToArray();
        }

        private static XElement ParseRoot(string svgMarkup)
        {
            var markup = svgMarkup.Trim();

            if (!markup.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
            {
                markup = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\">{markup}</svg>";
            }

            return XElement.Parse(markup);
        }

        private static RectangleF ReadViewBox(XElement root)
        {
            var viewBoxValues = SplitNumbers(root.Attribute("viewBox")?.Value);

            if (viewBoxValues.Count == 4 && viewBoxValues[2] > 0 && viewBoxValues[3] > 0)
            {
                return new RectangleF(viewBoxValues[0], viewBoxValues[1], viewBoxValues[2], viewBoxValues[3]);
            }

            var width = ReadNumber(root, "width", _defaultViewBoxSize);
            var height = ReadNumber(root, "height", _defaultViewBoxSize);

            return new RectangleF(0, 0, width, height);
        }

        private static void CollectShapes(
            XElement parent,
            float inheritedOpacity,
            IntersectionRule inheritedRule,
            List<(IPath Path, float Opacity, IntersectionRule Rule)> shapes)
        {
            foreach (var element in parent.Elements())
            {
                if (IsNotFilled(element))
                {
                    continue;
                }

                var opacity = inheritedOpacity
                    * ReadNumber(element, "opacity", 1f)
                    * ReadNumber(element, "fill-opacity", 1f);

                var rule = ReadIntersectionRule(element, inheritedRule);

                if (element.Name.LocalName == "g")
                {
                    CollectShapes(element, opacity, rule, shapes);
                    continue;
                }

                var path = CreatePath(element);

                if (path != null)
                {
                    shapes.Add((path, Math.Clamp(opacity, 0f, 1f), rule));
                }
            }
        }

        private static IPath? CreatePath(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "path":
                    return SixLabors.ImageSharp.Drawing.Path.TryParseSvgPath(element.Attribute("d")?.Value ?? string.Empty, out var path)
                        ? path
                        : null;

                case "rect":
                    var width = ReadNumber(element, "width", 0);
                    var height = ReadNumber(element, "height", 0);

                    if (width <= 0 || height <= 0)
                    {
                        return null;
                    }

                    var x = ReadNumber(element, "x", 0);
                    var y = ReadNumber(element, "y", 0);
                    var cornerRadius = ReadNumber(element, "rx", ReadNumber(element, "ry", 0));

                    return cornerRadius > 0
                        ? new RoundedRectanglePolygon(x, y, width, height, cornerRadius)
                        : new RectanglePolygon(x, y, width, height);

                case "circle":
                    var radius = ReadNumber(element, "r", 0);

                    return radius > 0
                        ? new EllipsePolygon(ReadNumber(element, "cx", 0), ReadNumber(element, "cy", 0), radius)
                        : null;

                case "ellipse":
                    var radiusX = ReadNumber(element, "rx", 0);
                    var radiusY = ReadNumber(element, "ry", 0);

                    return radiusX > 0 && radiusY > 0
                        ? new EllipsePolygon(ReadNumber(element, "cx", 0), ReadNumber(element, "cy", 0), radiusX * 2, radiusY * 2)
                        : null;

                case "polygon":
                case "polyline":
                    var coordinates = SplitNumbers(element.Attribute("points")?.Value);

                    if (coordinates.Count < 6)
                    {
                        return null;
                    }

                    var points = new PointF[coordinates.Count / 2];

                    for (var index = 0; index < points.Length; index++)
                    {
                        points[index] = new PointF(coordinates[index * 2], coordinates[index * 2 + 1]);
                    }

                    return new Polygon(points);

                default:
                    return null;
            }
        }

        private static bool IsNotFilled(XElement element)
        {
            var fill = element.Attribute("fill")?.Value.Trim();
            var style = element.Attribute("style")?.Value.Replace(" ", string.Empty) ?? string.Empty;

            return string.Equals(fill, "none", StringComparison.OrdinalIgnoreCase)
                || style.Contains("fill:none", StringComparison.OrdinalIgnoreCase)
                || style.Contains("display:none", StringComparison.OrdinalIgnoreCase);
        }

        private static IntersectionRule ReadIntersectionRule(XElement element, IntersectionRule inheritedRule)
        {
            var fillRule = element.Attribute("fill-rule")?.Value.Trim();

            if (string.IsNullOrEmpty(fillRule))
            {
                return inheritedRule;
            }

            return string.Equals(fillRule, "evenodd", StringComparison.OrdinalIgnoreCase)
                ? IntersectionRule.EvenOdd
                : IntersectionRule.NonZero;
        }

        private static float ReadNumber(XElement element, string attributeName, float defaultValue)
        {
            var value = element.Attribute(attributeName)?.Value.Trim().Replace("px", string.Empty);

            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : defaultValue;
        }

        private static List<float> SplitNumbers(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<float>();
            }

            return value
                .Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : float.NaN)
                .Where(x => !float.IsNaN(x))
                .ToList();
        }
    }
}
