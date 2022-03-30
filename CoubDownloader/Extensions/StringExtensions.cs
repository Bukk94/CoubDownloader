namespace CoubDownloader.Extensions
{
    public static class StringExtensions
    {
        public static string RemoveLinebreaks(this string text)
        {
            return text
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace("&#13;", "")
                .Replace("&#10;", "");
        }
    }
}