using Homewatch.Contracts.Enums;

namespace Homewatch.Core.Utilities
{
    public static class CameraSensorResolutionsExtensions
    {
        public static (int Width, int Height) GetSize(this CameraSensorResolutions resolution)
        {
            return resolution switch
            {
                CameraSensorResolutions.Resolution800x600 => (800, 600),
                CameraSensorResolutions.Resolution1024x768 => (1024, 768),
                CameraSensorResolutions.Resolution1280x720 => (1280, 720),
                CameraSensorResolutions.Resolution1280x1024 => (1280, 1024),
                CameraSensorResolutions.Resolution1600x1200 => (1600, 1200),
                CameraSensorResolutions.Resolution1920x1080 => (1920, 1080),
                CameraSensorResolutions.Resolution2048x1536 => (2048, 1536),
                CameraSensorResolutions.Resolution2560x1440 => (2560, 1440),
                CameraSensorResolutions.Resolution2560x1600 => (2560, 1600),
                CameraSensorResolutions.Resolution2560x1920 => (2560, 1920),
                CameraSensorResolutions.Resolution720x1280 => (720, 1280),
                CameraSensorResolutions.Resolution864x1536 => (864, 1536),
                CameraSensorResolutions.Resolution1080x1920 => (1080, 1920),
                _ => (2048, 1536)
            };
        }

        public static CameraSensorResolutions? FindBySize(int width, int height)
        {
            foreach (var resolution in Enum.GetValues<CameraSensorResolutions>())
            {
                if (resolution.GetSize() == (width, height))
                {
                    return resolution;
                }
            }

            return null;
        }

        // the supported camera sensors have a 4:3 pixel array, so only 4:3 output sizes are scaled from the whole sensor without cropping
        public static bool UsesFullSensor(this CameraSensorResolutions resolution)
        {
            var (width, height) = resolution.GetSize();

            return width * 3 == height * 4;
        }
    }
}
