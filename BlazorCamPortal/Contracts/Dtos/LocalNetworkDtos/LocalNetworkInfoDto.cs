namespace CamPortal.Contracts.Dtos.LocalNetworkDtos
{
    public sealed record LocalNetworkInfoDto
    {
        public required string LocalIp { get; init; }
        public string? Gateway { get; init; }
        public required string SubnetMask { get; init; }
    }
}
