using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Enums;
using System.Text.Json;

namespace CamPortal.Core.Utilities
{
    public class DeviceSessionHelper
    {
        private const byte _commandVersion = 1;
        private static readonly JsonSerializerOptions _configSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static bool TryBuildPayload(OutboundDeviceMessageDto message, out byte[] payload)
        {
            if (message.Command == DeviceCommand.None)
            {
                payload = Array.Empty<byte>();
                return false;
            }

            if (message.Command == DeviceCommand.SaveNewConfig)
            {
                if (message.Config == null)
                {
                    payload = Array.Empty<byte>();
                    return false;
                }

                var json = JsonSerializer
                    .SerializeToUtf8Bytes(message.Config.ToDictionary(
                        x => x.ConfigurationPropertyName,
                        x => x.Value
                    ), _configSerializerOptions);

                payload = new byte[2 + json.Length];
                payload[0] = _commandVersion;
                payload[1] = (byte)message.Command;
                Buffer.BlockCopy(json, 0, payload, 2, json.Length);
                return true;
            }

            payload = new byte[] { _commandVersion, (byte)message.Command };
            return true;
        }
    }
}
