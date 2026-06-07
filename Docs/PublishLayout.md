# Publish Layout Notes

This project is currently a self-contained Windows x64 WPF app. For self-contained .NET desktop apps, many runtime and framework DLLs are expected beside the executable. Moving those DLLs into arbitrary subfolders can break host/dependency resolution.

Current safe layout goals:

- Keep `OWTranslatorLite.exe` and required .NET host/runtime DLLs in the publish root.
- Keep OneOCR native files under `OneOcr\`.
- Keep glossary and app resources under `Resources\`.
- Keep optional design candidates under `Resources\UI\`.
- Keep beta tester notes as `README-BETA.md` only when preparing a tester package.
- Do not publish or zip from routine code changes; build locally first and package only after manual test approval.

Future packaging options to evaluate before a release:

- framework-dependent publish to reduce bundled runtime files, if testers can install the required .NET Desktop Runtime;
- installer-based layout that hides runtime files under an installation directory instead of changing runtime probing paths;
- signed executable and explicit app icon once the UI asset candidate is selected.
