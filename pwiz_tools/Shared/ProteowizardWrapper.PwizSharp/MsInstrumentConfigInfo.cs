// MsInstrumentConfigInfo — supporting type, no pwiz dep. Moved verbatim from
// the legacy ProteowizardWrapper to the net8 sandbox so MsDataFileImpl can
// return it without dragging in the .NET Framework wrapper assembly.

using System;

namespace pwiz.ProteowizardWrapper;

/// <summary>
/// Lightweight projection of an instrument configuration — what Skyline's UI
/// + tests assert on. Equality is value-equal across all four strings so test
/// suites can compare expected vs. actual without object-identity surprises.
/// </summary>
public sealed class MsInstrumentConfigInfo : IEquatable<MsInstrumentConfigInfo>
{
    public string Model { get; }
    public string Ionization { get; }
    public string Analyzer { get; }
    public string Detector { get; }

    public static readonly MsInstrumentConfigInfo EMPTY = new(string.Empty, string.Empty, string.Empty, string.Empty);

    public MsInstrumentConfigInfo(string model, string ionization, string analyzer, string detector)
    {
        Model = model ?? string.Empty;
        Ionization = ionization ?? string.Empty;
        Analyzer = analyzer ?? string.Empty;
        Detector = detector ?? string.Empty;
    }

    public bool IsEmpty =>
        string.IsNullOrEmpty(Model)
        && string.IsNullOrEmpty(Ionization)
        && string.IsNullOrEmpty(Analyzer)
        && string.IsNullOrEmpty(Detector);

    public override bool Equals(object? obj) => Equals(obj as MsInstrumentConfigInfo);
    public bool Equals(MsInstrumentConfigInfo? other) =>
        other is not null
        && Model == other.Model
        && Ionization == other.Ionization
        && Analyzer == other.Analyzer
        && Detector == other.Detector;

    public override int GetHashCode() => HashCode.Combine(Model, Ionization, Analyzer, Detector);
    public override string ToString() => $"{Model}|{Ionization}|{Analyzer}|{Detector}";
}
