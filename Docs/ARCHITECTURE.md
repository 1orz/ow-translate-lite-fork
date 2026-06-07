# Architecture

## Runtime Flow

```text
selected region
  -> GDI screenshot
  -> OneOCR
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
- `Ocr/WindowsOcrEngine.cs`: legacy/non-default Windows OCR wrapper; OneOCR is the maintained path.
- `Translation/OpenAICompatibleTranslationProvider.cs`: DeepSeek and OpenAI-compatible API.
- `Overlay/OverlayWindow.xaml`: topmost translation overlay.
- `AreaSelectorWindow.xaml`: capture region selector.

## Next Iterations

- Add screenshot fixture tests for real OW chat images.
- Continue improving OCR chunk merge and ordered-anchor dedupe.
- Add per-language OCR presets for EN/JA/KO chat.
- Add WGC capture for cases where GDI cannot capture exclusive/fullscreen content.
- Encrypt the local API key with DPAPI or the Windows credential store.
- Hide or remove beta-only test entries and `Local Rules` before a formal release.
