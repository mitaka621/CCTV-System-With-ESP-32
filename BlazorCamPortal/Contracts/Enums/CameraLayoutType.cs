namespace CamPortal.Contracts.Enums
{
    public enum CameraLayoutType
    {
        //when no camera is present in the grid cell
        Empty,

        //when a feed in horizontal aspect ratio (16:9) is present in the grid cell
        Horizontal,

        //when a feed in vertical aspect ratio (9:16) is present in the grid cell
        Vertical,

        //used for vertical feeds that span across two grid cells, the top cell will be marked as Vertical
        //and the bottom cell will be marked as Reserved (no other camera can be placed there)
        Reserved,
    }
}
