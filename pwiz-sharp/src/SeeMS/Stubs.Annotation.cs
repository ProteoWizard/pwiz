// Minimal stand-in for the cpp Annotation.cs while the full panel-bound port is parked.
// Manager.cs / Types.cs reference IAnnotation; we ship the interface here without dragging
// in the WinForms options-panel surface that the cpp-port ApolloUI panels expose.

using System;
using System.Windows.Forms;
using ZedGraph;

namespace Pwiz.SeeMS;

/// <summary>Hook for code that decorates a graph pane with annotations. Stub of the cpp
/// <c>seems.IAnnotation</c> — just the Update + Enabled + GetOptionsPanel surface. The
/// cpp <c>PeptideFragmentationAnnotation</c> implementation is parked until the
/// AnnotationPanels port lands.</summary>
public interface IAnnotation
{
    /// <summary>Returns a short description (e.g. "Fragmentation (PEPTIDE)").</summary>
    string ToString();

    /// <summary>Decorate the supplied <c>annotations</c> ZedGraph object list.</summary>
    void Update(GraphItem item, pwiz.MSGraph.MSPointList pointList, GraphObjList annotations);

    /// <summary>True iff the annotation is currently active.</summary>
    bool Enabled { get; set; }

    /// <summary>Per-annotation WinForms options panel. May return null in the stub.</summary>
    Panel GetOptionsPanel(bool updateControls = true);

    /// <summary>Fires when the panel mutates options.</summary>
    event EventHandler OptionsChanged;
}

/// <summary>Stub for the cpp <c>seems.AnnotationFactory</c>. The cpp version parses
/// command-line annotation strings into <c>PeptideFragmentationAnnotation</c> instances;
/// returns null until the panel-bound port lands.</summary>
public static class AnnotationFactory
{
    /// <summary>Always returns null in the stub.</summary>
    public static IAnnotation? ParseArgument(string arg) { _ = arg; return null; }
}
