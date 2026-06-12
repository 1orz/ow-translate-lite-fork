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

        ScriptCounts scripts = CountScripts(message);
        if (scripts.Hangul > 0)
        {
            return false;
        }

        if (scripts.Kana > 0)
        {
            return false;
        }

        if (scripts.Cjk > 0 &&
            scripts.Cjk >= Math.Max(2, scripts.TotalLetters / 2))
        {
            return true;
        }

        if (scripts.Latin > 0)
        {
            return false;
        }

        if (scripts.Cjk > 0)
        {
            return true;
        }

        if (scripts.TotalLetters == 0)
        {
            return true;
        }

        return false;
    }

    private static ScriptCounts CountScripts(string message)
    {
        int hangul = 0;
        int kana = 0;
        int latin = 0;
        int cjk = 0;
        foreach (char ch in message)
        {
            if (ch is >= '\uAC00' and <= '\uD7AF' ||
                ch is >= '\u1100' and <= '\u11FF' ||
                ch is >= '\u3130' and <= '\u318F')
            {
                hangul++;
            }
            else if (ch is >= '\u3040' and <= '\u30FF')
            {
                kana++;
            }
            else if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                latin++;
            }
            else if (ch is >= '\u4E00' and <= '\u9FFF')
            {
                cjk++;
            }
        }

        return new ScriptCounts(hangul, kana, latin, cjk);
    }

    private sealed record ScriptCounts(int Hangul, int Kana, int Latin, int Cjk)
    {
        public int TotalLetters => Hangul + Kana + Latin + Cjk;
    }
}
