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

    public async Task<(GameAction action, string reason)> ChooseAsync(
        BodyState state, IReadOnlyList<GameAction> actions)
    {
        var prompt = PromptBuilder.Build(state, actions);

        var requestJson = JsonSerializer.Serialize(new
        {
            model = _model,
            prompt = prompt,
            stream = false
        });

        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{_baseUrl}/api/generate", content);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(responseText);
        var llmOutput = doc.RootElement.GetProperty("response").GetString() ?? "";

        return ParseActionFromLlmOutput(llmOutput, actions);
    }

    private static (GameAction action, string reason) ParseActionFromLlmOutput(
        string text, IReadOnlyList<GameAction> actions)
    {
        try
        {
            // LLMs sometimes wrap JSON in markdown — extract the first {...} block
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');

            if (start >= 0 && end > start)
            {
                var json = text[start..(end + 1)];
                var doc = JsonDocument.Parse(json);

                var actionId = doc.RootElement.GetProperty("action").GetString() ?? "";
                var reason = doc.RootElement.TryGetProperty("reason", out var r)
                    ? r.GetString() ?? ""
                    : "";

                var action = ActionCatalog.FindById(actionId);
                if (action != null)
                    return (action, reason);
            }
        }
        catch
        {
            // Fall through to urgency-based fallback
        }

        // Fallback: most urgent drive wins
        return (PickByUrgency(actions), $"parse failed — raw: {text[..Math.Min(80, text.Length)]}");
    }

    private static GameAction PickByUrgency(IReadOnlyList<GameAction> actions) =>
        actions.FirstOrDefault() ?? ActionCatalog.All[0];
}
