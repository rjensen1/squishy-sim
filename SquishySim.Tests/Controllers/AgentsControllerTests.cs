using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SquishySim.Services;

namespace SquishySim.Tests.Controllers;

/// <summary>
/// AC4b — Controller/HTTP-layer assertion that IsSnapped=true appears in the agents
/// DTO when SuppressionBudget=0f. Separate from AC4a (pure RuleBasedDecisionMaker
/// unit test) to verify the DTO derivation layer independently.
/// </summary>
public class AgentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AgentsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // ── AC4b ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC4b_WhenSuppressionBudgetIsZero_IsSnappedIsTrueInDto()
    {
        // Arrange: pause simulation to prevent ticks from modifying budget during test,
        // then set alice's budget to 0f directly through the service.
        var sim = _factory.Services.GetRequiredService<SimulationService>();
        sim.Pause();

        var alice = sim.GetAgent("alice");
        Assert.NotNull(alice);
        alice.Drives.SuppressionBudget = 0f;

        // Act: GET /agents/alice through HTTP layer
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/agents/alice");
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert: isSnapped is true, suppressionBudget is 0 in the DTO
        var drives = json.RootElement.GetProperty("drives");
        Assert.Equal(0f, drives.GetProperty("suppressionBudget").GetSingle());
        Assert.True(json.RootElement.GetProperty("isSnapped").GetBoolean());
    }

    [Fact]
    public async Task AC4b_WhenSuppressionBudgetAboveZero_IsSnappedIsFalseInDto()
    {
        // Contrast: budget > 0f → isSnapped = false
        var sim = _factory.Services.GetRequiredService<SimulationService>();
        sim.Pause();

        var alice = sim.GetAgent("alice");
        Assert.NotNull(alice);
        alice.Drives.SuppressionBudget = 0.50f;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/agents/alice");
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("isSnapped").GetBoolean());
    }
}
