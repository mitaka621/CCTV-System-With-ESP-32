using CamPortal.Contracts.Dtos.LocalNetworkDtos;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Models
{
    public class PreprovisionDeviceModel
    {
        public Guid DeviceTypeId { get; set; }

        [Required]
        [StringLength(60, MinimumLength = 1, ErrorMessage = "Minimum length 1")]
        public string WifiSSID { get; set; } = null!;

        [Required]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "Minimum length 6")]
        public string WifiPassword { get; set; } = null!;

        public LocalNetworkInfoDto? LocalNetworkInfo { get; set; } = null!;
    }
}
