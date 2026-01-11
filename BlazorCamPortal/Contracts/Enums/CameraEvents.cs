using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Enums
{
    public enum CameraEvents
    {
        [Display(Name = "Video Chunk Saved")]
        VideoChunkSaved,

        [Display(Name = "Connection Lost")]
        ConnectionLost,

        [Display(Name = "Video Chunk Missing")]
        ChunkMissing,
    }
}
