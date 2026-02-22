// PROTOTYPE: Simulation control REST API
using Microsoft.AspNetCore.Mvc;
using SquishySim.Services;

namespace SquishySim.Controllers;

[ApiController]
[Route("sim")]
public class SimController : ControllerBase
{
    private readonly SimulationService _sim;
    public SimController(SimulationService sim) => _sim = sim;

    // POST /sim/step  — advance one tick (works even when paused)
    [HttpPost("step")]
    public IActionResult Step()
    {
        _sim.Step();
        return Ok(new { tick = _sim.TickCount });
    }

    // POST /sim/pause
    [HttpPost("pause")]
    public IActionResult Pause()
    {
        _sim.Pause();
        return Ok(new { paused = true, tick = _sim.TickCount });
    }

    // POST /sim/resume
    [HttpPost("resume")]
    public IActionResult Resume()
    {
        _sim.Resume();
        return Ok(new { paused = false, tick = _sim.TickCount });
    }

    // POST /sim/speed   body: { "multiplier": 2.0 }
    [HttpPost("speed")]
    public IActionResult SetSpeed([FromBody] SetSpeedRequest req)
    {
        if (req.Multiplier < 0.25 || req.Multiplier > 4.0)
            return BadRequest(new { error = "multiplier must be between 0.25 and 4.0" });

        _sim.SetSpeed(req.Multiplier);
        return Ok(new { multiplier = req.Multiplier });
    }

    // GET /sim/status  — convenience for UI polling
    [HttpGet("status")]
    public IActionResult Status() =>
        Ok(new { paused = _sim.IsPaused, tick = _sim.TickCount, speed = _sim.SpeedMultiplier });
}

public record SetSpeedRequest(double Multiplier);
