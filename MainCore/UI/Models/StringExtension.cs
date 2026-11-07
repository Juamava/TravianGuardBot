using System.Text.RegularExpressions;

namespace MainCore.UI.Models
{
    public static partial class StringExtension
    {
        [GeneratedRegex(@"[^a-zA-Z0-9\s]")]
        private static partial Regex NonAlphanumericRegex();

        public static string Sanitize(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            return NonAlphanumericRegex().Replace(input, "").Replace(' ', '_');
        }

        public static string GetServerUrl(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            var value = input.Trim();
            if (!value.Contains("://", StringComparison.Ordinal))
                value = "https://" + value;

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
                return "";

            if (uri.Scheme is not ("http" or "https"))
                return "";

            return $"{uri.Scheme}://{uri.Host}";
        }
    }
}