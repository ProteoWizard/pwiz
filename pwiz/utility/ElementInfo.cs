namespace Pwiz.Util.Chemistry;

/// <summary>
/// Static lookup over the built-in <see cref="ElementRecord"/> table.
/// Port of pwiz/chemistry::Element::Info free functions.
/// </summary>
public static class ElementInfo
{
    private static readonly Dictionary<ElementType, ElementRecord> s_byType = BuildByType();
    private static readonly Dictionary<string, ElementType> s_bySymbol = BuildBySymbol();

    /// <summary>Returns the record for <paramref name="type"/>. Throws if unknown.</summary>
    public static ElementRecord Record(ElementType type) =>
        s_byType.TryGetValue(type, out var r)
            ? r
            : throw new ArgumentException($"No record for element {type}", nameof(type));

    /// <summary>Returns the record for the given chemical symbol (e.g. "H", "Fe", "D", "_2H"). Throws if unknown.</summary>
    public static ElementRecord Record(string symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        return Record(TypeFromSymbol(symbol));
    }

    /// <summary>Translates a chemical symbol to <see cref="ElementType"/>. Accepts primary symbols and synonyms ("D" → <c>_2H</c>).</summary>
    public static ElementType TypeFromSymbol(string symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        return s_bySymbol.TryGetValue(symbol, out var t)
            ? t
            : throw new ArgumentException($"Unknown chemical symbol '{symbol}'", nameof(symbol));
    }

    /// <summary>True iff <paramref name="symbol"/> is a recognized chemical symbol (or synonym).</summary>
    public static bool TryGetType(string symbol, out ElementType type) =>
        s_bySymbol.TryGetValue(symbol, out type);

    /// <summary>All element records in the built-in table.</summary>
    public static IReadOnlyList<ElementRecord> All => ElementData.Elements;

    private static Dictionary<ElementType, ElementRecord> BuildByType()
    {
        var dict = new Dictionary<ElementType, ElementRecord>(ElementData.Elements.Length);
        foreach (var e in ElementData.Elements)
            dict[e.Type] = e;
        return dict;
    }

    private static Dictionary<string, ElementType> BuildBySymbol()
    {
        var dict = new Dictionary<string, ElementType>(ElementData.Elements.Length * 2, StringComparer.Ordinal);
        foreach (var e in ElementData.Elements)
        {
            dict[e.Symbol] = e.Type;
            if (!string.IsNullOrEmpty(e.Synonym))
                dict[e.Synonym] = e.Type;
        }
        return dict;
    }
}
