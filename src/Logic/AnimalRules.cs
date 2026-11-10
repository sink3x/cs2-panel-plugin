namespace CS2Panel;

/// <summary>Limits for <c>css_spawnanimal</c>. CS2 only has a chicken; "pig" and "cow" are a recoloured, scaled chicken.</summary>
public static class AnimalRules
{
    public const int MaxAnimals = 60;
    public const int MaxPerCall = 30;

    public static bool IsKnown(string kind) => kind.Equals("chicken", StringComparison.OrdinalIgnoreCase)
        || kind.Equals("pig", StringComparison.OrdinalIgnoreCase)
        || kind.Equals("cow", StringComparison.OrdinalIgnoreCase);

    /// <summary>The requested count clamped to 1..30.</summary>
    public static int ClampCount(int requested) => Math.Clamp(requested, 1, MaxPerCall);

    /// <summary>Whether <paramref name="count"/> more animals fit on a map that already has <paramref name="existing"/>.</summary>
    public static bool Fits(int existing, int count) => existing + count <= MaxAnimals;
}
