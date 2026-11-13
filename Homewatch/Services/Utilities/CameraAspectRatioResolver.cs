using Homewatch.Contracts.Dtos.CameraConfigurationDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Core.Utilities
{
    public static class CameraAspectRatioResolver
    {
        public static (int Width, int Height) CalculateActualResolution(int fullWidth, int fullHeight, CameraAspectRatios aspectRatio)
        {
            (int ratioWidth, int ratioHeight) = aspectRatio switch
            {
                CameraAspectRatios.Ratio4_3 => (4, 3),
                CameraAspectRatios.Ratio3_4 => (3, 4),
                CameraAspectRatios.Ratio16_9 => (16, 9),
                CameraAspectRatios.Ratio9_16 => (9, 16),
                _ => (0, 0)
            };

            if (ratioWidth == 0 || ratioHeight == 0)
            {
                return (fullWidth, fullHeight);
            }

            int targetHeight = (int)Math.Floor(fullWidth * (double)ratioHeight / ratioWidth);

            if (targetHeight <= fullHeight)
            {
                return (fullWidth, targetHeight);
            }

            int targetWidth = (int)Math.Floor(fullHeight * (double)ratioWidth / ratioHeight);

            return (targetWidth, fullHeight);
        }

        public static CameraLayoutType GetLayoutType(CameraStreamingConfigurationDto cameraConfig)
        {
            if (cameraConfig.CameraAspectRatio == CameraAspectRatios.Original)
            {
                var (sensorWidth, sensorHeight) = cameraConfig.SensorResolution.GetSize();
                var layoutType = sensorWidth > sensorHeight ? CameraLayoutType.Horizontal : CameraLayoutType.Vertical;

                if ((cameraConfig.FrameRotation / 90) % 2 != 0)
                {
                    return layoutType == CameraLayoutType.Horizontal ? CameraLayoutType.Vertical : CameraLayoutType.Horizontal;
                }

                return layoutType;
            }

            if (cameraConfig.CameraAspectRatio == CameraAspectRatios.Ratio16_9 || cameraConfig.CameraAspectRatio == CameraAspectRatios.Ratio4_3)
            {
                return CameraLayoutType.Horizontal;
            }

            return CameraLayoutType.Vertical;
        }

        public static (int Width, int Height) GetEncodingResolution(CameraStreamingConfigurationDto cameraConfig)
        {
            var (width, height) = cameraConfig.SensorResolution.GetSize();

            if ((int)(cameraConfig.FrameRotation / 90) % 2 != 0)
            {
                (width, height) = (height, width);
            }

            (int aspectWidth, int aspectHeight) = CalculateActualResolution(width, height, cameraConfig.CameraAspectRatio);

            float zoom = cameraConfig.ZoomFactor <= 0 ? 1 : cameraConfig.ZoomFactor;

            int finalWidth = Math.Clamp((int)(aspectWidth / zoom), 1, width);
            int finalHeight = Math.Clamp((int)(aspectHeight / zoom), 1, height);

            finalWidth -= finalWidth % 2;
            finalHeight -= finalHeight % 2;

            return (Math.Max(2, finalWidth), Math.Max(2, finalHeight));
        }
    }
}
