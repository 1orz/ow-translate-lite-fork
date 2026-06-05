using OwTranslateLite.Ocr;
using OwTranslateLite.Translation;

namespace OwTranslateLite.Core;

public sealed class TranslationCoordinator
{
    private readonly AppSettings _settings;
    private readonly OwGlossaryService _glossary;
    private readonly OwChatParser _parser;
    private readonly List<VisibleMessageSnapshot> _previousVisibleMessages = [];
    private readonly HashSet<string> _seenInCurrentChatCycle = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pendingMessageKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _recentDedupeCache = new(StringComparer.Ordinal);
    private DateTime? _lastAnyMessageVisibleAt;
    private static readonly TimeSpan ChatHiddenReset = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ShortRecentDedupeTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultRecentDedupeTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LongRecentDedupeTtl = TimeSpan.FromSeconds(90);
    private const int MaxRecentDedupeItems = 500;
    private const int MaxTailMessagesWithoutAnchor = 2;

    public bool ChatCycleJustReset { get; private set; }
    public bool HasVisibleChat { get; private set; }

    public TranslationCoordinator(AppSettings settings, OwGlossaryService glossary)
    {
        _settings = settings;
        _glossary = glossary;
        _parser = new OwChatParser(glossary);
    }

    public void ResetChatCycle(bool clearRecent = false)
    {
        _previousVisibleMessages.Clear();
        _seenInCurrentChatCycle.Clear();
        _pendingMessageKeys.Clear();
        _lastAnyMessageVisibleAt = null;
        ChatCycleJustReset = false;
        HasVisibleChat = false;
        if (clearRecent)
        {
            _recentDedupeCache.Clear();
        }
    }

    public void ClearPendingTranslations()
    {
        _pendingMessageKeys.Clear();
    }

    public void ReleasePendingTranslations(IReadOnlyList<ParsedChatLine> lines)
    {
        foreach (ParsedChatLine line in lines)
        {
            _pendingMessageKeys.Remove(CreateMessageKey(line));
        }
    }

    public async Task<IReadOnlyList<TranslationRecord>> ProcessAsync(IOcrEngine ocrEngine, CancellationToken cancellationToken)
    {
        IReadOnlyList<ParsedChatLine> lines = await DetectNewLinesAsync(ocrEngine, cancellationToken);
        return await TranslateAsync(lines, cancellationToken);
    }

    public async Task<IReadOnlyList<ParsedChatLine>> DetectNewLinesAsync(IOcrEngine ocrEngine, CancellationToken cancellationToken)
    {
        ChatCycleJustReset = false;
        HasVisibleChat = false;
        if (_settings.CaptureRegion is null)
        {
            return Array.Empty<ParsedChatLine>();
        }

        System.Windows.Rect captureRegion = _settings.CaptureRegion.ToRect();
        using System.Drawing.Bitmap bitmap = ScreenCaptureService.Capture(captureRegion);
        IReadOnlyList<OcrTextLine> ocrLines = await ocrEngine.RecognizeAsync(bitmap, _settings.OcrLanguage, cancellationToken);
        IReadOnlyList<ParsedChatLine> chatLines = _parser.Parse(ocrLines);
        if (chatLines.Count == 0)
        {
            ChatCycleJustReset = ResetCycleIfChatStayedHidden();
            return Array.Empty<ParsedChatLine>();
        }

        HasVisibleChat = true;
        DateTime now = DateTime.Now;
        CleanupRecentDedupe(now);
        _lastAnyMessageVisibleAt = now;
        List<ParsedChatLine> candidateLines = FindNewLinesByVisibleOrder(chatLines);
        UpdatePreviousVisibleMessages(chatLines);

        List<ParsedChatLine> newLines = [];
        HashSet<string> batchKeys = new(StringComparer.Ordinal);
        foreach (ParsedChatLine line in candidateLines)
        {
            string key = CreateMessageKey(line);
            if (_seenInCurrentChatCycle.Contains(key) ||
                _pendingMessageKeys.Contains(key) ||
                IsRecentDuplicate(key, line.SourceText, now) ||
                !batchKeys.Add(key))
            {
                continue;
            }

            newLines.Add(line);
            _pendingMessageKeys.Add(key);
        }

        return newLines;
    }

    public async Task<IReadOnlyList<TranslationRecord>> TranslateAsync(IReadOnlyList<ParsedChatLine> newLines, CancellationToken cancellationToken)
    {
        if (newLines.Count == 0)
        {
            return Array.Empty<TranslationRecord>();
        }

        try
        {
            ITranslationProvider provider = TranslationProviderFactory.Create(_settings, _glossary);
            IReadOnlyList<TranslationResult> translations = await provider.TranslateAsync(newLines, cancellationToken);
            List<TranslationRecord> records = [];
            foreach (TranslationResult result in translations)
            {
                if (string.IsNullOrWhiteSpace(result.TranslatedText))
                {
                    continue;
                }

                records.Add(new TranslationRecord(
                    result.SourceLine.Speaker,
                    result.SourceLine.SourceText,
                    result.TranslatedText,
                    DateTime.Now));
                string key = CreateMessageKey(result.SourceLine);
                _seenInCurrentChatCycle.Add(key);
                _recentDedupeCache[key] = DateTime.Now;
            }

            return records;
        }
        finally
        {
            foreach (ParsedChatLine line in newLines)
            {
                _pendingMessageKeys.Remove(CreateMessageKey(line));
            }
        }
    }

    private bool ResetCycleIfChatStayedHidden()
    {
        if (_lastAnyMessageVisibleAt is null)
        {
            return false;
        }

        if (DateTime.Now - _lastAnyMessageVisibleAt.Value >= ChatHiddenReset)
        {
            _previousVisibleMessages.Clear();
            _seenInCurrentChatCycle.Clear();
            _pendingMessageKeys.Clear();
            _lastAnyMessageVisibleAt = null;
            return true;
        }

        return false;
    }

    private List<ParsedChatLine> FindNewLinesByVisibleOrder(IReadOnlyList<ParsedChatLine> currentLines)
    {
        if (_previousVisibleMessages.Count == 0)
        {
            return currentLines.TakeLast(Math.Min(currentLines.Count, MaxTailMessagesWithoutAnchor)).ToList();
        }

        List<VisibleMessageSnapshot> current = currentLines.Select(CreateSnapshot).ToList();
        int anchorIndex = FindBestAnchorIndex(current);
        if (anchorIndex >= 0)
        {
            return currentLines.Skip(anchorIndex + 1).ToList();
        }

        return currentLines.TakeLast(Math.Min(currentLines.Count, MaxTailMessagesWithoutAnchor)).ToList();
    }

    private int FindBestAnchorIndex(IReadOnlyList<VisibleMessageSnapshot> current)
    {
        for (int currentIndex = current.Count - 1; currentIndex >= 0; currentIndex--)
        {
            VisibleMessageSnapshot currentMessage = current[currentIndex];
            for (int previousIndex = _previousVisibleMessages.Count - 1; previousIndex >= 0; previousIndex--)
            {
                if (!IsAnchorMatch(currentMessage, _previousVisibleMessages[previousIndex]))
                {
                    continue;
                }

                if (HasNeighborSupport(current, currentIndex, previousIndex) || IsStrongAnchor(currentMessage))
                {
                    return currentIndex;
                }
            }
        }

        return -1;
    }

    private bool HasNeighborSupport(IReadOnlyList<VisibleMessageSnapshot> current, int currentIndex, int previousIndex)
    {
        bool previousNeighborMatches = currentIndex > 0 &&
                                       previousIndex > 0 &&
                                       IsAnchorMatch(current[currentIndex - 1], _previousVisibleMessages[previousIndex - 1]);
        bool nextNeighborMatches = currentIndex + 1 < current.Count &&
                                   previousIndex + 1 < _previousVisibleMessages.Count &&
                                   IsAnchorMatch(current[currentIndex + 1], _previousVisibleMessages[previousIndex + 1]);
        return previousNeighborMatches || nextNeighborMatches;
    }

    private static bool IsStrongAnchor(VisibleMessageSnapshot message) =>
        message.NormalizedText.Length >= 12;

    private static bool IsAnchorMatch(VisibleMessageSnapshot left, VisibleMessageSnapshot right)
    {
        if (!string.Equals(left.NormalizedSpeaker, right.NormalizedSpeaker, StringComparison.Ordinal))
        {
            return false;
        }

        if (left.NormalizedText == right.NormalizedText)
        {
            return true;
        }

        return IsSimilarText(left.NormalizedText, right.NormalizedText);
    }

    private static bool IsSimilarText(string left, string right)
    {
        if (left.Length < 8 || right.Length < 8)
        {
            return false;
        }

        string shorter = left.Length <= right.Length ? left : right;
        string longer = left.Length <= right.Length ? right : left;
        if (longer.Contains(shorter, StringComparison.Ordinal) &&
            shorter.Length >= Math.Max(8, (int)(longer.Length * 0.65)))
        {
            return true;
        }

        int commonPrefix = 0;
        int limit = Math.Min(left.Length, right.Length);
        while (commonPrefix < limit && left[commonPrefix] == right[commonPrefix])
        {
            commonPrefix++;
        }

        return commonPrefix >= Math.Max(8, (int)(limit * 0.75));
    }

    private void UpdatePreviousVisibleMessages(IReadOnlyList<ParsedChatLine> currentLines)
    {
        _previousVisibleMessages.Clear();
        _previousVisibleMessages.AddRange(currentLines.Select(CreateSnapshot));
    }

    private static VisibleMessageSnapshot CreateSnapshot(ParsedChatLine line) =>
        new(NormalizeForHash(line.Speaker), NormalizeForHash(line.SourceText));

    private bool IsRecentDuplicate(string key, string text, DateTime now)
    {
        if (!_recentDedupeCache.TryGetValue(key, out DateTime lastSeenAt))
        {
            return false;
        }

        return now - lastSeenAt < GetRecentDedupeTtl(text);
    }

    private void CleanupRecentDedupe(DateTime now)
    {
        foreach (KeyValuePair<string, DateTime> item in _recentDedupeCache.ToList())
        {
            if (now - item.Value >= LongRecentDedupeTtl)
            {
                _recentDedupeCache.Remove(item.Key);
            }
        }

        if (_recentDedupeCache.Count <= MaxRecentDedupeItems)
        {
            return;
        }

        foreach (string key in _recentDedupeCache
                     .OrderBy(item => item.Value)
                     .Take(_recentDedupeCache.Count - MaxRecentDedupeItems)
                     .Select(item => item.Key)
                     .ToList())
        {
            _recentDedupeCache.Remove(key);
        }
    }

    private static TimeSpan GetRecentDedupeTtl(string text)
    {
        int length = NormalizeForHash(text).Length;
        return length switch
        {
            <= 12 => ShortRecentDedupeTtl,
            >= 50 => LongRecentDedupeTtl,
            _ => DefaultRecentDedupeTtl
        };
    }

    private static string CreateMessageKey(ParsedChatLine line) =>
        $"{NormalizeForHash(line.Speaker)}:{NormalizeForHash(line.SourceText)}";

    private static string NormalizeForHash(string value)
    {
        string lower = value.ToLowerInvariant();
        lower = System.Text.RegularExpressions.Regex.Replace(lower, @"[^\p{L}\p{N}]+", " ");
        return System.Text.RegularExpressions.Regex.Replace(lower, @"\s+", " ").Trim();
    }

    private sealed record VisibleMessageSnapshot(string NormalizedSpeaker, string NormalizedText);
}
