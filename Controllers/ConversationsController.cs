// PROTOTYPE: Global conversation feed REST API
using Microsoft.AspNetCore.Mvc;
using SquishySim.Services;

namespace SquishySim.Controllers;

[ApiController]
[Route("conversations")]
public class ConversationsController : ControllerBase
{
    private readonly SimulationService _sim;
    public ConversationsController(SimulationService sim) => _sim = sim;

    // GET /conversations
    [HttpGet]
    public IActionResult GetAll() =>
        Ok(_sim.AllConversations.Select(m => new {
            m.Timestamp, m.FromAgentId, m.ToAgentId, m.Text
        }));
}
