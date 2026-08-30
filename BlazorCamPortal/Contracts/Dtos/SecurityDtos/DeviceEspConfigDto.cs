namespace CamPortal.Contracts.Dtos.SecurityDtos
{
    public class DeviceEspConfigDto
    {
        public required string ConfigurationPropertyName { get; set; }

        public required object Value { get; set; }
    }
}
