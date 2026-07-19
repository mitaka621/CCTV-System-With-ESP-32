namespace CamPortal.Core.Services.Security
{
    internal sealed record SecurityThresholds(int MinFps, double MaxTemperatureC, double MaxHumidityPercent);
}
