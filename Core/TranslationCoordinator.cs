using OwTranslateLite.Ocr;
using OwTranslateLite.Translation;

namespace OwTranslateLite.Core;

public sealed class TranslationCoordinator
{
    private readonly AppSettings _settings;
    private readonly OwGlossaryService _glossary;
    private readonly OwChatParser _parser;
    private readonly HashSet<string> _seenInCurrentChatCycle = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _recentDedupeCache = new(StringComparer.Ordinal);
    private DateTime? _lastAnyMessageVisibleAt;
    private static readonly TimeSpan ChatHiddenReset = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ShortRecentDedupeTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultRecentDedupeTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LongRecentDedupeTtl = TimeSpan.FromSeconds(90);
    private const int MaxRecentDedupeItems = 500;

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
        _seenInCurrentChatCycle.Clear();
        _lastAnyMessageVisibleAt = null;
        ChatCycleJustReset = false;
        HasVisibleChat = false;
        if (clearRecent)
        {
            _recentDedupeCache.Clear();
        }
    }

    public async Task<IReadOnlyList<TranslationRecord>> ProcessAsync(IOcrEngine ocrEngine, CancellationToken cancellationToken)
    {
        ChatCycleJustReset = false;
        HasVisibleChat = false;
        if (_settings.CaptureRegion is null)
        {
            return Array.Empty<TranslationRecord>();
        }

        System.Windows.Rect captureRegion = _settings.CaptureRegion.ToRect();
        using System.Drawing.Bitmap bitmap = ScreenCaptureService.Capture(captureRegion);
        IReadOnlyList<OcrTextLine> ocrLines = await ocrEngine.RecognizeAsync(bitmap, _settings.OcrLanguage, cancellationToken);
        IReadOnlyList<ParsedChatLine> chatLines = _parser.Parse(ocrLines);
        if (chatLines.Count == 0)
        {
            ChatCycleJustReset = ResetCycleIfChatStayedHidden();
            return Array.Empty<TranslationRecord>();
        }

        HasVisibleChat = true;
        DateTime now = DateTime.Now;
        CleanupRecentDedupe(now);
        _lastAnyMessageVisibleAt = now;
        ITranslationProvider provider = TranslationProviderFactory.Create(_settings, _glossary);
        List<ParsedChatLine> newLines = [];
        HashSet<string> batchKeys = new(StringComparer.Ordinal);
        foreach (ParsedChatLine line in chatLines)
        {
            string key = CreateMessageKey(line);
            if (_seenInCurrentChatCycle.Contains(key) ||
                IsRecentDuplicate(key, line.SourceText, now) ||
                !batchKeys.Add(key))
            {
                continue;
            }

            newLines.Add(line);
        }

        if (newLines.Count == 0)
        {
            return Array.Empty<TranslationRecord>();
        }

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

    private bool ResetCycleIfChatStayedHidden()
    {
        if (_lastAnyMessageVisibleAt is null)
        {
            return false;
        }

        if (DateTime.Now - _lastAnyMessageVisibleAt.Value >= ChatHiddenReset)
        {
            _seenInCurrentChatCycle.Clear();
            _lastAnyMessageVisibleAt = null;
            return true;
        }

        return false;
    }

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
}
