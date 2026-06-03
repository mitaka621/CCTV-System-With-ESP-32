namespace CamPortal.Contracts.Dtos.CameraFrameDtos
{
    public class CameraFrameDto
    {
        public required byte[] ProccessedFrameOriginalResolution { get; set; }

        public required byte[] ProccessedFrameReducedResolution { get; set; }
    }
}
