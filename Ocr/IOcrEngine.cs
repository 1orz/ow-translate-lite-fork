using System.Drawing;
using OwTranslateLite.Core;

namespace OwTranslateLite.Ocr;

public interface IOcrEngine
{
    string Name { get; }
    Task<IReadOnlyList<OcrTextLine>> RecognizeAsync(Bitmap bitmap, string languageCode, CancellationToken cancellationToken);
}
