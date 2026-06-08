namespace CamPortal.Contracts.Models
{
    public class AddDeviceWizardStateModel
    {
        public PreprovisionDeviceModel PreprovisionRequest { get; set; } = new();

        public bool IsPaired { get; set; }

        public Guid? DeviceId { get; set; }
    }
}
