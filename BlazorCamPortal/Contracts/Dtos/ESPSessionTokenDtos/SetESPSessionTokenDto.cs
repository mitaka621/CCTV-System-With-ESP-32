using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.ESPSessionTokenDtos
{
    public class SetESPSessionTokenDto
    {
        public required string Ipv4 { get; set; }

        public required string Mac { get; set; }

        public required string SessionToken { get; set; }

        public DateTime ExpirationDate { get; set; }

        public required PairStatus[] AllowedStatuses { get; set; }
    }
}
