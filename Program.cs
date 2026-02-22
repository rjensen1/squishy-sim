using SquishySim.Actions;
using SquishySim.Body;
using SquishySim.Mind;

Console.OutputEncoding = System.Text.Encoding.UTF8;
try { Console.CursorVisible = false; } catch { /* non-interactive environment */ }

// ─── Parse args ─────────────────────────────────────────────────────────────
var useOllama = args.Contains("--ollama") || args.Contains("-o");
var model     = GetArg(args, "--model") ?? "phi3";
var ollamaUrl = GetArg(args, "--url")   ?? "http://localhost:11434";
var tickMs    = int.TryParse(GetArg(args, "--tick"), out var t) ? t : 2000;

// ─── Decision maker ──────────────────────────────────────────────────────────
IDecisionMaker mind = useOllama
    ? new OllamaDecisionMaker(model, ollamaUrl)
    : new RuleBasedDecisionMaker();

// ─── Shared simulation state ─────────────────────────────────────────────────
var body       = new BodyState();
var tick       = 0;
var lastAction = (GameAction?)null;
var lastReason = "";
var errorMsg   = (string?)null;
var paused     = false;

// ─── Sim loop (background) ───────────────────────────────────────────────────
var cts = new CancellationTokenSource();

var simTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        if (!paused)
        {
            tick++;
            DriveSystem.Tick(body);

            try
            {
                var (action, reason) = await mind.ChooseAsync(body, ActionCatalog.All);

                body.Hunger  += action.Effect.HungerDelta;
                body.Thirst  += action.Effect.ThirstDelta;
                body.Fatigue += action.Effect.FatigueDelta;
                body.Bladder += action.Effect.BladderDelta;
                body.Mood    += action.Effect.MoodDelta;
                body.Clamp();

                lastAction = action;
                lastReason = reason;
                errorMsg   = null;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
            }
        }

        try { await Task.Delay(tickMs, cts.Token); }
        catch (OperationCanceledException) { break; }
    }
}, cts.Token);

// ─── Display loop (foreground) ───────────────────────────────────────────────
try { Console.Clear(); } catch { }
PrintHeader(mind.DisplayName, tickMs, useOllama);

while (true)
{
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        if (key == ConsoleKey.Q || key == ConsoleKey.Escape) break;
        if (key == ConsoleKey.P) paused = !paused;
    }

    Render(body, tick, lastAction, lastReason, errorMsg, paused, mind.DisplayName);
    await Task.Delay(150);
}

cts.Cancel();
try { await simTask; } catch { /* clean shutdown */ }

try { Console.CursorVisible = true; } catch { }
try { Console.Clear(); } catch { }
Console.WriteLine($"Simulation ended at tick {tick}.");
Console.WriteLine($"Final state: hunger={body.Hunger:0.00} thirst={body.Thirst:0.00} fatigue={body.Fatigue:0.00} mood={body.Mood:0.00}");

// ─── Helpers ─────────────────────────────────────────────────────────────────

static void PrintHeader(string modelName, int tickMs, bool usingOllama)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("╔══════════════════════════════════════════════════════╗");
    Console.WriteLine("║              S Q U I S H Y   S I M                  ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine($"  Model: {modelName}   Tick: {tickMs}ms   [P] pause  [Q] quit");
    Console.WriteLine();
}

static void Render(
    BodyState b, int tick, GameAction? lastAction, string lastReason,
    string? error, bool paused, string modelName)
{
    // Position after the header (5 lines)
    try { Console.SetCursorPosition(0, 5); } catch { }

    var pauseTag = paused ? " [PAUSED]" : "         ";
    WriteL($"  Tick {tick,-6}{pauseTag}                              ");
    WriteL("");

    WriteL($"  HUNGER   {Bar(b.Hunger)}  {b.Hunger:0.00}  {b.HungerLabel,-10}");
    WriteL($"  THIRST   {Bar(b.Thirst)}  {b.Thirst:0.00}  {b.ThirstLabel,-10}");
    WriteL($"  FATIGUE  {Bar(b.Fatigue)}  {b.Fatigue:0.00}  {b.FatigueLabel,-10}");
    WriteL($"  BLADDER  {Bar(b.Bladder)}  {b.Bladder:0.00}  {b.BladderLabel,-10}");
    WriteL($"  MOOD     {MoodBar(b.Mood)}  {b.Mood:0.00}  {b.MoodLabel,-10}");

    WriteL("");

    if (lastAction != null)
    {
        WriteL($"  Action : {lastAction.Id,-15}                           ");
        WriteL($"  Reason : {Clip(lastReason, 48),-50}");
        WriteL($"  Effect : {Clip(lastAction.EffectSummary, 48),-50}");
    }
    else
    {
        WriteL("  Action : (waiting for first tick...)                  ");
        WriteL("                                                        ");
        WriteL("                                                        ");
    }

    WriteL("");

    if (error != null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        WriteL($"  ERROR  : {Clip(error, 48),-50}");
        Console.ResetColor();
    }
    else
    {
        WriteL("  Status : OK                                           ");
    }
}

static void WriteL(string s) => Console.WriteLine(s);

static string Bar(float value)
{
    const int w = 12;
    int filled = Math.Clamp((int)(value * w), 0, w);
    var bar = new string('█', filled) + new string('░', w - filled);
    var alert = value > 0.85f ? "!!" : value > 0.65f ? "! " : "  ";
    return $"[{bar}]{alert}";
}

static string MoodBar(float value)
{
    const int w = 12;
    int filled = Math.Clamp((int)(value * w), 0, w);
    var bar = new string('▓', filled) + new string('░', w - filled);
    return $"[{bar}]  ";
}

static string Clip(string s, int max) =>
    s.Length <= max ? s : s[..max] + "…";

static string? GetArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
