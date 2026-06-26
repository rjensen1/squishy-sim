using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SquishySim.Actions;
using SquishySim.Body;

namespace SquishySim.Mind;

/// <summary>
/// Sends body state to a local Ollama instance and parses the action response.
/// Requires Ollama running at the configured URL with the target model pulled.
/// </summary>
public class OllamaDecisionMaker : IDecisionMaker
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _baseUrl;

    public string DisplayName => $"ollama/{_model}";

    public OllamaDecisionMaker(string model = "phi3", string baseUrl = "http://localhost:11434")
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

        var raw = await GenerateAsync(prompt);
        if (TryParseDecision(raw, actions, targets, out var decision, out var failReason))
            return decision!;

        // Repair loop: one pointed correction — name exactly what was wrong, then re-ask.
        // Small models fix a named mistake; they'll often repeat a vague one.
        var correction = prompt
            + $"\n\nYour previous reply was rejected: {failReason}"
            + " Reply with ONLY the JSON object described above and nothing else.";
        raw = await GenerateAsync(correction);
        if (TryParseDecision(raw, actions, targets, out decision, out failReason))
            return decision!;

        // Floor: never block the sim. Fall back to the deterministic urgency pick.
        return new DecisionResult(PickByUrgency(actions), $"parse failed after retry — {failReason}");
    }

    /// <summary>POSTs a prompt to Ollama and returns the raw model text.</summary>
    private async Task<string> GenerateAsync(string prompt)
    {
        var requestJson = JsonSerializer.Serialize(new
        {
            model = _model,
            prompt = prompt,
            stream = false
        });

        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        var sw = Stopwatch.StartNew();
        var response = await _http.PostAsync($"{_baseUrl}/api/generate", content);
        sw.Stop();
        Console.WriteLine($"[LLM] {_model} inference: {sw.ElapsedMilliseconds}ms");
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseText);
        return doc.RootElement.GetProperty("response").GetString() ?? "";
    }

    /// <summary>
    /// Extracts a <see cref="DecisionResult"/> from raw LLM text. Returns false with a
    /// human-readable <paramref name="failReason"/> when there's no valid JSON action,
    /// an unknown action, or a parameterized action missing/invalid target or content.
    /// Public + static so it can be unit-tested without a running Ollama.
    /// </summary>
    public static bool TryParseDecision(
        string text,
        IReadOnlyList<GameAction> actions,
        IReadOnlyList<string>? targets,
        out DecisionResult? decision,
        out string failReason)
    {
        decision = null;

        // LLMs sometimes wrap JSON in prose/markdown — extract the first {...} block.
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            failReason = "no JSON object found in the reply.";
            return false;
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(text[start..(end + 1)]); }
        catch { failReason = "the JSON did not parse."; return false; }

        using (doc)
        {
            var root = doc.RootElement;
            var actionId = root.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
            var action = ActionCatalog.FindById(actionId);
            if (action == null)
            {
                failReason = $"\"{actionId}\" is not a valid action.";
                return false;
            }

            var reason  = root.TryGetProperty("reason",  out var r) ? r.GetString() ?? "" : "";
            var target  = root.TryGetProperty("target",  out var t) ? t.GetString() : null;
            var content = root.TryGetProperty("content", out var c) ? c.GetString() : null;

            if (action.RequiresTarget)
            {
                if (string.IsNullOrWhiteSpace(target))
                {
                    failReason = $"action \"{action.Id}\" needs a \"target\".";
                    return false;
                }
                // Validate against the legal set (case-insensitive) and canonicalize the casing.
                if (targets is { Count: > 0 })
                {
                    var match = targets.FirstOrDefault(
                        x => string.Equals(x, target, StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                    {
                        failReason = $"\"{target}\" is not someone you can talk to.";
                        return false;
                    }
                    target = match;
                }
            }

            if (action.RequiresContent && string.IsNullOrWhiteSpace(content))
            {
                failReason = $"action \"{action.Id}\" needs \"content\" — the words you say.";
                return false;
            }

            decision = new DecisionResult(action, reason, target, content);
            failReason = "";
            return true;
        }
    }

    private static GameAction PickByUrgency(IReadOnlyList<GameAction> actions) =>
        actions.FirstOrDefault() ?? ActionCatalog.All[0];
}
