using System;
using System.Text.RegularExpressions;

namespace CoubDownloader.Extensions
{
    public static class ConsoleEx
    {
        /// <summary>
        ///     Write colored messages.
        ///     Example: WriteColor("This is my [message] with inline [color] changes.", ConsoleColor.Yellow);
        /// </summary>
        /// <param name="message">Message to write.</param>
        /// <param name="color">Target color.</param>
        public static void WriteLineColor(string message, ConsoleColor color)
        {
            WriteColor(message, color);
            Console.WriteLine();
        }
        
        public static void WriteColor(string message, ConsoleColor color)
        {
            var parts = Regex.Split(message, @"(\[[^\]]*\])");

            foreach (var part in parts)
            {
                var text = part;
        
                if (text.StartsWith("[") && text.EndsWith("]"))
                {
                    Console.ForegroundColor = color;
                    text = text.Substring(1,text.Length-2);          
                }
        
                Console.Write(text);
                Console.ResetColor();
            }
        }
    }
}