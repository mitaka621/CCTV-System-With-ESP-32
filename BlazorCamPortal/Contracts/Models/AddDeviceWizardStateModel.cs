namespace CamPortal.Contracts.Models
{
    public class AddDeviceWizardStateModel
    {
        public PreprovisionDeviceModel PreprovisionRequest { get; set; } = new();

        public bool IsHandshakeCompleted { get; set; }

        public Guid? PairedDeviceId { get; set; }
    }
}
