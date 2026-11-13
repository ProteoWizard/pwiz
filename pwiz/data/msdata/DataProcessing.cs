using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;

namespace Pwiz.Data.MsData.Processing;

/// <summary>One processing step. Port of pwiz::msdata::ProcessingMethod.</summary>
public sealed class ProcessingMethod : ParamContainer
{
    /// <summary>Order of this step within a <see cref="DataProcessing"/> sequence.</summary>
    public int Order { get; set; }

    /// <summary>Software that ran this step.</summary>
    public Software? Software { get; set; }

    /// <inheritdoc/>
    public override bool IsEmpty =>
        Order == 0 && Software is null && base.IsEmpty;
}

/// <summary>A sequence of <see cref="ProcessingMethod"/>s.</summary>
/// <remarks>Port of pwiz::msdata::DataProcessing.</remarks>
public sealed class DataProcessing
{
    /// <summary>Unique id for this processing block.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Ordered list of processing methods.</summary>
    public List<ProcessingMethod> ProcessingMethods { get; } = new();

    /// <summary>Creates an empty DataProcessing.</summary>
    public DataProcessing() { }

    /// <summary>Creates a DataProcessing with the given id.</summary>
    public DataProcessing(string id) => Id = id ?? string.Empty;

    /// <summary>True iff all fields are empty.</summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(Id) && ProcessingMethods.Count == 0;
}
