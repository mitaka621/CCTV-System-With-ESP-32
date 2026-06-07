namespace CamPortal.Contracts.Dtos.UserCameraLayoutDtos
{
    public class CameraGridCellDto
    {
        public int X { get; set; }

        public int Y { get; set; }

        public string LocationAsString => $"{Y}_{X}";
    }
}
