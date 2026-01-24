namespace Cortex.Serialization.Yaml.Common
{
    internal static class StringUtils
    {
        public static string ToSnakeCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }
        public static string ToKebabCase(string s) => ToSnakeCase(s).Replace('_', '-');
        public static string ToCamelCase(string s) => string.IsNullOrEmpty(s) || !char.IsUpper(s[0]) ? s : char.ToLowerInvariant(s[0]) + s[1..];
    }
}
