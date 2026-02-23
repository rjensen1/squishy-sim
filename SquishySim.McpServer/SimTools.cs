// PROTOTYPE: SquishySim MCP tools — thin proxy to SquishySim.Api REST endpoints
using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;

namespace SquishySim.McpServer;

[McpServerToolType]
public class SimTools(HttpClient http)
{
    // ── Agents ────────────────────────────────────────────────────────────────

    [McpServerTool, Description("List all agents and their current drive state.")]
    public async Task<string> ListAgents()
    {
        var res = await http.GetStringAsync("/agents");
        return res;
    }

    [McpServerTool, Description("Get full state for a specific agent, including drives, current action, and LLM config.")]
    public async Task<string> GetAgentState(
        [Description("The agent ID (e.g. 'alice', 'bob', 'charlie')")] string agentId)
    {
        var res = await http.GetAsync($"/agents/{agentId}");
        return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : $"Agent '{agentId}' not found.";
    }

    [McpServerTool, Description("Set a drive value for an agent. Drive must be one of: hunger, thirst, fatigue, bladder, mood. Value must be 0.0–1.0. Takes effect immediately.")]
    public async Task<string> SetDrive(
        [Description("The agent ID")] string agentId,
        [Description("Drive name: hunger | thirst | fatigue | bladder | mood")] string drive,
        [Description("Drive value between 0.0 (none) and 1.0 (critical/max)")] double value)
    {
        var res = await http.PostAsJsonAsync($"/agents/{agentId}/drives/{drive}", new { value });
        return await res.Content.ReadAsStringAsync();
    }

    [McpServerTool, Description("Get the thought log for an agent (recent decisions and reasons). Defaults to last 20 entries.")]
    public async Task<string> GetThoughts(
        [Description("The agent ID")] string agentId,
        [Description("Maximum number of recent thoughts to return (1–200, default 20)")] int limit = 20)
    {
        var res = await http.GetStringAsync($"/agents/{agentId}/thoughts?limit={limit}");
        return res;
    }

    [McpServerTool, Description("Get the inter-agent conversation messages involving a specific agent.")]
    public async Task<string> GetAgentMessages(
        [Description("The agent ID")] string agentId)
    {
        var res = await http.GetStringAsync($"/agents/{agentId}/messages");
        return res;
    }

    [McpServerTool, Description("Get the global conversation feed — all inter-agent messages across all agents.")]
    public async Task<string> GetConversations()
    {
        var res = await http.GetStringAsync("/conversations");
        return res;
    }

    [McpServerTool, Description("Set the LLM config for a specific agent (model, base URL, optional API key). API key is write-only and never returned.")]
    public async Task<string> SetLlmConfig(
        [Description("The agent ID")] string agentId,
        [Description("Model name (e.g. 'phi3', 'llama3', 'rule-based')")] string model,
        [Description("Base URL of the LLM provider (e.g. 'http://localhost:11434')")] string baseUrl,
        [Description("Optional API key — write-only, never returned")] string? apiKey = null)
    {
        var res = await http.PutAsJsonAsync($"/agents/{agentId}/llm", new { model, baseUrl, apiKey });
        return await res.Content.ReadAsStringAsync();
    }

    // ── Simulation control ────────────────────────────────────────────────────

    [McpServerTool, Description("Advance the simulation by one tick. Works even when paused.")]
    public async Task<string> StepSimulation()
    {
        var res = await http.PostAsync("/sim/step", null);
        return await res.Content.ReadAsStringAsync();
    }

    [McpServerTool, Description("Pause the simulation auto-advance timer.")]
    public async Task<string> PauseSimulation()
    {
        var res = await http.PostAsync("/sim/pause", null);
        return await res.Content.ReadAsStringAsync();
    }

    [McpServerTool, Description("Resume the simulation auto-advance timer.")]
    public async Task<string> ResumeSimulation()
    {
        var res = await http.PostAsync("/sim/resume", null);
        return await res.Content.ReadAsStringAsync();
    }

    [McpServerTool, Description("Set the simulation tick rate. Multiplier must be between 0.25 (slow) and 4.0 (fast). 1.0 = 1 tick per 2 seconds.")]
    public async Task<string> SetSpeed(
        [Description("Speed multiplier: 0.25–4.0 (1.0 = normal)")] double multiplier)
    {
        var res = await http.PostAsJsonAsync("/sim/speed", new { multiplier });
        return await res.Content.ReadAsStringAsync();
    }
}
