namespace Homewatch.Contracts.Dtos.EmailNotificationDtos
{
    public class EmailOperationResultDto
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
