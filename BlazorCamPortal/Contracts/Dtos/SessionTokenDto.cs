namespace BlazorCamPortal.Contracts.Dtos
{
    public class SessionTokenDto
    {
        public string? SessionToken { get; set; }

        public DateTime? SessionTokenExpirationDate { get; set; }
    }
}
