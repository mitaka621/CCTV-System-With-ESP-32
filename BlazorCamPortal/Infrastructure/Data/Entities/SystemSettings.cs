using CamPortal.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Infrastructure.Data.Entities
{
    public class SystemSettings
    {
        [Key]
        public Guid Id { get; set; }

        public RetentionPeriod EncodedVideoRetention { get; set; }

        public RetentionPeriod CameraChunkRetention { get; set; }
    }
}
