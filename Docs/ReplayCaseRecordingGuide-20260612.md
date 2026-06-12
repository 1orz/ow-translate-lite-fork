# Replay Case Recording Guide（2026-06-12）

本文档用于录制 Timeline 重构前的 T0 golden cases。目标是让每个 case 都能被
`Tools/ReplayLab` 离线回放，并人工标注期望消息顺序。

## 通用录制步骤

1. 启动 OW Translator Lite，先“选择聊天区域”，框完整包含 OW 左侧聊天框。
2. 在“Beta 测试”区域选择一个 Case。
3. 点击“录制 Case”。如果当前未运行，程序会自动开始识别。
4. 按下面对应 case 的步骤在游戏聊天里制造样本。
5. 建议每个 case 录制 20-60 秒；完成后再次点击“停止录制”。
6. 程序会打开会话目录：`captured-screenshots/sessions/<时间-case>/`。
7. 在会话目录旁创建或复制 `expected.json`，写入本 case 应翻译的玩家消息顺序。
8. 用 ReplayLab 检查：

```powershell
E:\rstgametranslation\.dotnet\dotnet.exe run --project Tools\ReplayLab\ReplayLab.csproj -c Release -- <session-directory> <expected.json>
```

## 标注格式

```json
{
  "caseId": "case01-korean-short-cold-start",
  "expectedMessages": [
    {
      "speaker": "PLAYER1",
      "sourceText": "가자"
    }
  ],
  "allowedMissingCount": 0,
  "allowedDuplicateCount": 0,
  "allowedOutOfOrderCount": 0,
  "allowedExtraCount": 0
}
```

`speaker` 和 `sourceText` 尽量按 ReplayLab `report.md` 中的 accepted/parsed 文本填写，
不要把玩家名写进 `sourceText`。如果 OCR 把同一句稳定识别成某个错误文本，也按 OCR
稳定文本标注，后续阶段再用共识和相似度修正。

## Case 1：韩语短消息/冷启动

目的：覆盖冷启动后第一条短韩语消息，以及短文本判重死区。

建议样例：

```text
[PLAYER1]: ㄱㄱ
[PLAYER1]: 가자
[PLAYER1]: 힐좀
[PLAYER1]: ㄴㄴ
```

操作：

1. 暂停程序 5 秒以上，清空聊天区可见消息或等聊天淡出。
2. 选择 `Case 1 韩语短消息/冷启动`，点击“录制 Case”。
3. 让测试者连续发送 2-4 条短韩语消息，每条间隔 2-3 秒。
4. 继续录 10 秒，确保同一消息跨多帧出现。

期望：所有玩家消息按发送顺序各出现一次，零漏翻、零重复、零乱序。

## Case 2：多人陆续发言

目的：覆盖多人短时间连续发言，暴露旧 tail-2 截断导致的中间消息漏翻。

建议样例：

```text
[TANK1]: 우리 왼쪽 가자
[SUP1]: 힐 줄게
[DPS1]: 겐지 뒤
[SUP2]: 나노 있어
[DPS2]: 용검 준비
```

操作：

1. 最好找 3 个以上测试账号或队友。
2. 选择 `Case 2 多人陆续发言`，点击“录制 Case”。
3. 在 5-8 秒内让不同玩家连续发送至少 5 条消息。
4. 不要打开聊天历史，录制自然滚动可见区。

期望：所有玩家消息都进入 expected，顺序与游戏内发送顺序一致。

## Case 3：打开聊天历史

目的：覆盖一次出现多条旧消息时的回放行为。

建议样例：

```text
[PLAYER1]: 처음에 왼쪽
[PLAYER2]: 위도우 조심
[PLAYER3]: 궁 있어?
[PLAYER4]: 다음 한타 기다려
[PLAYER5]: 같이 들어가
```

操作：

1. 先让聊天区自然产生 5 条以上玩家消息。
2. 等聊天淡出或只剩少量可见行。
3. 选择 `Case 3 打开聊天历史`，点击“录制 Case”。
4. 打开 OW 聊天窗口，让历史消息一次性出现。
5. 关闭再打开聊天窗口 2-3 次，每次间隔 2 秒。

期望：历史中的玩家消息不漏；已翻译过的消息后续阶段应能直接显示，不重复请求。

## Case 4：OCR 字符抖动

目的：捕获 OneOCR 对同一句相邻帧识别出不同字符的情况。

建议样例：

```text
[PLAYER1]: 트레이서 뒤에 있어
[PLAYER2]: 키리코 스즈 빠짐
[PLAYER3]: 라마트라 궁 조심
```

操作：

1. 选择 `Case 4 OCR 字符抖动`，点击“录制 Case”。
2. 发送包含复杂韩文字形的中等长度句子。
3. 不要立刻发下一句，让同一句在聊天区停留至少 10 秒。
4. 可以轻微移动游戏画面或开关聊天框，增加 OCR 抖动机会。

期望：同一句即使 raw OCR 多帧不同，也只产生一条 expected 消息。

## Case 5：韩语空格抖动

目的：覆盖韩语空格不稳定导致的同句多变体。

建议样例：

```text
[PLAYER1]: 우리 같이 들어가자
[PLAYER1]: 라인 방벽 없어
[PLAYER1]: 아나 힐 좀 줘
```

操作：

1. 选择 `Case 5 韩语空格抖动`，点击“录制 Case”。
2. 发送 2-3 条带空格的韩语句子。
3. 每条消息停留 8-10 秒再发送下一条。
4. 录完后检查 frame JSON 中 raw/processed 是否出现空格位置差异。

期望：空格变体不应产生重复翻译条目。

## Case 6：系统提示与玩家消息交错

目的：覆盖中文系统提示和韩语玩家消息交错，验证 parser 后续 D12 修改。

建议样例：

```text
[PLAYER1]: 힐 필요해
[PLAYER2]: 리퍼 뒤
[PLAYER3]: 궁극기 있어
```

操作：

1. 选择 `Case 6 系统提示交错`，点击“录制 Case”。
2. 在游戏中触发几条中文系统提示，例如加入/离开语音、队伍提示、比赛阶段提示。
3. 在系统提示之间穿插 3 条以上韩语玩家消息。
4. 录制 30 秒左右。

期望：expected 只写玩家消息；系统提示不写入 expected。若当前旧 parser 误翻系统提示，
记录为 baseline，不在 T0 阶段修。

## Case 7：完全淡化后再次发言

目的：覆盖空帧后 1-2 条可见行必须按新消息处理，防止被吸收到旧记录。

建议样例：

```text
[PLAYER1]: ㄱㄱ
等待淡化
[PLAYER1]: ㄱㄱ
[PLAYER1]: 가자
```

操作：

1. 选择 `Case 7 淡化后同文本再发`，点击“录制 Case”。
2. 发送一条短消息，例如 `ㄱㄱ` 或 `가자`。
3. 等聊天完全淡化，录到至少 2-3 帧无聊天。
4. 再发送同样或非常相似的短消息。
5. 继续录 10 秒。

期望：淡化前后的两条相同短消息都应作为独立玩家消息进入 expected。

## 建议命名

真实样本确认可用后，可把会话目录复制到：

```text
Tools/ReplayLab/fixtures/<case-id>/
```

每个 fixture 至少包含：

```text
session.json
frames/frame_*.json
expected.json
```

PNG 可以保留用于人工复核；如果仓库体积过大，提交前先和维护者确认。
