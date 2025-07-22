namespace BlazorCamPortal.Contracts.Abstractions.Services
{
    public interface IDeviceAuthenticatorService
    {
        public string GenerateChallenge();

        public bool ValidateDeviceResponse(string challenge, string deviceResponse);

        string ComputeHmac(string challenge);

        string GenerateSessionToken(int keySizeInBits = 256);
    }
}
