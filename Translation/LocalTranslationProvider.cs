using OwTranslateLite.Core;

namespace OwTranslateLite.Translation;

public sealed class LocalTranslationProvider : ITranslationProvider
{
    private readonly OwGlossaryService _glossary;

    public LocalTranslationProvider(OwGlossaryService glossary)
    {
        _glossary = glossary;
    }

    public string Name => "Local";

    public Task<string> TranslateAsync(ParsedChatLine line, CancellationToken cancellationToken)
    {
        string translated = _glossary.TryLocalTranslate(line.SourceText);
        return Task.FromResult(_glossary.ApplyTerms(translated));
    }
}
