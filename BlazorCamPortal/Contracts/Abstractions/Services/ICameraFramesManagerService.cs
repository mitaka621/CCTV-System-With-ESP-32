namespace BlazorCamPortal.Contracts.Abstractions.Services
{
    public interface ICameraFramesManagerService
    {
        public event Func<Guid, byte[], Task>? FrameAdded;

        void AddFrame(Guid cameraId, byte[] frame);

    }
}
