namespace CoubDownloader
{
    public static class Extensions
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