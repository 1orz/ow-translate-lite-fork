# Architecture

## Runtime Flow

```text
selected region
  -> GDI screenshot
  -> OneOCR or Windows OCR
  -> OW OCR cleanup
  -> OW chat parser
  -> duplicate suppression
  -> Local / DeepSeek / OpenAI-compatible translation
  -> glossary post-processing
  -> overlay records
```

## Main Modules

- `Core/AppSettings.cs`: persisted user settings.
- `Core/ConfigStore.cs`: UTF-8 JSON settings in `%APPDATA%\OWTranslatorLite`.
- `Core/OwGlossaryService.cs`: glossary load, OCR normalization, local rewrites, term locking.
- `Core/OwChatParser.cs`: player-chat extraction and Chinese UI filtering.
- `Core/TranslationCoordinator.cs`: capture/OCR/parse/translate loop and duplicate suppression.
- `Ocr/OneOcrEngine.cs`: native OneOCR wrapper.
- `Ocr/WindowsOcrEngine.cs`: Windows Media OCR wrapper.
- `Translation/OpenAICompatibleTranslationProvider.cs`: DeepSeek and OpenAI-compatible API.
- `Overlay/OverlayWindow.xaml`: topmost translation overlay.
- `AreaSelectorWindow.xaml`: capture region selector.

## Next Iterations

- Add screenshot fixture tests for real OW chat images.
- Add per-language OCR presets for EN/JA/KO/RU mixed chat.
- Add WGC capture for cases where GDI cannot capture exclusive/fullscreen content.
- Add import/export for glossary overrides.
