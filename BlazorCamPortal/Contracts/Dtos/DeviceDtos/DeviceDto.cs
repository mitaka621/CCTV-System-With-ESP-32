using Homewatch.Contracts.Dtos.DeviceTypeDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.DeviceDtos
{
    public class DeviceDto
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }

        public string? Ipv4Address { get; set; }

        public DevicePairStatus PairStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public required string PublicKey { get; set; }

        public required string Fingerprint { get; set; }

        public DeviceTypeDto? DeviceType { get; set; }
    }
}
