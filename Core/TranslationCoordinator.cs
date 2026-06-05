using System.Security.Cryptography;
using System.Text;
using OwTranslateLite.Ocr;
using OwTranslateLite.Translation;

namespace OwTranslateLite.Core;

public sealed class TranslationCoordinator
{
    private readonly AppSettings _settings;
    private readonly OwGlossaryService _glossary;
    private readonly OwChatParser _parser;
    private readonly Dictionary<string, DateTime> _recentHashes = new();

    public TranslationCoordinator(AppSettings settings, OwGlossaryService glossary)
    {
        _settings = settings;
        _glossary = glossary;
        _parser = new OwChatParser(glossary);
    }

    public async Task<IReadOnlyList<TranslationRecord>> ProcessAsync(IOcrEngine ocrEngine, CancellationToken cancellationToken)
    {
        if (_settings.CaptureRegion is null)
        {
            return Array.Empty<TranslationRecord>();
        }

        using System.Drawing.Bitmap bitmap = ScreenCaptureService.Capture(_settings.CaptureRegion.ToRect());
        IReadOnlyList<OcrTextLine> ocrLines = await ocrEngine.RecognizeAsync(bitmap, _settings.OcrLanguage, cancellationToken);
        IReadOnlyList<ParsedChatLine> chatLines = _parser.Parse(ocrLines);
        if (chatLines.Count == 0)
        {
            return Array.Empty<TranslationRecord>();
        }

        ITranslationProvider provider = TranslationProviderFactory.Create(_settings, _glossary);
        List<TranslationRecord> records = [];
        foreach (ParsedChatLine line in chatLines)
        {
            string hash = Hash($"{line.Speaker}:{line.SourceText}");
            if (IsRecentDuplicate(hash))
            {
                continue;
            }

            string translated = await provider.TranslateAsync(line, cancellationToken);
            if (!string.IsNullOrWhiteSpace(translated))
            {
                records.Add(new TranslationRecord(line.Speaker, line.SourceText, translated, DateTime.Now));
                _recentHashes[hash] = DateTime.Now;
            }
        }

        CleanupHashes();
        return records;
    }

    private bool IsRecentDuplicate(string hash)
    {
        return _recentHashes.TryGetValue(hash, out DateTime lastSeen) &&
               DateTime.Now - lastSeen < TimeSpan.FromSeconds(8);
    }

    private void CleanupHashes()
    {
        foreach (string key in _recentHashes.Where(pair => DateTime.Now - pair.Value > TimeSpan.FromSeconds(30)).Select(pair => pair.Key).ToList())
        {
            _recentHashes.Remove(key);
        }
    }

    private static string Hash(string value)
    {
        byte[] data = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(data);
    }
}
