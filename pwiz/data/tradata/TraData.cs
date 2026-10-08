using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.TraData;

/// <summary>The current TraML schema version emitted by sharp.</summary>
public static class TraMlVersion
{
    /// <summary>Schema version used when writing fresh TraML documents.</summary>
    public const string Current = "1.0.0";

    /// <summary>Default CV list — PSI-MS + Unit Ontology + UNIMOD. Mirrors
    /// cpp's <c>tradata::defaultCVList()</c>.</summary>
    public static List<CV> DefaultCVList() => new()
    {
        new CV { Id = "MS", FullName = "Proteomics Standards Initiative Mass Spectrometry Ontology",
                 Version = "3.30.0", Uri = "http://psidev.cvs.sourceforge.net/viewvc/psidev/psi/psi-ms/mzML/controlledVocabulary/psi-ms.obo" },
        new CV { Id = "UO", FullName = "Unit Ontology",
                 Version = "12:10:2011", Uri = "http://obo.cvs.sourceforge.net/viewvc/obo/obo/ontology/phenotype/unit.obo" },
        new CV { Id = "UNIMOD", FullName = "UNIMOD",
                 Version = "2011-03-25", Uri = "http://www.unimod.org/obo/unimod.obo" },
    };
}

/// <summary>Person + organization information. cpp <c>tradata::Contact</c>.</summary>
public sealed class Contact : ParamContainer
{
    /// <summary>Document-scoped identifier (for <c>contactRef</c> resolution).</summary>
    public string Id { get; set; }

    /// <summary>Constructs a Contact with the given id (default empty).</summary>
    public Contact(string id = "") { Id = id ?? string.Empty; }

    /// <summary>True iff id is empty and the param container is empty.</summary>
    public new bool IsEmpty => string.IsNullOrEmpty(Id) && base.IsEmpty;
}

/// <summary>Bibliographic reference. cpp <c>tradata::Publication</c>.</summary>
public sealed class Publication : ParamContainer
{
    /// <summary>Document-scoped identifier.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>True iff id is empty and the param container is empty.</summary>
    public new bool IsEmpty => string.IsNullOrEmpty(Id) && base.IsEmpty;
}

/// <summary>Software used to generate / process the transition list.
/// cpp <c>tradata::Software</c>.</summary>
public sealed class Software : ParamContainer
{
    /// <summary>Document-scoped identifier (for <c>softwareRef</c> resolution).</summary>
    public string Id { get; set; }

    /// <summary>Software version string (free-form).</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Constructs a Software with the given id (default empty).</summary>
    public Software(string id = "") { Id = id ?? string.Empty; }

    /// <summary>Constructs a Software with an id, a tagging CV term (e.g. <see cref="CVID.MS_pwiz"/>),
    /// and a version string. Mirrors cpp's 3-arg constructor (cpp takes a full CVParam; sharp
    /// accepts a CVID since that's the common case).</summary>
    public Software(string id, CVID softwareCvid, string version) : this(id)
    {
        Set(softwareCvid);
        Version = version ?? string.Empty;
    }

    /// <summary>True iff id is empty, version is empty, and the param container is empty.</summary>
    public new bool IsEmpty => string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(Version) && base.IsEmpty;
}

/// <summary>Retention-time tagging (predicted or empirical) on a Peptide / Compound /
/// Transition / Target. cpp <c>tradata::RetentionTime</c>.</summary>
public sealed class RetentionTime : ParamContainer
{
    /// <summary>Optional reference to the software that produced this retention time.</summary>
    public Software? Software { get; set; }
    /// <summary>True iff Software is null and the param container is empty.</summary>
    public new bool IsEmpty => Software is null && base.IsEmpty;
}

/// <summary>Software + contact provenance for a predicted transition.
/// cpp <c>tradata::Prediction</c>.</summary>
public sealed class Prediction : ParamContainer
{
    /// <summary>Optional reference to a prediction-producing software.</summary>
    public Software? Software { get; set; }
    /// <summary>Optional reference to a contact who generated the prediction.</summary>
    public Contact? Contact { get; set; }
    /// <summary>True iff both references are null and the param container is empty.</summary>
    public new bool IsEmpty => Software is null && Contact is null && base.IsEmpty;
}

/// <summary>Empirical-evidence metadata bound to a peptide. cpp <c>tradata::Evidence</c>.</summary>
public sealed class Evidence : ParamContainer { }

/// <summary>Validation result for a transition on a given instrument configuration.
/// cpp <c>tradata::Validation</c>.</summary>
public sealed class Validation : ParamContainer { }

/// <summary>Mass spectrometer instrument referenced from <see cref="Configuration"/>.
/// cpp <c>tradata::Instrument</c>.</summary>
public sealed class Instrument : ParamContainer
{
    /// <summary>Document-scoped identifier (for <c>instrumentRef</c> resolution).</summary>
    public string Id { get; set; }
    /// <summary>Constructs an Instrument with the given id (default empty).</summary>
    public Instrument(string id = "") { Id = id ?? string.Empty; }
    /// <summary>True iff id is empty and the param container is empty.</summary>
    public new bool IsEmpty => string.IsNullOrEmpty(Id) && base.IsEmpty;
}

/// <summary>Instrument configuration used during transition validation / optimization,
/// optionally with one or more <see cref="Validation"/> entries. cpp <c>tradata::Configuration</c>.</summary>
public sealed class Configuration : ParamContainer
{
    /// <summary>Optional reference to the contact who supplied the validation.</summary>
    public Contact? Contact { get; set; }
    /// <summary>Optional reference to the instrument the validation was performed on.</summary>
    public Instrument? Instrument { get; set; }
    /// <summary>Per-(instrument, configuration) validation results.</summary>
    public List<Validation> Validations { get; } = new();
}

/// <summary>One possible interpretation of a transition's product ion (fragment ion type,
/// number, neutral loss, etc.). cpp <c>tradata::Interpretation</c>.</summary>
public sealed class Interpretation : ParamContainer { }

/// <summary>A protein referenced by one or more peptides. cpp <c>tradata::Protein</c>.</summary>
public sealed class Protein : ParamContainer
{
    /// <summary>Document-scoped identifier (for <c>proteinRef</c> resolution).</summary>
    public string Id { get; set; }
    /// <summary>Amino-acid sequence (single-letter, optional).</summary>
    public string Sequence { get; set; } = string.Empty;
    /// <summary>Constructs a Protein with the given id (default empty).</summary>
    public Protein(string id = "") { Id = id ?? string.Empty; }
    /// <summary>True iff id and sequence are empty and the param container is empty.</summary>
    public new bool IsEmpty => string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(Sequence) && base.IsEmpty;
}

/// <summary>A modification on a peptide residue (or terminus). Multiple <see cref="Modification"/>
/// instances on the same location represent ambiguous-mod situations.
/// cpp <c>tradata::Modification</c>.</summary>
public sealed class Modification : ParamContainer
{
    /// <summary>1-based residue position (0 = N-terminus, peptideLength+1 = C-terminus).</summary>
    public int Location { get; set; }
    /// <summary>Monoisotopic mass delta.</summary>
    public double MonoisotopicMassDelta { get; set; }
    /// <summary>Average mass delta.</summary>
    public double AverageMassDelta { get; set; }
    /// <summary>True iff all numeric fields are zero and the param container is empty.</summary>
    public new bool IsEmpty => Location == 0 && MonoisotopicMassDelta == 0.0
                               && AverageMassDelta == 0.0 && base.IsEmpty;
}

/// <summary>A peptide referenced by one or more transitions / targets.
/// cpp <c>tradata::Peptide</c>.</summary>
public sealed class Peptide : ParamContainer
{
    /// <summary>Document-scoped identifier (for <c>peptideRef</c> resolution).</summary>
    public string Id { get; set; }
    /// <summary>Bare amino-acid sequence.</summary>
    public string Sequence { get; set; } = string.Empty;
    /// <summary>Modifications applied to this peptide.</summary>
    public List<Modification> Modifications { get; } = new();
    /// <summary>Resolved parent-protein references.</summary>
    public List<Protein> Proteins { get; } = new();
    /// <summary>Predicted / measured retention-time tags.</summary>
    public List<RetentionTime> RetentionTimes { get; } = new();
    /// <summary>Empirical-evidence metadata block.</summary>
    public Evidence Evidence { get; } = new();
    /// <summary>Constructs a Peptide with the given id (default empty).</summary>
    public Peptide(string id = "") { Id = id ?? string.Empty; }
}

/// <summary>A non-peptide chemical compound referenced by one or more transitions / targets.
/// cpp <c>tradata::Compound</c>.</summary>
public sealed class Compound : ParamContainer
{
    /// <summary>Document-scoped identifier (for <c>compoundRef</c> resolution).</summary>
    public string Id { get; set; }
    /// <summary>Predicted / measured retention-time tags.</summary>
    public List<RetentionTime> RetentionTimes { get; } = new();
    /// <summary>Constructs a Compound with the given id (default empty).</summary>
    public Compound(string id = "") { Id = id ?? string.Empty; }
}

/// <summary>Precursor (Q1) ion description. cpp <c>tradata::Precursor</c>.</summary>
public sealed class Precursor : ParamContainer { }

/// <summary>Product (Q3) ion description. cpp <c>tradata::Product</c>.</summary>
public sealed class Product : ParamContainer { }

/// <summary>A single SRM/MRM/PRM transition. cpp <c>tradata::Transition</c>.</summary>
public sealed class Transition : ParamContainer
{
    /// <summary>String label for the transition (free-form; not a reference target).</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Resolved peptide reference (if peptide-derived).</summary>
    public Peptide? Peptide { get; set; }
    /// <summary>Resolved compound reference (if compound-derived).</summary>
    public Compound? Compound { get; set; }
    /// <summary>Q1 precursor description.</summary>
    public Precursor Precursor { get; } = new();
    /// <summary>Q3 product description.</summary>
    public Product Product { get; } = new();
    /// <summary>Optional prediction provenance.</summary>
    public Prediction Prediction { get; } = new();
    /// <summary>Optional retention-time tag for the transition.</summary>
    public RetentionTime RetentionTime { get; } = new();
    /// <summary>Zero or more interpretations of the product ion.</summary>
    public List<Interpretation> Interpretations { get; } = new();
    /// <summary>Zero or more validated instrument configurations.</summary>
    public List<Configuration> Configurations { get; } = new();
}

/// <summary>An include/exclude precursor m/z target. cpp <c>tradata::Target</c>.</summary>
public sealed class Target : ParamContainer
{
    /// <summary>String label.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Resolved peptide reference (if peptide-derived).</summary>
    public Peptide? Peptide { get; set; }
    /// <summary>Resolved compound reference (if compound-derived).</summary>
    public Compound? Compound { get; set; }
    /// <summary>Q1 precursor description.</summary>
    public Precursor Precursor { get; } = new();
    /// <summary>Optional retention-time tag.</summary>
    public RetentionTime RetentionTime { get; } = new();
    /// <summary>Zero or more validated instrument configurations.</summary>
    public List<Configuration> Configurations { get; } = new();
}

/// <summary>Include and exclude target lists. cpp <c>tradata::TargetList</c>.</summary>
public sealed class TargetList : ParamContainer
{
    /// <summary>Targets to exclude.</summary>
    public List<Target> ExcludeList { get; } = new();
    /// <summary>Targets to include.</summary>
    public List<Target> IncludeList { get; } = new();
}

/// <summary>Top-level TraML document. cpp <c>tradata::TraData</c>.</summary>
public sealed class TraData
{
    /// <summary>Document id (not part of the schema; populated from the file basename).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Declared CVs.</summary>
    public List<CV> CVs { get; } = new();

    /// <summary>Per-document Contact entries.</summary>
    public List<Contact> Contacts { get; } = new();

    /// <summary>Publications cited.</summary>
    public List<Publication> Publications { get; } = new();

    /// <summary>Instruments referenced from configurations.</summary>
    public List<Instrument> Instruments { get; } = new();

    /// <summary>Software entries.</summary>
    public List<Software> Software { get; } = new();

    /// <summary>Proteins referenced by peptides.</summary>
    public List<Protein> Proteins { get; } = new();

    /// <summary>Peptides referenced by transitions and targets.</summary>
    public List<Peptide> Peptides { get; } = new();

    /// <summary>Compounds referenced by transitions and targets.</summary>
    public List<Compound> Compounds { get; } = new();

    /// <summary>Transition list.</summary>
    public List<Transition> Transitions { get; } = new();

    /// <summary>Include / exclude target lists.</summary>
    public TargetList Targets { get; } = new();

    /// <summary>TraML schema version. Defaults to the current emit version; readers
    /// overwrite this with the version read from the file.</summary>
    public string Version { get; set; } = TraMlVersion.Current;

    /// <summary>True iff the document carries no data.</summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(Id)
        && CVs.Count == 0 && Contacts.Count == 0 && Publications.Count == 0
        && Instruments.Count == 0 && Software.Count == 0 && Proteins.Count == 0
        && Peptides.Count == 0 && Compounds.Count == 0
        && Transitions.Count == 0
        && Targets.ExcludeList.Count == 0 && Targets.IncludeList.Count == 0
        && Targets.IsEmpty;
}
