namespace CamPortal.Contracts.Enums
{
    public enum DeviceCommand : byte
    {
        None = 0,

        ResetSecurityAlarm = 1,

        TriggerSecurityAlarm = 2,

        SaveNewConfig = 3,

        //used to confirm to the device that the server has received the payload and processed it
        PayloadAck = 4
    }
}
