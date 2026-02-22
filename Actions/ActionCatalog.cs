namespace SquishySim.Actions;

public static class ActionCatalog
{
    public static readonly List<GameAction> All = new()
    {
        new GameAction
        {
            Id = "eat_food",
            Description = "eat available food",
            Effect = new ActionEffect(HungerDelta: -0.50f, ThirstDelta:  0.00f, FatigueDelta:  0.00f, BladderDelta: +0.15f, MoodDelta: +0.10f)
        },
        new GameAction
        {
            Id = "drink_water",
            Description = "drink a glass of water",
            Effect = new ActionEffect(HungerDelta:  0.00f, ThirstDelta: -0.60f, FatigueDelta:  0.00f, BladderDelta: +0.20f, MoodDelta: +0.05f)
        },
        new GameAction
        {
            Id = "sleep",
            Description = "take a nap or sleep",
            Effect = new ActionEffect(HungerDelta: +0.05f, ThirstDelta: +0.05f, FatigueDelta: -0.80f, BladderDelta:  0.00f, MoodDelta: +0.15f)
        },
        new GameAction
        {
            Id = "use_toilet",
            Description = "use the bathroom",
            Effect = new ActionEffect(HungerDelta:  0.00f, ThirstDelta:  0.00f, FatigueDelta:  0.00f, BladderDelta: -0.90f, MoodDelta: +0.10f)
        },
        new GameAction
        {
            Id = "wander",
            Description = "walk around aimlessly",
            Effect = new ActionEffect(HungerDelta: +0.02f, ThirstDelta: +0.02f, FatigueDelta: +0.04f, BladderDelta:  0.00f, MoodDelta: +0.08f)
        },
        new GameAction
        {
            Id = "do_nothing",
            Description = "sit and stare",
            Effect = new ActionEffect(HungerDelta:  0.00f, ThirstDelta:  0.00f, FatigueDelta: -0.03f, BladderDelta:  0.00f, MoodDelta: -0.04f)
        }
    };

    public static GameAction? FindById(string id) =>
        All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
}
