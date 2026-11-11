using CamPortal.Contracts.Dtos.CameraFrameDtos;
using CamPortal.Contracts.Enums;
using SixLabors.ImageSharp.Processing;
using System.Globalization;
using System.Text;

namespace CamPortal.Core.Utilities
{
    public static class CameraFrameStampFilterBuilder
    {
        public const string OriginalResolutionOutputLabel = "original";
        public const string ReducedResolutionOutputLabel = "reduced";

        private const int _stampFontSize = 28;
        private const int _stampBottomOffset = 50;
        private const int _stampSidePadding = 10;
        private const int _reducedResolutionWidth = 640;

        private static readonly Lazy<string?> _stampFontFilePath = new(FindStampFontFilePath);

        public static string? StampFontFilePath => _stampFontFilePath.Value;

        public static string Build(CameraFrameStampSettingsDto settings)
        {
            var normalizedRotation = ((settings.FrameRotation % 360) + 360) % 360;
            var (rotatedWidth, rotatedHeight) = GetRotatedSize(settings.FrameWidth, settings.FrameHeight, normalizedRotation);

            var filters = new List<string>();

            AddRotationFilters(filters, normalizedRotation, rotatedWidth, rotatedHeight);
            AddCropFilter(filters, settings, rotatedWidth, rotatedHeight);
            AddColorFilter(filters, settings.Brightness, settings.Contrast);
            AddFlipFilter(filters, settings.FlipMode);
            AddStampFilters(filters, settings.CameraName);

            return $"[0:v]{string.Join(',', filters)},split=2[{OriginalResolutionOutputLabel}][scaled];"
                + $"[scaled]scale=w='min({_reducedResolutionWidth},iw)':h=-2:flags=bilinear[{ReducedResolutionOutputLabel}]";
        }

        private static (int Width, int Height) GetRotatedSize(int width, int height, float normalizedRotation)
        {
            switch (normalizedRotation)
            {
                case 0:
                case 180:
                    return (width, height);
                case 90:
                case 270:
                    return (height, width);
            }

            var radians = normalizedRotation * Math.PI / 180;
            var cosine = Math.Abs(Math.Cos(radians));
            var sine = Math.Abs(Math.Sin(radians));

            return (
                RoundUpToEven(width * cosine + height * sine),
                RoundUpToEven(width * sine + height * cosine));
        }

        private static int RoundUpToEven(double value)
        {
            var rounded = (int)Math.Ceiling(value);

            return rounded % 2 == 0 ? rounded : rounded + 1;
        }

        private static void AddRotationFilters(List<string> filters, float normalizedRotation, int rotatedWidth, int rotatedHeight)
        {
            switch (normalizedRotation)
            {
                case 0:
                    return;
                case 90:
                    filters.Add("transpose=clock");
                    return;
                case 180:
                    filters.Add("hflip");
                    filters.Add("vflip");
                    return;
                case 270:
                    filters.Add("transpose=cclock");
                    return;
            }

            var radians = FormatNumber(normalizedRotation * Math.PI / 180);

            filters.Add($"rotate=a={radians}:ow={rotatedWidth}:oh={rotatedHeight}:c=black");
        }

        private static void AddCropFilter(List<string> filters, CameraFrameStampSettingsDto settings, int width, int height)
        {
            if (settings.ZoomFactor <= 1 && settings.CameraAspectRatio == CameraAspectRatios.Original)
            {
                return;
            }

            (int frameWidth, int frameHeight) = CameraAspectRatioResolver.CalculateActualResolution(width, height, settings.CameraAspectRatio);

            var cropWidth = Math.Clamp((int)(frameWidth / settings.ZoomFactor), 1, width);
            var cropHeight = Math.Clamp((int)(frameHeight / settings.ZoomFactor), 1, height);
            var startX = Math.Clamp(settings.ZoomStartX, 0, width - cropWidth);
            var startY = Math.Clamp(settings.ZoomStartY, 0, height - cropHeight);

            filters.Add($"crop={cropWidth}:{cropHeight}:{startX}:{startY}");
        }

        private static void AddColorFilter(List<string> filters, float brightness, float contrast)
        {
            if (brightness == 1 && contrast == 1)
            {
                return;
            }

            var brightnessText = FormatNumber(brightness);
            var contrastText = FormatNumber(contrast);
            var lumaOffsetText = FormatNumber(127.5 * (1 - contrast));

            // brightness scales RGB and contrast scales RGB around mid grey, which in full range YCbCr becomes these per channel formulas
            var luma = $"clip({contrastText}*clip(val*{brightnessText},0,255)+{lumaOffsetText},0,255)";
            var chroma = $"clip(128+{contrastText}*(clip(128+(val-128)*{brightnessText},0,255)-128),0,255)";

            filters.Add($"lutyuv=y='{luma}':u='{chroma}':v='{chroma}'");
        }

        private static void AddFlipFilter(List<string> filters, FlipMode flipMode)
        {
            if (flipMode == FlipMode.Horizontal)
            {
                filters.Add("hflip");
            }
            else if (flipMode == FlipMode.Vertical)
            {
                filters.Add("vflip");
            }
        }

        private static void AddStampFilters(List<string> filters, string cameraName)
        {
            var fontOption = StampFontFilePath == null
                ? string.Empty
                : $"fontfile={EscapeOptionValue(StampFontFilePath.Replace('\\', '/'))}:";

            var textOptions = $"{fontOption}fontsize={_stampFontSize}:fontcolor=white";

            filters.Add($@"drawtext={textOptions}:x=W-tw-{_stampSidePadding}:y=H-{_stampBottomOffset}:text='%{{localtime\:%Y-%m-%d %H\\\:%M\\\:%S}}'");
            filters.Add($"drawtext={textOptions}:x={_stampSidePadding}:y=H-{_stampBottomOffset}:expansion=none:text={EscapeOptionValue(cameraName)}");
        }

        private static string EscapeOptionValue(string value)
        {
            // ffmpeg unescapes filter option values twice: once for the option list and once for the filter graph
            var optionListEscaped = EscapeCharacters(value, @"\':");

            return EscapeCharacters(optionListEscaped, @"\'[],;");
        }

        private static string EscapeCharacters(string value, string specialCharacters)
        {
            var builder = new StringBuilder(value.Length);

            foreach (var character in value)
            {
                if (specialCharacters.Contains(character))
                {
                    builder.Append('\\');
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string? FindStampFontFilePath()
        {
            string[] candidates = OperatingSystem.IsWindows()
                ?
                [
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arialbd.ttf"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf")
                ]
                :
                [
                    "/usr/share/fonts/truetype/msttcorefonts/Arial_Bold.ttf",
                    "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
                    "/usr/share/fonts/liberation-sans/LiberationSans-Bold.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                    "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf"
                ];

            return candidates.FirstOrDefault(File.Exists);
        }
    }
}
