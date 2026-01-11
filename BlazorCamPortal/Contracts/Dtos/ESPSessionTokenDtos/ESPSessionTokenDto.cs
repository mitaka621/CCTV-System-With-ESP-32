namespace CamPortal.Contracts.Dtos.ESPSessionTokenDtos
{
    public class ESPSessionTokenDto
    {
        public string? SessionToken { get; set; }

        public DateTime? SessionTokenExpirationDate { get; set; }
    }
}
