namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IDeviceAuthenticatorService
    {
        public string GenerateChallenge();

        public bool ValidateDeviceResponse(string challenge, string deviceResponse);

        string ComputeHmac(string challenge);

        string GenerateSessionToken(int keySizeInBits = 256);

        byte[] DecryptAesGcm(byte[] ciphertext, byte[] key, byte[] iv, byte[] tag);
    }
}
