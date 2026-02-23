// PROTOTYPE: SquishySim REST API + web UI — not production ready
using SquishySim.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5300");

builder.Services.AddSingleton<SimulationService>();
builder.Services.AddControllers();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

// Kick off the auto-advance timer on startup
var sim = app.Services.GetRequiredService<SimulationService>();
sim.StartAutoAdvance();

app.Run();
