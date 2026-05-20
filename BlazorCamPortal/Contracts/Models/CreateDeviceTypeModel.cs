using CamPortal.Contracts.Enums;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Models
{
    public class CreateDeviceTypeModel
    {
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required.")]
        [EnumDataType(typeof(DeviceTypeCategories), ErrorMessage = "Category is not a recognized value.")]
        public DeviceTypeCategories DeviceVariant { get; set; }

        [Required(ErrorMessage = "Icon file is required.")]
        public IBrowserFile? IconFile { get; set; }
    }
}
