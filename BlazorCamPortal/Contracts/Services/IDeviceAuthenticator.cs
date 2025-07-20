namespace BlazorCamPortal.Contracts.Services
{
    public interface IDeviceAuthenticator
    {
        public string GenerateChallenge();

        public bool ValidateDeviceResponse(string challenge, string deviceResponse);
    }
}
