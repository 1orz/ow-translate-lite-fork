# OW Translator Lite

<p align="center">
  <img src="Resources/UI/ow-translator-lite-icon.png" alt="OW Translator Lite" width="96" height="96">
</p>

<p align="center">
  <strong>OW 专用实时聊天翻译 overlay / Real-time Overwatch chat translation overlay</strong>
</p>

<p align="center">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square">
  <img alt="Platform" src="https://img.shields.io/badge/Windows-x64-0078D4?style=flat-square">
  <img alt="OCR" src="https://img.shields.io/badge/OCR-OneOCR-00A8E8?style=flat-square">
  <img alt="Status" src="https://img.shields.io/badge/status-0.2.0--beta.4--ui-34C759?style=flat-square">
</p>

<p align="center">
  <a href="#中文">中文</a> · <a href="#english">English</a>
</p>

---

## 中文

OW Translator Lite 是一个面向《守望先锋 2》外服对局聊天的轻量 Windows OCR 翻译工具。它不是通用屏幕翻译器，而是专注于 OW 聊天区域：截取玩家框选区域，使用本地 OneOCR 自动识别，通过 DeepSeek 或 OpenAI-compatible API 翻译为简体中文，并把译文按聊天顺序显示在 overlay 中。

当前版本：`0.2.0-beta.4-ui`

### 当前进度

- 已完成 Timeline 重构：消息身份由权威 `ChatTimeline.Seq` 决定，不再依赖内容判重。
- 已移除旧判重路径：包括 per-cycle seen、recent TTL cache、pending fuzzy match、显示层重复过滤、anchor/tail 截断等。
- 已加入多帧共识：新消息连续两帧一致，或韩语 jamo 距离足够近时才入队翻译；不稳定 OCR 会再等确认帧。
- 已加入像素 diff 巡逻采样：稳定画面只做低成本截图签名，变化后才进入 2-3 帧 OCR 突发采样。
- 已增强韩语 OCR 容错：支持去空格比较、Hangul NFD/jamo 级距离、短韩语消息评分、jamo 混淆代价矩阵。
- 已优化 overlay：译文按 `Seq` 排序回填，翻译失败保留原顺序显式重试，聊天可见性改为电平触发。
- 已改造 Apple Dark Console UI：深色控制台主界面、翻译框设置、Beta Tools 折叠区、HUD 风格 overlay。
- 已提供 ReplayLab：支持录制帧序列、离线回放、漏翻/重复/乱序断言和韩语 jamo 回归。

### 功能亮点

- **OW 专用识别流程**：面向 `[玩家名]: 正文` 聊天结构，过滤中文系统/UI 提示。
- **本地 OCR**：固定 OneOCR 自动识别，不依赖云 OCR。
- **实时翻译**：支持 DeepSeek 和 OpenAI-compatible chat completions API。
- **术语表增强**：内置 OW 英雄、技能、地图、模式、常见英语/日语/韩语叫法和中文社区术语。
- **顺序稳定**：使用 Timeline 对齐和 `Seq` 排序，降低开聊天历史、OCR 抖动、多人连续发言时的重复/乱序。
- **透明 overlay**：置顶显示译文，支持常态显示、鼠标穿透、拖动、缩放、滚动历史。
- **回话助手**：在 overlay 底部输入中文，翻译为英语/日语/韩语并复制到剪贴板，不自动发送游戏聊天。
- **隐私保护**：API Key 使用 Windows DPAPI 存储；诊断导出会脱敏。

### 明确不做

为了保持竞技场景的实时性和可维护性，本项目不会主动恢复或加入以下方向：

- WinOCR、多 OCR 服务切换、多翻译商复杂 UI。
- TTS、Whisper、语音聊天翻译。
- Local Rules 离线规则翻译路径。
- 通用剪贴板监听翻译、漫画模式、全场景屏幕翻译器。
- 除英语、日语、韩语以外的语言扩展，除非后续有明确需求和测试样本。

### 快速开始

1. 启动 `OWTranslatorLite.exe`。
2. 在翻译 API 中选择 `DeepSeek` 或 `OpenAI Compatible`。
3. DeepSeek 默认 API URL：`https://api.deepseek.com`。
4. 填入 API Key，点击获取模型，选择模型，例如 `deepseek-v4-flash`。
5. 点击“选择翻译区域”，框选 OW 左侧聊天区域，确保完整包含玩家名和正文。
6. 点击“开始”。
7. 如需固定显示 overlay，打开“常态显示 overlay”；如需拖动或滚动 overlay，可关闭鼠标穿透。

### 推荐测试样例

可以先在记事本或 OW 自定义房间中测试类似聊天行：

```text
[TEAM] Reverieach: group up
[TEAM] 疯狂的鹿: 힐좀
[TEAM] 天剑若叶: 위도우 조심
[MATCH] kiriMain: suzu no
[MATCH] genji99: nano blade soon
```

期望结果：

- 玩家消息被识别并翻译。
- 同一行 OCR 抖动不会反复翻译。
- 韩语短消息不会因为过短被丢弃。
- 多人连续发言时 overlay 顺序保持一致。

### 构建

推荐使用仓库同级目录的本地 .NET SDK：

```powershell
& "E:\rstgametranslation\.dotnet\dotnet.exe" build OwTranslateLite.csproj -c Release
```

发布测试包：

```powershell
& "E:\rstgametranslation\.dotnet\dotnet.exe" publish OwTranslateLite.csproj -c Release -o E:\rstgametranslation\ow-translate-lite\dist\OWTranslatorLite-v0.2.0-beta.4-ui-portable-win-x64
```

### 回归测试

```powershell
& "E:\rstgametranslation\.dotnet\dotnet.exe" run --project Tools\ReplayLab\ReplayLab.csproj -c Release -- --timeline-smoke
& "E:\rstgametranslation\.dotnet\dotnet.exe" run --project Tools\ReplayLab\ReplayLab.csproj -c Release -- --similarity Tools\ReplayLab\similarity\korean-jamo-regression.json
& "E:\rstgametranslation\.dotnet\dotnet.exe" run --project Tools\ReplayLab\ReplayLab.csproj -c Release -- Tools\ReplayLab\fixtures\smoke-korean-short Tools\ReplayLab\fixtures\smoke-korean-short\expected.json
& "E:\rstgametranslation\.dotnet\dotnet.exe" build OwTranslateLite.csproj -c Release
```

核心期望：

```text
missing=0, duplicates=0, outOfOrder=0, extra=0
```

### 目录结构

```text
Core/                 设置、Timeline、解析、诊断、翻译协调
Ocr/                  OneOCR 封装与 OW 图像预处理
Overlay/              置顶翻译 overlay
Translation/          OpenAI-compatible / DeepSeek 请求
Resources/            OW 术语表、主题资源、UI 图标、韩语 jamo 代价表
Tools/ReplayLab/      离线回放与回归断言
Tools/OcrPreprocessLab/ OCR 预处理实验工具
Docs/                 架构、测试指南、重构记录
```

### 维护文档

- [Architecture](Docs/ARCHITECTURE.md)
- [Timeline Refactor Update](Docs/TimelineRefactor-Update-20260613.md)
- [Replay Case Recording Guide](Docs/ReplayCaseRecordingGuide-20260612.md)
- [Publish Layout](Docs/PublishLayout.md)

---

## English

OW Translator Lite is a lightweight Windows OCR translation overlay for Overwatch 2 matches. It is not a general-purpose screen translator. The app focuses on OW chat: it captures a user-selected chat region, recognizes text locally with OneOCR, translates player messages to Simplified Chinese through DeepSeek or any OpenAI-compatible chat completions API, and renders translations in a topmost overlay.

Current version: `0.2.0-beta.4-ui`

### Current Status

- Timeline refactor completed: message identity is the authoritative `ChatTimeline.Seq`, not content equality.
- Legacy de-duplication removed: per-cycle seen state, recent TTL cache, pending fuzzy matching, display-layer duplicate filtering, and anchor/tail truncation are gone.
- Multi-frame consensus added: new messages are translated after two stable observations, or after Korean jamo-near observations; unstable OCR variants wait for confirmation.
- Pixel-diff patrol sampling added: stable frames use cheap screenshot signatures, while changed regions trigger short OCR bursts.
- Korean OCR robustness improved: whitespace-insensitive comparison, Hangul NFD/jamo distance, short Korean message scoring, and a weighted jamo confusion table.
- Overlay ordering improved: translated records keep their original `Seq`, retry explicitly on failures, and are rendered in sequence order.
- Apple Dark Console UI added: dark control console, integrated translation-box settings, collapsed Beta Tools, and HUD-style overlay.
- ReplayLab added: frame-sequence recording, offline replay, missing/duplicate/order assertions, and Korean jamo regression tests.

### Highlights

- **OW-specific OCR flow**: optimized for `[player]: message` chat lines and Chinese system/UI noise filtering.
- **Local OCR**: fixed OneOCR automatic recognition, no cloud OCR dependency.
- **Real-time translation**: DeepSeek and OpenAI-compatible chat completions APIs.
- **OW glossary**: heroes, abilities, maps, modes, common English/Japanese/Korean aliases, and Chinese community terminology.
- **Stable ordering**: Timeline alignment and `Seq`-sorted overlay updates reduce duplicates and out-of-order display when chat history is opened or OCR jitters.
- **Transparent overlay**: topmost translations with always-show mode, click-through, dragging, resizing, and scrollable history.
- **Reply helper**: type Chinese in the overlay input bar, translate to English/Japanese/Korean, and copy to clipboard. The app does not auto-send chat.
- **Privacy-aware storage**: API keys are protected with Windows DPAPI; diagnostics redact secrets.

### Non-Goals

To keep the app fast and maintainable for competitive play, the project does not plan to reintroduce:

- WinOCR, multiple OCR backends, or a complex multi-provider translation UI.
- TTS, Whisper, or voice-chat translation.
- Local Rules as an offline translation path.
- Generic clipboard translation, manga mode, or a universal screen translator workflow.
- Languages beyond English, Japanese, and Korean unless backed by clear user demand and test samples.

### Quick Start

1. Launch `OWTranslatorLite.exe`.
2. Choose `DeepSeek` or `OpenAI Compatible`.
3. For DeepSeek, the default API URL is `https://api.deepseek.com`.
4. Enter an API key, fetch models, and select a model such as `deepseek-v4-flash`.
5. Click the region selector and select the OW chat area. Include complete player names and message text.
6. Click Start.
7. Enable always-show overlay if needed. Disable click-through when you want to drag, resize, or scroll the overlay.

### Sample Test Lines

You can first test with Notepad or an OW custom lobby:

```text
[TEAM] Reverieach: group up
[TEAM] 疯狂的鹿: 힐좀
[TEAM] 天剑若叶: 위도우 조심
[MATCH] kiriMain: suzu no
[MATCH] genji99: nano blade soon
```

Expected behavior:

- Player messages are recognized and translated.
- OCR jitter on the same line does not trigger repeated translations.
- Short Korean messages are not dropped just because they are short.
- Multi-player bursts keep the same order in the overlay.

### Build

Use the local .NET SDK near the repository:

```powershell
& "E:\rstgametranslation\.dotnet\dotnet.exe" build OwTranslateLite.csproj -c Release
```

Publish a tester package:

```powershell
& "E:\rstgametranslation\.dotnet\dotnet.exe" publish OwTranslateLite.csproj -c Release -o E:\rstgametranslation\ow-translate-lite\dist\OWTranslatorLite-v0.2.0-beta.4-ui-portable-win-x64
```

### Regression Tests

```powershell
& "E:\rstgametranslation\.dotnet\dotnet.exe" run --project Tools\ReplayLab\ReplayLab.csproj -c Release -- --timeline-smoke
& "E:\rstgametranslation\.dotnet\dotnet.exe" run --project Tools\ReplayLab\ReplayLab.csproj -c Release -- --similarity Tools\ReplayLab\similarity\korean-jamo-regression.json
& "E:\rstgametranslation\.dotnet\dotnet.exe" run --project Tools\ReplayLab\ReplayLab.csproj -c Release -- Tools\ReplayLab\fixtures\smoke-korean-short Tools\ReplayLab\fixtures\smoke-korean-short\expected.json
& "E:\rstgametranslation\.dotnet\dotnet.exe" build OwTranslateLite.csproj -c Release
```

Expected fixture metrics:

```text
missing=0, duplicates=0, outOfOrder=0, extra=0
```

### Project Layout

```text
Core/                 settings, Timeline, parsing, diagnostics, coordination
Ocr/                  OneOCR wrapper and OW image preprocessing
Overlay/              topmost translation overlay
Translation/          OpenAI-compatible / DeepSeek requests
Resources/            OW glossary, theme resources, UI icons, Korean jamo costs
Tools/ReplayLab/      offline replay and regression assertions
Tools/OcrPreprocessLab/ OCR preprocessing lab
Docs/                 architecture, test guides, refactor notes
```

### Maintainer Notes

- [Architecture](Docs/ARCHITECTURE.md)
- [Timeline Refactor Update](Docs/TimelineRefactor-Update-20260613.md)
- [Replay Case Recording Guide](Docs/ReplayCaseRecordingGuide-20260612.md)
- [Publish Layout](Docs/PublishLayout.md)
