namespace CamPortal.Contracts.Enums
{
    public enum CameraCommand : byte
    {
        None = 0,

        ResetSecurityAlarm = 1,

        TriggerSecurityAlarm = 2,

        SaveNewConfig = 3
    }
}
