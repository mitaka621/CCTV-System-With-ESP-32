namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IDeviceAuthenticatorService
    {
        public string GenerateChallenge();

        public bool ValidateDeviceResponse(string challenge, string deviceResponse);

        string ComputeHmac(string challenge);

        string GenerateSessionToken(int keySizeInBits = 256);

        byte[] DecryptAesGcm(Span<byte> ciphertext, Span<byte> key, Span<byte> iv, Span<byte> tag);
    }
}
