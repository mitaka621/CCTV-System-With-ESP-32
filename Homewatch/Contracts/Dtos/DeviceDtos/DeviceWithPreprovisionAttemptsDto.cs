using Homewatch.Contracts.Dtos.DeviceDtos;
using Homewatch.Contracts.Dtos.PreprovisionAttemptDtos;

namespace Homewatch.Contracts.Dtos.CameraDtos
{
    public class DeviceWithPreprovisionAttemptsDto : DeviceDto
    {
        public List<PreprovisionAttemptDto> PreprovisionAttempts { get; set; } = new();
    }
}
