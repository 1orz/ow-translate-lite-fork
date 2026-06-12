using System.Windows;

namespace OwTranslateLite.Core;

public sealed class ChatTimeline
{
    private readonly List<ChatMessage> _messages = [];
    private readonly int _capacity;
    private long _nextSeq = 1;

    public ChatTimeline(int capacity = 100)
    {
        _capacity = Math.Max(1, capacity);
    }

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public ChatMessage AddDetected(ParsedChatLine line, long frameId, DateTime? timestamp = null)
    {
        DateTime now = timestamp ?? DateTime.Now;
        ChatMessage message = new(
            _nextSeq++,
            line.Speaker,
            line.SourceText,
            line.Bounds,
            now,
            frameId);
        _messages.Add(message);
        TrimToCapacity();
        return message;
    }

    public void Observe(ChatMessage message, ParsedChatLine line, long frameId, DateTime? timestamp = null)
    {
        message.Speaker = line.Speaker;
        message.Bounds = Rect.Union(message.Bounds, line.Bounds);
        message.LastSeenAt = timestamp ?? DateTime.Now;
        message.LastSeenFrameId = frameId;
        message.SeenCount++;
        message.AddVariant(line.SourceText);
    }

    public IReadOnlyList<ChatMessage> TailWindow(int maxCount)
    {
        if (maxCount <= 0)
        {
            return Array.Empty<ChatMessage>();
        }

        return _messages.Count <= maxCount
            ? _messages.ToArray()
            : _messages.TakeLast(maxCount).ToArray();
    }

    public void Clear()
    {
        _messages.Clear();
        _nextSeq = 1;
    }

    public void MarkQueued(ChatMessage message)
    {
        message.State = ChatMessageState.Queued;
    }

    public void MarkTranslating(ChatMessage message)
    {
        message.State = ChatMessageState.Translating;
    }

    public void MarkTranslated(ChatMessage message, string translation)
    {
        message.Translation = translation;
        message.State = ChatMessageState.Translated;
    }

    public void MarkFailed(ChatMessage message)
    {
        message.RetryCount++;
        message.State = ChatMessageState.Failed;
    }

    private void TrimToCapacity()
    {
        if (_messages.Count <= _capacity)
        {
            return;
        }

        _messages.RemoveRange(0, _messages.Count - _capacity);
    }
}

public sealed class ChatMessage
{
    private readonly List<string> _variants;

    public ChatMessage(
        long seq,
        string speaker,
        string sourceText,
        Rect bounds,
        DateTime firstSeenAt,
        long firstSeenFrameId)
    {
        Seq = seq;
        Speaker = speaker;
        ConsensusText = sourceText;
        Bounds = bounds;
        FirstSeenAt = firstSeenAt;
        LastSeenAt = firstSeenAt;
        LastSeenFrameId = firstSeenFrameId;
        _variants = [sourceText];
    }

    public long Seq { get; }
    public string Speaker { get; set; }
    public string ConsensusText { get; private set; }
    public Rect Bounds { get; set; }
    public IReadOnlyList<string> Variants => _variants;
    public int SeenCount { get; set; } = 1;
    public long LastSeenFrameId { get; set; }
    public ChatMessageState State { get; set; } = ChatMessageState.Detected;
    public string? Translation { get; set; }
    public int RetryCount { get; set; }
    public DateTime FirstSeenAt { get; }
    public DateTime LastSeenAt { get; set; }

    public void AddVariant(string text)
    {
        if (_variants.Contains(text, StringComparer.Ordinal))
        {
            return;
        }

        _variants.Add(text);
        ConsensusText = ChooseConsensusText(_variants);
    }

    private static string ChooseConsensusText(IReadOnlyList<string> variants) =>
        variants
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenByDescending(static group => group.Key.Length)
            .First()
            .Key;
}

public enum ChatMessageState
{
    Detected,
    Confirming,
    Queued,
    Translating,
    Translated,
    Failed
}
