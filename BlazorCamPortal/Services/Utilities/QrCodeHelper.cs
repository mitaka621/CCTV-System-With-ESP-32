using QRCoder;

namespace CamPortal.Core.Utilities
{
    public static class QrCodeHelper
    {
        public static string GenerateQrCodeBase64(string text)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(
                text,
                QRCodeGenerator.ECCLevel.Q);

            var pngQrCode = new PngByteQRCode(qrCodeData);

            byte[] qrBytes = pngQrCode.GetGraphic(20);

            return $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";
        }
    }
}
