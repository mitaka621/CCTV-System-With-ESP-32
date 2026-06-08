using CamPortal.Contracts.Dtos.DeviceDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IDeviceAuthenticatorService
    {

        DeviceKeyPairDto GenerateKeyPair();

        string GenerateNonceBase64();

        byte[] ComputeBindingHash(
            Guid deviceId,
            string publicKeyFingerprintHex,
            string nonceBase64);

        bool VerifySignature(
            string publicKeySpkiBase64,
            byte[] bindingHash,
            string signatureBase64);
    }
}
