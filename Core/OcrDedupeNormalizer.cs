using System.Text.RegularExpressions;

namespace OwTranslateLite.Core;

public static class OcrDedupeNormalizer
{
    public static string NormalizeText(string value)
    {
        string normalized = value.ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^\p{L}\p{N}]+", " ");
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    public static string NormalizeSpeaker(string value)
    {
        string lower = value.ToLowerInvariant();
        return Regex.Replace(lower, @"[^\p{L}\p{N}]+", "");
    }

    public static bool IsSpeakerMatch(string left, string right)
    {
        if (left == right)
        {
            return true;
        }

        int limit = Math.Min(left.Length, right.Length);
        if (limit < 5 || Math.Abs(left.Length - right.Length) > 1)
        {
            return false;
        }

        return LevenshteinDistance(left, right) <= 1;
    }

    public static bool IsSimilarText(string left, string right)
        => TextSimilarityScore(left, right) >= 0.76;

    public static double TextSimilarityScore(string left, string right)
    {
        if (left == right)
        {
            return 1;
        }

        if (left.Length < 8 || right.Length < 8)
        {
            return 0;
        }

        double best = 0;
        string shorter = left.Length <= right.Length ? left : right;
        string longer = left.Length <= right.Length ? right : left;
        if (longer.Contains(shorter, StringComparison.Ordinal) &&
            shorter.Length >= Math.Max(8, (int)(longer.Length * 0.65)))
        {
            best = Math.Max(best, (double)shorter.Length / longer.Length);
        }

        int commonPrefix = 0;
        int limit = Math.Min(left.Length, right.Length);
        while (commonPrefix < limit && left[commonPrefix] == right[commonPrefix])
        {
            commonPrefix++;
        }

        if (commonPrefix >= Math.Max(8, (int)(limit * 0.75)))
        {
            best = Math.Max(best, (double)commonPrefix / Math.Max(left.Length, right.Length));
        }

        best = Math.Max(best, TokenOverlapRatio(left, right));
        best = Math.Max(best, CharacterDiceRatio(left, right));
        best = Math.Max(best, 1.0 - ((double)LevenshteinDistance(left, right) / Math.Max(left.Length, right.Length)));
        return best;
    }

    private static double TokenOverlapRatio(string left, string right)
    {
        string[] leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (leftTokens.Length == 0 || rightTokens.Length == 0)
        {
            return 0;
        }

        int matched = 0;
        bool[] used = new bool[rightTokens.Length];
        foreach (string leftToken in leftTokens)
        {
            for (int i = 0; i < rightTokens.Length; i++)
            {
                if (used[i])
                {
                    continue;
                }

                if (leftToken == rightTokens[i] || AreTokensSimilar(leftToken, rightTokens[i]))
                {
                    used[i] = true;
                    matched++;
                    break;
                }
            }
        }

        return (double)matched / Math.Max(leftTokens.Length, rightTokens.Length);
    }

    private static bool AreTokensSimilar(string left, string right)
    {
        int minLength = Math.Min(left.Length, right.Length);
        if (minLength < 3 || Math.Abs(left.Length - right.Length) > 1)
        {
            return false;
        }

        return LevenshteinDistance(left, right) <= 1;
    }

    private static double CharacterDiceRatio(string left, string right)
    {
        string compactLeft = left.Replace(" ", "", StringComparison.Ordinal);
        string compactRight = right.Replace(" ", "", StringComparison.Ordinal);
        if (compactLeft.Length < 2 || compactRight.Length < 2)
        {
            return 0;
        }

        Dictionary<string, int> leftBigrams = CountBigrams(compactLeft);
        Dictionary<string, int> rightBigrams = CountBigrams(compactRight);
        int overlap = 0;
        foreach ((string key, int leftCount) in leftBigrams)
        {
            if (rightBigrams.TryGetValue(key, out int rightCount))
            {
                overlap += Math.Min(leftCount, rightCount);
            }
        }

        return (2.0 * overlap) / (compactLeft.Length - 1 + compactRight.Length - 1);
    }

    private static Dictionary<string, int> CountBigrams(string value)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        for (int i = 0; i < value.Length - 1; i++)
        {
            string bigram = value.Substring(i, 2);
            counts[bigram] = counts.TryGetValue(bigram, out int count) ? count + 1 : 1;
        }

        return counts;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];

        for (int j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= right.Length; j++)
            {
                int cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
