using System.ComponentModel.DataAnnotations;

namespace CamPortal.Core.Utilities
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

        public static bool ValidateModel<T>(T model)
        {
            if (model == null)
            {
                return false;
            }

            var context = new ValidationContext(model);

            return Validator.TryValidateObject(model, context, null, validateAllProperties: true);
        }
    }
}
