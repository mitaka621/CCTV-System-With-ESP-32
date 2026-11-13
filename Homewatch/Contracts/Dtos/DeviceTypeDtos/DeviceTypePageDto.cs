namespace Homewatch.Contracts.Dtos.DeviceTypeDtos
{
    public record DeviceTypePageDto
    {
        public required List<DeviceTypeDto> Items { get; set; }

        public int TotalCount { get; set; }
    }
}
