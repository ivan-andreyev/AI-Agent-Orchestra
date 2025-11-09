namespace Orchestra.Core.Utilities;

/// <summary>
/// Utility class for Telegram Markdown V2 formatting
/// </summary>
/// <remarks>
/// Provides helper methods to escape special characters for Telegram's MarkdownV2 format
/// </remarks>
public static class TelegramMarkdownHelper
{
    /// <summary>
    /// Escapes special characters for Markdown V2 format in Telegram
    /// </summary>
    /// <param name="text">Text to escape</param>
    /// <returns>Escaped text safe for Telegram MarkdownV2</returns>
    public static string EscapeMarkdownV2(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Special characters in MarkdownV2 that need to be escaped
        var specialChars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };

        var result = text;
        foreach (var ch in specialChars)
        {
            result = result.Replace(ch.ToString(), $"\\{ch}");
        }

        return result;
    }
}
