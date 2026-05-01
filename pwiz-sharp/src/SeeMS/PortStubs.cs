// Stubs for pwiz cpp/CLI types referenced by the SeeMS port that don't have a clean
// pwiz-sharp equivalent yet. The form/panel classes that the cpp Manager.cs references
// (SpectrumProcessingForm, SpectrumAnnotationForm, DataPointTableForm, TreeViewForm) are
// excluded from the build via SeeMS.csproj; their type identities live here so Manager.cs
// compiles. Re-port the real implementations when needed.

#pragma warning disable CS1591  // public stubs without docs

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.SeeMS;

// Form-class type identities for the dialogs we excluded from this build pass. Each one
// is a `DockableForm` subclass so calls like `.Show(dockPanel, DockState.X)` type-check.
public sealed class SpectrumProcessingForm : ManagedDockableForm
{
    public sealed class ProcessingChangedEventArgs : EventArgs
    {
        public enum Scope { Spectrum, Run, Global }
        public Scope ChangeScope { get; set; }
        public MassSpectrum Spectrum { get; set; }
    }
    public event EventHandler<ProcessingChangedEventArgs> ProcessingChanged;
    public IList<MassSpectrum> SpectrumList { get; } = new List<MassSpectrum>();
    public IList<IProcessing> SpectrumProcessing { get; } = new List<IProcessing>();
    public IList<IProcessing> GlobalProcessing { get; } = new List<IProcessing>();
    public MassSpectrum CurrentSpectrum { get; set; }
    public Pwiz.Data.MsData.Spectra.ISpectrumList GetProcessingSpectrumList(MassSpectrum spectrum,
        Pwiz.Data.MsData.Spectra.ISpectrumList inner)
    {
        _ = spectrum; return inner;
    }
    /// <summary>
    /// Stub: the cpp version walks the processing list and fires <c>ProcessingChanged</c>
    /// with the affected spectrum. We have no processing list, so this is a no-op — firing
    /// the event would pass a null Spectrum to Manager and NRE on dereference.
    /// </summary>
    public void UpdateProcessing(MassSpectrum spectrum) { _ = spectrum; }
}
public sealed class SpectrumAnnotationForm : ManagedDockableForm
{
    public sealed class AnnotationChangedEventArgs : EventArgs
    {
        public MassSpectrum Spectrum { get; set; }
    }
    public event EventHandler<AnnotationChangedEventArgs> AnnotationChanged;
    public IList<MassSpectrum> SpectrumList { get; } = new List<MassSpectrum>();
    public IList<IAnnotation> Annotations { get; } = new List<IAnnotation>();
    public MassSpectrum CurrentSpectrum { get; set; }
    /// <summary>Stub no-op (see <see cref="SpectrumProcessingForm.UpdateProcessing"/>).</summary>
    public void UpdateAnnotations(MassSpectrum spectrum) { _ = spectrum; }
}

public sealed class DataPointTableForm : ManagedDockableForm
{
    public List<GraphItem> DataItems { get; } = new();
    public DataPointTableForm() { }
    public DataPointTableForm(GraphItem item) { _ = item; }
}

// HeatmapForm + TimeMzHeatmapForm + SelectColumnsDialog stubs (real ports excluded for
// now — the cpp versions depend on .NET-Framework-era types we haven't ported).
public sealed class HeatmapForm : ManagedDockableForm
{
    public HeatmapForm() { }
    public HeatmapForm(ManagedDataSource source) { _ = source; }
    public HeatmapForm(ManagedDataSource source, int targetMsLevel) { _ = source; _ = targetMsLevel; }
    public HeatmapForm(Manager manager, ManagedDataSource source) { _ = manager; _ = source; }
}

// AboutForm stub — original was a logo + version dialog tied to Properties.Resources.
public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About SeeMS-sharp";
        Size = new System.Drawing.Size(360, 200);
        StartPosition = FormStartPosition.CenterParent;
        var label = new System.Windows.Forms.Label
        {
            Text = "SeeMS-sharp\n\nPort of pwiz_tools/SeeMS to pwiz-sharp.",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
        };
        Controls.Add(label);
    }
}
public sealed class TimeMzHeatmapForm : ManagedDockableForm
{
    public TimeMzHeatmapForm() { }
    public TimeMzHeatmapForm(ManagedDataSource source) { _ = source; }
    public TimeMzHeatmapForm(Manager manager, ManagedDataSource source) { _ = manager; _ = source; }
}
public sealed class SelectColumnsDialog : Form
{
    public SelectColumnsDialog() { }
    public SelectColumnsDialog(System.Windows.Forms.DataGridView grid) { _ = grid; }
}

// OpenDataSourceDialog stub: vanilla OpenFileDialog wrapper. The cpp version is a tree-view
// file browser with vendor-format previews — replaced for now with the standard file picker.
public sealed class OpenDataSourceDialog : Form
{
    /// <summary>Path with optional <c>:run-index</c> suffix.</summary>
    public sealed class MSDataRunPath
    {
        public string Filepath { get; }
        public int RunIndex { get; }
        public MSDataRunPath(string spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            int colon = spec.LastIndexOf(':');
            if (colon > 1 && int.TryParse(spec[(colon + 1)..], out int idx))
            { Filepath = spec[..colon]; RunIndex = idx; }
            else { Filepath = spec; RunIndex = 0; }
        }
        public MSDataRunPath(string filepath, int runIndex) { Filepath = filepath; RunIndex = runIndex; }
        public override string ToString() => $"{Filepath}:{RunIndex}";
    }

    public IList<string> DataSources { get; } = new List<string>();
    public string InitialDirectory { get; set; } = string.Empty;
    public string CurrentDirectory => InitialDirectory;

    public new DialogResult ShowDialog(IWin32Window owner = null)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Open mass-spec data file",
            Filter = "All supported|*.mzML;*.mgf;*.raw;*.d;*.wiff;*.wiff2|All files|*.*",
            CheckFileExists = true,
            Multiselect = true,
            InitialDirectory = string.IsNullOrEmpty(InitialDirectory) ? Environment.CurrentDirectory : InitialDirectory,
        };
        var result = owner is null ? ofd.ShowDialog() : ofd.ShowDialog(owner);
        if (result == DialogResult.OK)
        {
            DataSources.Clear();
            foreach (var f in ofd.FileNames) DataSources.Add(f);
        }
        return result;
    }
}

// pwiz cpp/CLI analysis types referenced by Processing.cs / ProcessingListView.cs.
// Real ports would map to Pwiz.Analysis equivalents (SpectrumList_Smoother /
// ThresholdFilter / PeakDetector). Stubbed here as empty classes so the SeeMS port
// compiles; behavior is restored when those algorithms are actually wired up.
public sealed class Smoother
{
    public enum Type { SavitzkyGolay, Whittaker }
    public Smoother() { }
    public Smoother(Type type, int windowSize, int polynomialOrder, double lambda) { }
}

public sealed class PeakDetector
{
    public enum Type { LocalMaximum, CWT }
    public PeakDetector() { }
}

// SeeMS uses pwiz cpp's Analysis::ThresholdFilter (different shape from
// Pwiz.Analysis.PeakFilters.ThresholdFilter). Stub for now.
// The cpp/CLI binding exposes the enum members as `ThresholdingBy_Count` etc., so we
// alias the C# enum-member names so call sites compile unchanged.
public sealed class ThresholdFilter
{
    public enum ThresholdingBy_Type { Count, AbsoluteIntensity, FractionOfBasePeakIntensity, FractionOfTotalIntensity, FractionOfTotalIntensityCutoff }
    public enum ThresholdingOrientation { MostIntense, LeastIntense }
    public enum ThresholdingOrientation_Type { MostIntense, LeastIntense }
    public const ThresholdingBy_Type ThresholdingBy_Count = ThresholdingBy_Type.Count;
    public const ThresholdingBy_Type ThresholdingBy_AbsoluteIntensity = ThresholdingBy_Type.AbsoluteIntensity;
    public const ThresholdingBy_Type ThresholdingBy_FractionOfBasePeakIntensity = ThresholdingBy_Type.FractionOfBasePeakIntensity;
    public const ThresholdingBy_Type ThresholdingBy_FractionOfTotalIntensity = ThresholdingBy_Type.FractionOfTotalIntensity;
    public const ThresholdingBy_Type ThresholdingBy_FractionOfTotalIntensityCutoff = ThresholdingBy_Type.FractionOfTotalIntensityCutoff;
    public const ThresholdingOrientation_Type Orientation_MostIntense = ThresholdingOrientation_Type.MostIntense;
    public const ThresholdingOrientation_Type Orientation_LeastIntense = ThresholdingOrientation_Type.LeastIntense;
    public ThresholdFilter() { }
    public ThresholdFilter(ThresholdingBy_Type by, double threshold) { }
    public ThresholdFilter(ThresholdingBy_Type by, double threshold, ThresholdingOrientation_Type orientation) { }
}

// Generic LRU cache referenced by HeatmapForm / TimeMzHeatmapForm. The cpp version uses
// pwiz.Common.Collections.MemoryCache; stubbed as a thin Dictionary<TKey, TValue> wrapper
// for now (no eviction).
public sealed class MemoryCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _backing = new();
    public TValue this[TKey key]
    {
        get => _backing.TryGetValue(key, out var v) ? v : default!;
        set => _backing[key] = value;
    }
    public bool ContainsKey(TKey key) => _backing.ContainsKey(key);
    public void Add(TKey key, TValue value) => _backing[key] = value;
    public void Clear() => _backing.Clear();
    public int Count => _backing.Count;
}

#pragma warning restore CS1591
