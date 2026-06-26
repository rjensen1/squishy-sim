using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SquishySim.Actions;
using SquishySim.Body;

namespace SquishySim.Mind;

/// <summary>
/// Sends body state to an LM Studio local server (OpenAI-compatible chat API at
/// {baseUrl}/chat/completions) and parses the action response. Requires LM Studio's
/// "Start Server" toggled on with the target model loaded.
/// <para>
/// Shares <see cref="PromptBuilder"/> and <see cref="OllamaDecisionMaker.TryParseDecision"/>
/// with the Ollama brain — only the HTTP transport differs (chat-completions vs /api/generate).
/// </para>
/// </summary>
public class LmStudioDecisionMaker : IDecisionMaker
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _baseUrl;

    public string DisplayName => $"lmstudio/{_model}";

    public LmStudioDecisionMaker(string model = "phi-4-mini-instruct", string baseUrl = "http://localhost:1234/v1")
    {
        _model = model;
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<DecisionResult> ChooseAsync(
        BodyState state, IReadOnlyList<GameAction> actions,
        string? persona = null, string? navState = null,
        IReadOnlyList<string>? targets = null)
    {
        var prompt = PromptBuilder.Build(state, actions, persona, navState, targets);

        var raw = await ChatAsync(prompt);
        if (OllamaDecisionMaker.TryParseDecision(raw, actions, targets, out var decision, out var failReason))
            return decision!;

        // One pointed-correction retry — name the mistake, then re-ask. Same repair loop
        // the Ollama brain uses; small models fix a named error but repeat a vague one.
        var correction = prompt
            + $"\n\nYour previous reply was rejected: {failReason}"
            + " Reply with ONLY the JSON object described above and nothing else.";
        raw = await ChatAsync(correction);
        if (OllamaDecisionMaker.TryParseDecision(raw, actions, targets, out decision, out failReason))
            return decision!;

        // Floor: never block the sim — fall back to the most-urgent drive.
        return new DecisionResult(actions.FirstOrDefault() ?? ActionCatalog.All[0],
                                  $"parse failed after retry — {failReason}");
    }

    /// <summary>POSTs a prompt as a single user message and returns the model's reply text.</summary>
    private async Task<string> ChatAsync(string prompt)
    {
        var requestJson = JsonSerializer.Serialize(new
        {
            model = _model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.7,
            stream = false
        });

        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        var sw = Stopwatch.StartNew();
        var response = await _http.PostAsync($"{_baseUrl}/chat/completions", content);
        sw.Stop();
        Console.WriteLine($"[LLM] lmstudio/{_model} inference: {sw.ElapsedMilliseconds}ms");
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseText);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}
