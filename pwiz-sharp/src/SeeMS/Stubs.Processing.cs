// Minimal stand-in for the cpp Processing.cs while the full panel-bound port is parked.
// Manager.cs / Types.cs reference IProcessing + ProcessingFactory; we ship the interface
// without the WinForms options-panel surface.

using System;
using System.Windows.Forms;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Processing;

namespace Pwiz.SeeMS;

/// <summary>Hook for spectrum / chromatogram processing applied before display.
/// Stub of the cpp <c>seems.IProcessing</c>.</summary>
public interface IProcessing
{
    /// <summary>Short description (e.g. "Centroiding").</summary>
    string ToString();

    /// <summary>Wraps an inner SpectrumList / ChromatogramList with a processing pipeline.</summary>
    ProcessableListType ProcessList<ProcessableListType>(ProcessableListType innerList) where ProcessableListType : class;

    /// <summary>Returns a ProcessingMethod describing this processor.</summary>
    ProcessingMethod ToProcessingMethod();

    /// <summary>CV term identifying the processor.</summary>
    CVID CVID { get; }

    /// <summary>True iff active.</summary>
    bool Enabled { get; set; }

    /// <summary>Per-processor WinForms options panel. May return null in the stub.</summary>
    Panel OptionsPanel { get; }

    /// <summary>Fires when the panel mutates options.</summary>
    event EventHandler OptionsChanged;
}

/// <summary>Stub for cpp <c>seems.ProcessingFactory</c>; returns null until the
/// panel-bound port lands.</summary>
public static class ProcessingFactory
{
    /// <summary>Always returns null in the stub.</summary>
    public static IProcessing? ParseArgument(string arg) { _ = arg; return null; }
}
