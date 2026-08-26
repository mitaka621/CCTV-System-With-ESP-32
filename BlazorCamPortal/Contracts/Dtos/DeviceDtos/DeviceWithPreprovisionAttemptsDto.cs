using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.PreprovisionAttemptDtos;

namespace CamPortal.Contracts.Dtos.CameraDtos
{
    public class DeviceWithPreprovisionAttemptsDto : DeviceDto
    {
        public List<PreprovisionAttemptDto> PreprovisionAttempts { get; set; } = new();
    }
}
