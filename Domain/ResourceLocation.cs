namespace SquishySim.Domain;

/// <summary>Resource in the simulation world that satisfies a drive on arrival.</summary>
public record ResourceLocation(string Name, (float X, float Y) Position, string ActionId);
