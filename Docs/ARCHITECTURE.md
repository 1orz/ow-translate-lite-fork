# Architecture

## Runtime Flow

```text
selected region
  -> GDI screenshot
  -> OneOCR
  -> OW color-mask preprocessing
  -> OW OCR post-processing
  -> OW chat parser
  -> duplicate suppression
  -> DeepSeek / OpenAI-compatible translation
  -> glossary post-processing
  -> overlay records
```

## Main Modules

- `Core/AppSettings.cs`: persisted user settings.
- `Core/ConfigStore.cs`: UTF-8 JSON settings in `%APPDATA%\OWTranslatorLite`.
- `Core/SecretStore.cs`: Windows DPAPI protection for local API keys.
- `Core/SettingsMigrator.cs`: legacy setting normalization and API key migration.
- `Core/OcrTextPostProcessor.cs`: player-boundary repair and wrapped-line merge before parsing.
- `Core/DiagnosticsService.cs`: beta diagnostics, runtime log, dedupe log, and redacted report export.
- `Core/OwGlossaryService.cs`: glossary load, OCR normalization, prompt context, term locking.
- `Core/OwChatParser.cs`: player-chat extraction and Chinese UI filtering.
- `Core/TranslationCoordinator.cs`: capture/OCR/parse/translate loop and duplicate suppression.
- `Core/TranslationQueueStatusTracker.cs`: queue observability for diagnostics.
- `Ocr/OneOcrEngine.cs`: native OneOCR wrapper.
- `Ocr/OcrEngineManager.cs`: OneOCR instance reuse, serialization, and disposal boundary.
- `Ocr/OcrImagePreprocessor.cs`: OW chat color-mask preprocessing presets.
- `Translation/OpenAICompatibleTranslationProvider.cs`: DeepSeek and OpenAI-compatible API.
- `Overlay/OverlayWindow.xaml`: topmost translation overlay.
- `Overlay/OverlayController.cs`: overlay lifecycle and event boundary.
- `AreaSelectorWindow.xaml`: capture region selector.
- `Tools/OcrPreprocessLab`: local OCR preprocessing comparison tool.
- `Tools/GlossaryValidator`: OW glossary maintenance checker.

## Next Iterations

- Add screenshot fixture tests for real OW chat images.
- Continue improving OCR chunk merge and ordered-anchor dedupe.
- Add WGC capture for cases where GDI cannot capture exclusive/fullscreen content.
- Consider migrating from DPAPI settings storage to Windows Credential Manager if the UX needs account-level secret management.
- Remove or hide beta-only test entries before a formal release.
