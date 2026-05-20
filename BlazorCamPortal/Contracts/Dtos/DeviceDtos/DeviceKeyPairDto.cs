namespace CamPortal.Contracts.Dtos.DeviceDtos
{
    public class DeviceKeyPairDto
    {
        public required string PrivateKeyBase64 { get; set; }
        public required string PublicKeySpkiBase64 { get; set; }
        public required string PublicKeyFingerprintHex { get; set; }
    }
}
