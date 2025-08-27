using System.ComponentModel.DataAnnotations;

namespace BlazorCamPortal.Core.Utilities
{
    public static class MiscUtilities
    {
        public static string GetDisplayNameAttributeValue(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DisplayAttribute), false)
                .FirstOrDefault() as DisplayAttribute;
            return attribute?.Name ?? value.ToString();
        }
    }
}
