using System.Text.Json.Serialization;

namespace CamPortal.Contracts.Dtos.PreprovisionAttemptDtos
{
    public class PreprovisionDetailsDto
    {
        [JsonIgnore]
        public string? QRCode { get; set; }

        public string WifiSSID { get; set; } = null!;

        public string WifiPassword { get; set; } = null!;

        public string PrivateKey { get; set; } = null!;

        public string ServerIp { get; set; } = null!;

        public Guid DeviceId { get; set; }

        public string Nonce { get; set; } = null!;

        [JsonIgnore]
        public Guid PreprovisionId { get; set; }
    }
}
