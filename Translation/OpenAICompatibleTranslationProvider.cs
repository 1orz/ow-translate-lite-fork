using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OwTranslateLite.Core;

namespace OwTranslateLite.Translation;

public sealed class OpenAICompatibleTranslationProvider : ITranslationProvider
{
    private readonly AppSettings _settings;
    private readonly OwGlossaryService _glossary;
    private readonly HttpClient _client;

    public OpenAICompatibleTranslationProvider(AppSettings settings, OwGlossaryService glossary)
    {
        _settings = settings;
        _glossary = glossary;
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 90))
        };
    }

    public string Name => _settings.TranslationProvider;

    public async Task<string> TranslateAsync(ParsedChatLine line, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiUrl) || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return _glossary.TryLocalTranslate(line.SourceText);
        }

        using HttpRequestMessage request = new(HttpMethod.Post, _settings.ApiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        string glossaryContext = _glossary.BuildPromptContext(line.GlossaryHits);
        object payload = new
        {
            model = _settings.Model,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "你是守望先锋2实时竞技聊天翻译器。把外语玩家发言翻译为简体中文短句，只输出JSON。不要解释，不要翻译玩家ID，不要扩写。英雄、技能、地图和俚语使用中国玩家常用译名。保留语气但优先短、快、可读。"
                },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(new
                    {
                        speaker = line.Speaker,
                        text = line.SourceText,
                        glossary_hits = glossaryContext,
                        output_schema = new { translation = "简体中文短句" }
                    })
                }
            }
        };

        string json = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        string responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        string translated = ExtractTranslation(responseText);
        translated = CleanupModelText(translated);
        return _glossary.ApplyTerms(translated);
    }

    private static string ExtractTranslation(string responseText)
    {
        using JsonDocument document = JsonDocument.Parse(responseText);
        JsonElement root = document.RootElement;
        string content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

        try
        {
            using JsonDocument inner = JsonDocument.Parse(content);
            if (inner.RootElement.TryGetProperty("translation", out JsonElement translation))
            {
                return translation.GetString() ?? content;
            }
        }
        catch
        {
            return content;
        }

        return content;
    }

    private static string CleanupModelText(string text)
    {
        string result = text.Trim();
        result = result.Trim('`');
        result = result.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```", "")
            .Trim();
        return result.Length > 80 ? result[..80] : result;
    }
}
