// Stub for the WinForms-Designer-generated Properties/Settings class. The cpp SeeMS
// stored UI preferences (last-browsed dir, window position, tolerance defaults, etc.) in
// .NET Framework's Settings provider; that infrastructure exists on .NET 8 too but its
// designer file uses code-gen we haven't ported. For now we surface the same property
// names backed by an in-memory dictionary so callers compile and round-trip values
// during a single session.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Pwiz.SeeMS;

/// <summary>
/// Drop-in for the cpp SeeMS Pwiz.SeeMS.Settings: the SeeMS UI calls
/// <c>Pwiz.SeeMS.Settings.Default.X</c> for every preference. Backed by an in-memory
/// dictionary; persisted across runs is a follow-up.
/// </summary>
public sealed class Settings
{
    /// <summary>Singleton instance — mirrors the cpp <c>Pwiz.SeeMS.Settings.Default</c>.</summary>
    public static Settings Default { get; } = new Settings();

    private Settings()
    {
        // Reasonable defaults that match the cpp Settings.Designer values.
        TimeInMinutes = true;
        DefaultDecimalPlaces = 4;
        LastBrowseToFileLocation = string.Empty;
        MainFormLocation = new Point(100, 100);
        MainFormSize = new Size(1200, 800);
        MainFormWindowState = FormWindowState.Normal;
        SimAsSpectra = false;
        SrmAsSpectra = false;
        CombineIonMobilitySpectra = false;
        IgnoreZeroIntensityPoints = false;
        AcceptZeroLengthSpectra = false;
        ShowChromatogramIntensityLabels = true;
        ShowChromatogramMatchedAnnotations = true;
        ShowChromatogramTimeLabels = false;
        ShowChromatogramUnmatchedAnnotations = false;
        ShowScanIntensityLabels = false;
        ShowScanMatchedAnnotations = true;
        ShowScanMzLabels = false;
        ShowScanUnmatchedAnnotations = false;
        ScanMatchToleranceOverride = false;
        ChromatogramMatchToleranceOverride = false;
        MzMatchTolerance = 0.5;
        MzMatchToleranceUnit = 0;       // 0 = Da, 1 = ppm; matches cpp combobox order
        TimeMatchTolerance = 5;
        TimeMatchToleranceUnit = 0;     // 0 = sec, 1 = min; matches cpp combobox order
    }

    public bool TimeInMinutes { get; set; }
    public int DefaultDecimalPlaces { get; set; }
    public string LastBrowseToFileLocation { get; set; }
    public Point MainFormLocation { get; set; }
    public Size MainFormSize { get; set; }
    public FormWindowState MainFormWindowState { get; set; }
    public bool SimAsSpectra { get; set; }
    public bool SrmAsSpectra { get; set; }
    public bool CombineIonMobilitySpectra { get; set; }
    public bool IgnoreZeroIntensityPoints { get; set; }
    public bool AcceptZeroLengthSpectra { get; set; }
    public bool ShowChromatogramIntensityLabels { get; set; }
    public bool ShowChromatogramMatchedAnnotations { get; set; }
    public bool ShowChromatogramTimeLabels { get; set; }
    public bool ShowChromatogramUnmatchedAnnotations { get; set; }
    public bool ShowScanIntensityLabels { get; set; }
    public bool ShowScanMatchedAnnotations { get; set; }
    public bool ShowScanMzLabels { get; set; }
    public bool ShowScanUnmatchedAnnotations { get; set; }
    public bool ScanMatchToleranceOverride { get; set; }
    public bool ChromatogramMatchToleranceOverride { get; set; }
    public double MzMatchTolerance { get; set; }
    public int MzMatchToleranceUnit { get; set; }
    public double TimeMatchTolerance { get; set; }
    public int TimeMatchToleranceUnit { get; set; }

    /// <summary>Match the API of <c>System.Configuration.ApplicationSettingsBase.Save</c>.
    /// In-memory backing means this is a no-op for now.</summary>
    public void Save() { /* persist support is a follow-up */ }
}
