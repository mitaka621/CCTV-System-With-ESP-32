using System.Security.Cryptography;
using System.Text;

namespace Homewatch.Core.Utilities
{
    public static class StringExtensions
    {
        public static byte[] ToHash(this string input)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(input.Normalize(NormalizationForm.FormC)));
        }
    }
}
