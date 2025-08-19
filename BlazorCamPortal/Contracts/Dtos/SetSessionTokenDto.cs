using BlazorCamPortal.Contracts.Enums;

namespace BlazorCamPortal.Contracts.Dtos
{
    public class SetSessionTokenDto
    {
        public required string Ipv4 { get; set; }

        public required string Mac { get; set; }

        public required string SessionToken { get; set; }

        public DateTime ExpirationDate { get; set; }

        public required PairStatus[] AllowedStatuses { get; set; }
    }
}
