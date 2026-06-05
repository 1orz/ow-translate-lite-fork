using System.Text.RegularExpressions;
using System.Windows;

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

            if (!TryExtractPlayerMessage(normalized, out string speaker, out string message))
            {
                continue;
            }

            if (ShouldSkipMessage(message))
            {
                continue;
            }

            IReadOnlyList<GlossaryHit> hits = _glossary.FindHits(message);
            parsed.Add(new ParsedChatLine(speaker, message, line.Bounds, hits));
        }

        return MergeNearbyLines(parsed);
    }

    private static bool TryExtractPlayerMessage(string text, out string speaker, out string message)
    {
        speaker = "";
        message = "";

        string cleaned = Regex.Replace(text, @"^\s*(\[?(TEAM|MATCH|GROUP|队伍|比赛|小队|团队|전체|팀|マッチ|チーム)\]?\s*)+", "", RegexOptions.IgnoreCase).Trim();
        Match match = Regex.Match(cleaned, @"^(?<speaker>[^:：]{2,24})[:：]\s*(?<message>.+)$");
        if (!match.Success)
        {
            return false;
        }

        speaker = match.Groups["speaker"].Value.Trim();
        message = match.Groups["message"].Value.Trim();
        if (speaker.Length == 0 || message.Length == 0)
        {
            return false;
        }

        if (Regex.IsMatch(speaker, @"[\u4e00-\u9fff]{4,}") && !Regex.IsMatch(speaker, @"[#\d]"))
        {
            return false;
        }

        return true;
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

        if (!Regex.IsMatch(message, @"[A-Za-zА-Яа-яぁ-んァ-ン가-힣]"))
        {
            return true;
        }

        return false;
    }

    private static IReadOnlyList<ParsedChatLine> MergeNearbyLines(List<ParsedChatLine> lines)
    {
        if (lines.Count <= 1)
        {
            return lines;
        }

        List<ParsedChatLine> merged = [];
        foreach (ParsedChatLine line in lines)
        {
            ParsedChatLine? previous = merged.LastOrDefault();
            if (previous is not null &&
                previous.Speaker.Equals(line.Speaker, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(line.Bounds.Top - previous.Bounds.Bottom) < 18)
            {
                Rect bounds = Rect.Union(previous.Bounds, line.Bounds);
                List<GlossaryHit> hits = [.. previous.GlossaryHits, .. line.GlossaryHits];
                merged[^1] = previous with
                {
                    SourceText = $"{previous.SourceText} {line.SourceText}",
                    Bounds = bounds,
                    GlossaryHits = hits.GroupBy(hit => hit.Target).Select(group => group.First()).ToList()
                };
            }
            else
            {
                merged.Add(line);
            }
        }

        return merged;
    }
}
