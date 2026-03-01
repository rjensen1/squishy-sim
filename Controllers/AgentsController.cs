// PROTOTYPE: Agents REST API
using Microsoft.AspNetCore.Mvc;
using SquishySim.Body;
using SquishySim.Services;

namespace SquishySim.Controllers;

[ApiController]
[Route("agents")]
public class AgentsController : ControllerBase
{
    private readonly SimulationService _sim;
    public AgentsController(SimulationService sim) => _sim = sim;

    // GET /agents
    [HttpGet]
    public IActionResult GetAll() =>
        Ok(_sim.Agents.Select(AgentDto.From));

    // GET /agents/{id}
    [HttpGet("{id}")]
    public IActionResult GetOne(string id)
    {
        var agent = _sim.GetAgent(id);
        return agent == null ? NotFound() : Ok(AgentDto.From(agent));
    }

    // POST /agents/{id}/drives/{drive}   body: { "value": 0.75 }
    [HttpPost("{id}/drives/{drive}")]
    public IActionResult SetDrive(string id, string drive, [FromBody] SetDriveRequest req)
    {
        var valid = new[] { "hunger", "thirst", "fatigue", "bladder", "social", "mood" };
        if (!valid.Contains(drive.ToLowerInvariant()))
            return BadRequest(new { error = $"Unknown drive '{drive}'. Valid: {string.Join(", ", valid)}" });

        if (req.Value < 0.0 || req.Value > 1.0)
            return BadRequest(new { error = "Value must be between 0.0 and 1.0" });

        return _sim.SetDrive(id, drive, req.Value)
            ? Ok(new { agent = id, drive, value = req.Value, note = "takes effect immediately" })
            : NotFound();
    }

    // GET /agents/{id}/thoughts?limit=20
    [HttpGet("{id}/thoughts")]
    public IActionResult GetThoughts(string id, [FromQuery] int limit = 20)
    {
        var agent = _sim.GetAgent(id);
        if (agent == null) return NotFound();
        var thoughts = agent.Thoughts
            .TakeLast(Math.Clamp(limit, 1, 200))
            .Select(t => new { t.Timestamp, t.Text });
        return Ok(thoughts);
    }

    // GET /agents/{id}/messages
    [HttpGet("{id}/messages")]
    public IActionResult GetMessages(string id)
    {
        var agent = _sim.GetAgent(id);
        if (agent == null) return NotFound();
        return Ok(agent.Messages.Select(m => new {
            m.Timestamp, m.FromAgentId, m.ToAgentId, m.Text
        }));
    }

    // PUT /agents/{id}/llm
    [HttpPut("{id}/llm")]
    public IActionResult SetLlm(string id, [FromBody] SetLlmRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Model))
            return BadRequest(new { error = "model is required" });
        if (string.IsNullOrWhiteSpace(req.BaseUrl))
            return BadRequest(new { error = "base_url is required" });

        return _sim.SetLlmConfig(id, req.Model, req.BaseUrl, req.ApiKey)
            ? Ok(new { agent = id, model = req.Model, base_url = req.BaseUrl,
                       api_key = req.ApiKey != null ? "***" : null })
            : NotFound();
    }
}

public record SetDriveRequest(double Value);
public record SetLlmRequest(string Model, string BaseUrl, string? ApiKey);

// DTO — api_key is never included
// SECURITY-NOTE: SuppressionBudget and IsSnapped are read-only in this DTO.
// If a writable endpoint is added for dev tooling, setting budget=0 allows forcing
// any agent to snap on demand — obvious manipulation surface if ever network-facing.
public record AgentDto(
    string Id, string Name,
    DrivesDto Drives,
    string CurrentAction, string CurrentReason,
    LlmConfigDto LlmConfig,
    bool ContextModifierActive,
    PositionDto Position,
    PositionDto? Destination,
    string NavState,
    bool IsSnapped,
    float PersonaDriftFactor,
    float BehavioralCoherence)
{
    public static AgentDto From(SquishySim.Domain.Agent a) => new(
        a.Id, a.Name,
        new DrivesDto(a.Drives.Hunger, a.Drives.Thirst, a.Drives.Fatigue, a.Drives.Bladder, a.Drives.Social, a.Drives.Mood, a.Drives.SuppressionBudget),
        a.CurrentAction, a.CurrentReason,
        new LlmConfigDto(a.LlmConfig.Model, a.LlmConfig.BaseUrl, a.LlmConfig.HasApiKey ? "***" : null),
        a.Drives.Social < 0.65f,
        new PositionDto(a.Position.X, a.Position.Y),
        a.Destination.HasValue ? new PositionDto(a.Destination.Value.X, a.Destination.Value.Y) : null,
        a.NavState.ToString(),
        a.Drives.SuppressionBudget <= 0f,
        a.PersonaDriftFactor,
        CoherenceDegradationSystem.BehavioralCoherence(a.Drives.Social)
    );
}

public record DrivesDto(float Hunger, float Thirst, float Fatigue, float Bladder, float Social, float Mood, float SuppressionBudget);
public record LlmConfigDto(string Model, string BaseUrl, string? ApiKey);
public record PositionDto(float X, float Y);
