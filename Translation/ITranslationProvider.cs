using OwTranslateLite.Core;

namespace OwTranslateLite.Translation;

public interface ITranslationProvider
{
    string Name { get; }
    Task<string> TranslateAsync(ParsedChatLine line, CancellationToken cancellationToken);
}
