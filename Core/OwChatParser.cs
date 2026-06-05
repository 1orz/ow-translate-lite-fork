using System.Text.RegularExpressions;

namespace OwTranslateLite.Core;

public sealed class OwChatParser
{
    private readonly OwGlossaryService _glossary;

    public OwChatParser(OwGlossaryService glossary)
    {
        _glossary = glossary;
    }

    public IReadOnlyList<ParsedChatLine> Parse(IReadOnlyList<OcrTextLine> lines)
    {
        List<ParsedChatLine> parsed = [];
        foreach (OcrTextLine line in lines.OrderBy(line => line.Bounds.Top))
        {
            string normalized = _glossary.NormalizeOcrText(line.Text);
            if (_glossary.ShouldIgnoreLine(normalized))
            {
                continue;
            }

            foreach ((string speaker, string message) in ExtractPlayerMessages(normalized))
            {
                if (ShouldSkipMessage(message))
                {
                    continue;
                }

                IReadOnlyList<GlossaryHit> hits = _glossary.FindHits(message);
                parsed.Add(new ParsedChatLine(speaker, message, line.Bounds, hits));
            }
        }

        return parsed;
    }

    private static IReadOnlyList<(string Speaker, string Message)> ExtractPlayerMessages(string text)
    {
        List<(string Speaker, string Message)> messages = [];

        foreach (Match match in Regex.Matches(
            text,
            @"\[(?<speaker>[^\]\r\n]{2,24})\]\s*[:：]\s*(?<message>.*?)(?=\s*\[[^\]\r\n]{2,24}\]\s*[:：]|$)"))
        {
            string speaker = match.Groups["speaker"].Value.Trim();
            string message = match.Groups["message"].Value.Trim();
            if (speaker.Length == 0 || message.Length == 0)
            {
                continue;
            }

            messages.Add((speaker, message));
        }

        return messages;
    }

    private static bool ShouldSkipMessage(string message)
    {
        if (message.Length < 2)
        {
            return true;
        }

        if (Regex.IsMatch(message, @"^[\u4e00-\u9fff\s，。！？、：；（）《》0-9a-zA-Z#_-]+$") &&
            Regex.Matches(message, @"[\u4e00-\u9fff]").Count >= Math.Max(2, message.Length / 3))
        {
            return true;
        }

        if (!Regex.IsMatch(message, @"[A-Za-zぁ-んァ-ン가-힣]"))
        {
            return true;
        }

        return false;
    }
}
