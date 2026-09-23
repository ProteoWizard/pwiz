// Port of pwiz_tools/BiblioSpec/src/MaxQuantModReader.{h,cpp}
//
// Sequentially parses MaxQuant modifications.xml and mqpar.xml files.
//   - modifications.xml: map mod name -> composition, position, sites
//   - mqpar.xml: list of fixed mods, list of raw files, per-group labeling mods
//
// cpp uses Expat via the SAXHandler base class; the C# port uses System.Xml.XmlReader
// instead (the cpp parser is stateful — start/end-element callbacks with a STATE enum
// — and XmlReader's pull model maps cleanly onto that with a switch on the same
// STATE in the read loop).

using System.Globalization;
using System.Xml;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// One MaxQuant modification: name, mass delta, position, and the AA sites it applies to.
/// </summary>
/// <remarks>cpp parity: MaxQuantModReader.h:44 <c>struct MaxQuantModification</c>.</remarks>
internal sealed class MaxQuantModification
{
    /// <summary>
    /// Where on the peptide the modification is allowed. cpp parity:
    /// MaxQuantModReader.h:47 <c>MAXQUANT_MOD_POSITION</c>.
    /// </summary>
    public enum MaxQuantModPosition
    {
        Anywhere,
        ProteinCTerm,
        ProteinNTerm,
        AnyNTerm,
        AnyCTerm,
        NotCTerm,
        NotNTerm,
    }

    public string Name { get; set; } = string.Empty;
    public double MassDelta { get; set; }
    public MaxQuantModPosition Position { get; set; } = MaxQuantModPosition.Anywhere;
    public HashSet<char> Sites { get; } = new();

    public void Clear()
    {
        Name = string.Empty;
        MassDelta = 0.0;
        Position = MaxQuantModPosition.Anywhere;
        Sites.Clear();
    }

    /// <summary>
    /// Given the modification's name, look it up in <paramref name="modBank"/>. cpp parity:
    /// MaxQuantModReader.h:70 <c>find</c>. Returns null when not found.
    /// </summary>
    public static MaxQuantModification? Find(IReadOnlyDictionary<string, MaxQuantModification> modBank, string name)
    {
        return modBank.TryGetValue(name, out var mod) ? mod : null;
    }
}

/// <summary>
/// One labeling state for one raw file from a mqpar.xml file.
/// </summary>
/// <remarks>cpp parity: MaxQuantModReader.h:82 <c>struct MaxQuantLabelingState</c>.</remarks>
internal sealed class MaxQuantLabelingState
{
    public List<string> ModsStrings { get; } = new();
    public List<MaxQuantModification> Mods { get; } = new();
    public Dictionary<MaxQuantModification.MaxQuantModPosition, List<MaxQuantModification>> ModsByPosition { get; } = new();
}

/// <summary>
/// A set of labeling states for one raw file in a mqpar.xml file.
/// </summary>
/// <remarks>cpp parity: MaxQuantModReader.h:93 <c>struct MaxQuantLabels</c>.</remarks>
internal sealed class MaxQuantLabels
{
    public string RawFile { get; }
    public List<MaxQuantLabelingState> LabelingStates { get; } = new();

    public MaxQuantLabels(string filename)
    {
        RawFile = filename;
    }

    /// <summary>cpp parity: MaxQuantModReader.h:108 <c>addModsStrings</c>.</summary>
    public void AddModsStrings(IEnumerable<string> modsToAdd)
    {
        var newState = new MaxQuantLabelingState();
        newState.ModsStrings.AddRange(modsToAdd);
        LabelingStates.Add(newState);
    }

    /// <summary>cpp parity: MaxQuantModReader.h:117 <c>addMods</c>.</summary>
    public static void AddMods(MaxQuantLabelingState state, IList<MaxQuantModification> modsToAdd)
    {
        state.Mods.Clear();
        state.Mods.AddRange(modsToAdd);

        // cpp parity: MaxQuantModReader.h:122 initialise the 5 supported position buckets.
        state.ModsByPosition[MaxQuantModification.MaxQuantModPosition.Anywhere] = new();
        state.ModsByPosition[MaxQuantModification.MaxQuantModPosition.AnyNTerm] = new();
        state.ModsByPosition[MaxQuantModification.MaxQuantModPosition.AnyCTerm] = new();
        state.ModsByPosition[MaxQuantModification.MaxQuantModPosition.NotNTerm] = new();
        state.ModsByPosition[MaxQuantModification.MaxQuantModPosition.NotCTerm] = new();

        foreach (var mod in modsToAdd)
        {
            if (!state.ModsByPosition.TryGetValue(mod.Position, out var bucket))
            {
                bucket = new List<MaxQuantModification>();
                state.ModsByPosition[mod.Position] = bucket;
            }
            bucket.Add(mod);
        }
    }

    /// <summary>cpp parity: MaxQuantModReader.h:136 <c>findLabels</c>.</summary>
    public static MaxQuantLabels? FindLabels(IList<MaxQuantLabels> labelBank, string filename)
    {
        foreach (var entry in labelBank)
        {
            if (string.Equals(entry.RawFile, filename, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }
}

/// <summary>
/// Reads MaxQuant <c>modifications.xml</c> and <c>mqpar.xml</c> files.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::MaxQuantModReader</c> (MaxQuantModReader.{h,cpp}). The cpp
/// version inherits from <c>SAXHandler</c>; the C# port uses <see cref="XmlReader"/> in
/// a pull-loop and reproduces the cpp STATE machine inside it.</para>
/// <para>One instance handles either the modifications.xml flavour (constructed with a
/// <see cref="MaxQuantModification"/> bank) OR the mqpar.xml flavour (constructed with
/// a fixed-mods set + a label bank) — not both. That mirrors the cpp two-ctor design.</para>
/// </remarks>
internal sealed class MaxQuantModReader
{
    private enum State
    {
        Root,
        // modifications.xml
        ModificationTag,
        ReadingPosition,
        // mqpar.xml
        FixedModificationsTag,
        ReadingFixedModification,
        FilePathsTag,
        ReadingFilePath,
        ParamGroupIndicesTag,
        ReadingParamGroupIndex,
        LabelsTag,
        ReadingLabel,
    }

    // Cached separator arrays for CA1861 (prefer static readonly over constant arrays).
    private static readonly char[] _pathSeparatorChars = { '/', '\\' };
    private static readonly char[] _whitespaceChars = { ' ', '\t' };

    private readonly string _xmlFileName;
    private readonly Dictionary<string, double> _elementMasses = new(StringComparer.Ordinal);

    // modifications.xml mode
    private readonly Dictionary<string, MaxQuantModification>? _modBank;

    // mqpar.xml mode
    private readonly HashSet<string>? _fixedMods;
    private readonly List<MaxQuantLabels>? _labelBank;
    private readonly List<int> _paramGroupIndices = new();
    private int _groupParams;
    private int _rawIndex = -1;
    private bool _haveReadFilenames;

    private State _state = State.Root;
    private MaxQuantModification _curMod = new();
    private string _charBuf = string.Empty;

    /// <summary>
    /// Construct a reader for <c>modifications.xml</c>.
    /// </summary>
    /// <remarks>cpp parity: MaxQuantModReader.cpp:30.</remarks>
    public MaxQuantModReader(string xmlFileName, Dictionary<string, MaxQuantModification> modBank)
    {
        ArgumentNullException.ThrowIfNull(xmlFileName);
        ArgumentNullException.ThrowIfNull(modBank);
        _xmlFileName = xmlFileName;
        _modBank = modBank;
        InitElementMasses();
    }

    /// <summary>
    /// Construct a reader for <c>mqpar.xml</c>.
    /// </summary>
    /// <remarks>cpp parity: MaxQuantModReader.cpp:170.</remarks>
    public MaxQuantModReader(string xmlFileName, HashSet<string> fixedMods, List<MaxQuantLabels> labelBank)
    {
        ArgumentNullException.ThrowIfNull(xmlFileName);
        ArgumentNullException.ThrowIfNull(fixedMods);
        ArgumentNullException.ThrowIfNull(labelBank);
        _xmlFileName = xmlFileName;
        _fixedMods = fixedMods;
        _labelBank = labelBank;
        // Element masses are only needed in modifications.xml mode; cpp leaves the map empty
        // here too.
    }

    /// <summary>
    /// Read the XML file and populate either <see cref="_modBank"/> or
    /// <see cref="_fixedMods"/> + <see cref="_labelBank"/>.
    /// </summary>
    public void Parse()
    {
        var settings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
            DtdProcessing = DtdProcessing.Ignore,
            CloseInput = true,
        };

        using var reader = XmlReader.Create(_xmlFileName, settings);
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    HandleStartElement(reader);
                    if (reader.IsEmptyElement)
                    {
                        // For self-closing tags <foo/>, XmlReader does NOT emit an EndElement,
                        // so simulate one here so the state machine stays balanced. cpp's
                        // SAXHandler likewise fires startElement+endElement back-to-back.
                        HandleEndElement(reader.LocalName);
                    }
                    break;
                case XmlNodeType.EndElement:
                    HandleEndElement(reader.LocalName);
                    break;
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.SignificantWhitespace:
                case XmlNodeType.Whitespace:
                    HandleCharacters(reader.Value);
                    break;
            }
        }
    }

    private void HandleStartElement(XmlReader reader)
    {
        var name = reader.LocalName;

        // modifications.xml mode
        if (_modBank != null && _fixedMods == null)
        {
            if (string.Equals(name, "modification", StringComparison.Ordinal))
            {
                _curMod = new MaxQuantModification
                {
                    Name = GetRequiredAttr(reader, "title"),
                };
                var modComposition = GetRequiredAttr(reader, "composition");
                _curMod.MassDelta = ParseComposition(modComposition);
                _state = State.ModificationTag;
                return;
            }
            if (_state == State.ModificationTag)
            {
                if (string.Equals(name, "position", StringComparison.Ordinal))
                {
                    _charBuf = string.Empty;
                    _state = State.ReadingPosition;
                }
                else if (string.Equals(name, "modification_site", StringComparison.Ordinal))
                {
                    var siteAttr = GetRequiredAttr(reader, "site");
                    if (siteAttr.Length > 0)
                    {
                        var modSite = siteAttr[0];
                        if (modSite >= 'A' && modSite <= 'Z')
                            _curMod.Sites.Add(modSite);
                    }
                }
            }
            return;
        }

        // mqpar.xml mode
        if (_modBank == null && _fixedMods != null)
        {
            if (string.Equals(name, "fixedModifications", StringComparison.OrdinalIgnoreCase))
            {
                _state = State.FixedModificationsTag;
            }
            else if (_state == State.FixedModificationsTag
                     && string.Equals(name, "string", StringComparison.OrdinalIgnoreCase))
            {
                _charBuf = string.Empty;
                _state = State.ReadingFixedModification;
            }
            else if (!_haveReadFilenames
                     && (string.Equals(name, "filePaths", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(name, "Filenames", StringComparison.OrdinalIgnoreCase)))
            {
                _state = State.FilePathsTag;
            }
            else if (_state == State.FilePathsTag
                     && string.Equals(name, "string", StringComparison.OrdinalIgnoreCase))
            {
                _charBuf = string.Empty;
                _state = State.ReadingFilePath;
            }
            else if (string.Equals(name, "paramGroupIndices", StringComparison.OrdinalIgnoreCase))
            {
                _state = State.ParamGroupIndicesTag;
            }
            else if (_state == State.ParamGroupIndicesTag
                     && string.Equals(name, "int", StringComparison.OrdinalIgnoreCase))
            {
                _charBuf = string.Empty;
                _state = State.ReadingParamGroupIndex;
            }
            else if (string.Equals(name, "GroupParams", StringComparison.OrdinalIgnoreCase)
                     || (string.Equals(name, "parameterGroups", StringComparison.OrdinalIgnoreCase) && _paramGroupIndices.Count > 0))
            {
                ++_groupParams;
            }
            else if (_groupParams > 0
                     && (string.Equals(name, "labels", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(name, "labelMods", StringComparison.OrdinalIgnoreCase)))
            {
                ++_rawIndex;
                _state = State.LabelsTag;
            }
            else if (_state == State.LabelsTag
                     && string.Equals(name, "string", StringComparison.OrdinalIgnoreCase))
            {
                _charBuf = string.Empty;
                _state = State.ReadingLabel;
            }
        }
    }

    private void HandleEndElement(string name)
    {
        // modifications.xml mode
        if (_modBank != null && _fixedMods == null)
        {
            if (string.Equals(name, "modification", StringComparison.OrdinalIgnoreCase))
            {
                if (_curMod.MassDelta != 0.0)
                    _modBank[_curMod.Name] = _curMod;
                _state = State.Root;
            }
            else if (_state == State.ReadingPosition)
            {
                _curMod.Position = StringToPosition(_charBuf);
                _state = State.ModificationTag;
            }
            return;
        }

        // mqpar.xml mode
        if (_modBank == null && _fixedMods != null && _labelBank != null)
        {
            if (string.Equals(name, "fixedModifications", StringComparison.OrdinalIgnoreCase))
            {
                _state = State.Root;
            }
            else if (_state == State.ReadingFixedModification)
            {
                _fixedMods.Add(_charBuf);
                _state = State.FixedModificationsTag;
            }
            else if (string.Equals(name, "filePaths", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(name, "Filenames", StringComparison.OrdinalIgnoreCase))
            {
                _haveReadFilenames = true;
                _state = State.Root;
            }
            else if (_state == State.ReadingFilePath)
            {
                var rawBaseName = _charBuf;
                var lastSlash = rawBaseName.LastIndexOfAny(_pathSeparatorChars);
                if (lastSlash >= 0)
                    rawBaseName = rawBaseName.Substring(lastSlash + 1);
                var extensionBegin = rawBaseName.LastIndexOf('.');
                if (extensionBegin >= 0)
                    rawBaseName = rawBaseName.Substring(0, extensionBegin);

                _labelBank.Add(new MaxQuantLabels(rawBaseName));
                _state = State.FilePathsTag;
            }
            else if (string.Equals(name, "paramGroupIndices", StringComparison.OrdinalIgnoreCase))
            {
                // cpp parity: MaxQuantModReader.cpp:327 — raw files must match indices.
                if (_paramGroupIndices.Count != _labelBank.Count)
                {
                    throw new BlibException(false,
                        string.Format(CultureInfo.InvariantCulture,
                            "Number of raw files ({0}) did not match number of paramGroupIndices ({1}).",
                            _labelBank.Count, _paramGroupIndices.Count));
                }
                _state = State.Root;
            }
            else if (_state == State.ReadingParamGroupIndex)
            {
                if (!int.TryParse(_charBuf, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                {
                    throw new BlibException(false,
                        $"Could not parse paramGroupIndices entry '{_charBuf}' as integer.");
                }
                _paramGroupIndices.Add(idx);
                _state = State.ParamGroupIndicesTag;
            }
            else if (string.Equals(name, "parameterGroups", StringComparison.OrdinalIgnoreCase)
                     && _paramGroupIndices.Count > 0)
            {
                // cpp parity: MaxQuantModReader.cpp:343 — each raw file must have a corresponding group.
                foreach (var i in _paramGroupIndices)
                {
                    if (i > _rawIndex)
                    {
                        throw new BlibException(false,
                            string.Format(CultureInfo.InvariantCulture,
                                "Parameter group index {0} was outside the range of parameter groups ({1}).",
                                i, _rawIndex + 1));
                    }
                }
            }
            else if (string.Equals(name, "GroupParams", StringComparison.OrdinalIgnoreCase))
            {
                if (--_groupParams == 0 && _rawIndex + 1 != _labelBank.Count)
                {
                    throw new BlibException(false,
                        string.Format(CultureInfo.InvariantCulture,
                            "Number of raw files ({0}) did not match number of label sets ({1}).",
                            _labelBank.Count, _rawIndex + 1));
                }
            }
            else if (_state == State.LabelsTag
                     && (string.Equals(name, "labels", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(name, "labelMods", StringComparison.OrdinalIgnoreCase)))
            {
                _state = State.Root;
            }
            else if (_state == State.ReadingLabel)
            {
                // cpp parity: MaxQuantModReader.cpp:371 — split on ';' + trim each part.
                var newLabelSubset = new List<string>();
                foreach (var raw in _charBuf.Split(';'))
                    newLabelSubset.Add(raw.Trim());

                if (_paramGroupIndices.Count > 0)
                {
                    // cpp parity: MaxQuantModReader.cpp:378 — assign to all raw files at this paramGroup.
                    for (var i = 0; i < _paramGroupIndices.Count; i++)
                    {
                        if (_paramGroupIndices[i] == _rawIndex)
                        {
                            _labelBank[i].AddModsStrings(newLabelSubset);
                            Verbosity.Debug(
                                string.Format(CultureInfo.InvariantCulture,
                                    "Adding to labelBank_[{0}] : {1}", i, _charBuf));
                        }
                    }
                }
                else
                {
                    _labelBank[_rawIndex].AddModsStrings(newLabelSubset);
                }

                _state = State.LabelsTag;
            }
        }
    }

    private void HandleCharacters(string text)
    {
        if (_state == State.ReadingPosition
            || _state == State.ReadingFixedModification
            || _state == State.ReadingFilePath
            || _state == State.ReadingParamGroupIndex
            || _state == State.ReadingLabel)
        {
            _charBuf += text;
        }
    }

    private static string GetRequiredAttr(XmlReader reader, string name)
    {
        var v = reader.GetAttribute(name);
        if (v == null)
            throw new BlibException(false, $"Required attribute '{name}' missing from <{reader.LocalName}>.");
        return v;
    }

    /// <summary>
    /// cpp parity: MaxQuantModReader.cpp:416 — sum element-mass contributions in
    /// a composition string like "C(2) H(2) O".
    /// </summary>
    private double ParseComposition(string composition)
    {
        double deltaMass = 0.0;

        // cpp parity: bal::split on whitespace with token_compress_on.
        var components = composition.Split(_whitespaceChars, StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in components)
        {
            var component = raw.Trim();
            if (component.Length == 0) continue;

            string element;
            int quantity;

            var openQuantity = component.IndexOf('(');
            if (openQuantity >= 0)
            {
                element = component.Substring(0, openQuantity);
                var closeQuantity = component.IndexOf(')', openQuantity + 1);
                if (closeQuantity < 0)
                {
                    Verbosity.Warn(string.Format(CultureInfo.InvariantCulture,
                        "Invalid composition '{0}': '(' without ')'", composition));
                    return 0.0;
                }
                var quantityStr = component.Substring(openQuantity + 1, closeQuantity - openQuantity - 1);
                if (!int.TryParse(quantityStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity))
                {
                    Verbosity.Warn(string.Format(CultureInfo.InvariantCulture,
                        "Invalid quantity for '{0}': '{1}'", composition, quantityStr));
                    return 0.0;
                }
            }
            else
            {
                element = component;
                quantity = 1;
            }

            if (!_elementMasses.TryGetValue(element, out var mass))
            {
                Verbosity.Warn(string.Format(CultureInfo.InvariantCulture,
                    "Unknown element for '{0}': '{1}'", composition, element));
                return 0.0;
            }

            deltaMass += quantity * mass;
        }

        return deltaMass;
    }

    /// <summary>
    /// cpp parity: MaxQuantModReader.cpp:480 — translate position string to enum
    /// (case-insensitive match).
    /// </summary>
    private static MaxQuantModification.MaxQuantModPosition StringToPosition(string positionString)
    {
        if (string.Equals(positionString, "anywhere", StringComparison.OrdinalIgnoreCase))
            return MaxQuantModification.MaxQuantModPosition.Anywhere;
        if (string.Equals(positionString, "proteinNterm", StringComparison.OrdinalIgnoreCase))
            return MaxQuantModification.MaxQuantModPosition.ProteinNTerm;
        if (string.Equals(positionString, "proteinCterm", StringComparison.OrdinalIgnoreCase))
            return MaxQuantModification.MaxQuantModPosition.ProteinCTerm;
        if (string.Equals(positionString, "anyNterm", StringComparison.OrdinalIgnoreCase))
            return MaxQuantModification.MaxQuantModPosition.AnyNTerm;
        if (string.Equals(positionString, "anyCterm", StringComparison.OrdinalIgnoreCase))
            return MaxQuantModification.MaxQuantModPosition.AnyCTerm;
        if (string.Equals(positionString, "notNterm", StringComparison.OrdinalIgnoreCase))
            return MaxQuantModification.MaxQuantModPosition.NotNTerm;
        if (string.Equals(positionString, "notCterm", StringComparison.OrdinalIgnoreCase))
            return MaxQuantModification.MaxQuantModPosition.NotCTerm;
        throw new BlibException(false, $"Invalid position value: {positionString}");
    }

    /// <summary>
    /// Element mass table. Values from MaxQuant's MonoIsotopicMass at
    /// https://github.com/JurgenCox/compbio-base/blob/master/BaseLibS/Mol/ChemElements.cs.
    /// cpp parity: MaxQuantModReader.cpp:36-165.
    /// </summary>
    private void InitElementMasses()
    {
        _elementMasses["H"] = 1.0078250321;
        _elementMasses["[1H]"] = 1.0078250321;
        _elementMasses["Hx"] = _elementMasses["2H"] = 2.014101778;
        _elementMasses["T"] = 3.0160492777;
        _elementMasses["He"] = 4.00260325415;
        _elementMasses["Li"] = 7.016004;
        _elementMasses["B"] = 11.0093055;
        _elementMasses["Be"] = 9.0121822;
        _elementMasses["C"] = 12;
        _elementMasses["Cx"] = _elementMasses["13C"] = 13.0033548378;
        _elementMasses["N"] = 14.0030740052;
        _elementMasses["Nx"] = _elementMasses["15N"] = 15.0001088984;
        _elementMasses["O"] = 15.9949146221;
        _elementMasses["Ox"] = 17.9991604;
        _elementMasses["Oy"] = 16.9991315;
        _elementMasses["F"] = 18.9984032;
        _elementMasses["Ne"] = 19.9924401754;
        _elementMasses["Na"] = 22.98976967;
        _elementMasses["Mg"] = 23.9850417;
        _elementMasses["Al"] = 26.98153863;
        _elementMasses["P"] = 30.97376151;
        _elementMasses["S"] = 31.97207069;
        _elementMasses["Sx"] = 33.96786683;
        _elementMasses["Sy"] = 32.9714585;
        _elementMasses["Si"] = 27.9769265325;
        _elementMasses["Cl"] = 34.96885271;
        _elementMasses["Clx"] = 36.9659026;
        _elementMasses["Ar"] = 39.9623831225;
        _elementMasses["K"] = 38.9637069;
        _elementMasses["Kx"] = 40.96182597;
        _elementMasses["Sc"] = 44.9559119;
        _elementMasses["Ti"] = 47.9479463;
        _elementMasses["Ca"] = 39.9625912;
        _elementMasses["V"] = 50.9439595;
        _elementMasses["Cr"] = 51.9405075;
        _elementMasses["Mn"] = 54.9380451;
        _elementMasses["Fe"] = 55.9349421;
        _elementMasses["Fex"] = 55.9349421;
        _elementMasses["Fey"] = 56.9353987;
        _elementMasses["Ni"] = 57.9353479;
        _elementMasses["Co"] = 58.933195;
        _elementMasses["Cu"] = 62.9296011;
        _elementMasses["Zn"] = 63.9291466;
        _elementMasses["Ga"] = 68.9255736;
        _elementMasses["Ge"] = 73.9211778;
        _elementMasses["As"] = 74.9215964;
        _elementMasses["Se"] = 79.9165218;
        _elementMasses["Br"] = 78.9183376;
        _elementMasses["Kr"] = 83.911507;
        _elementMasses["Rb"] = 84.911789738;
        _elementMasses["Sr"] = 87.9056121;
        _elementMasses["Y"] = 88.9058483;
        _elementMasses["Zr"] = 89.9047044;
        _elementMasses["Nb"] = 92.9063781;
        _elementMasses["Rh"] = 102.905504;
        _elementMasses["Ag"] = 106.905093;
        _elementMasses["Mo"] = 97.9054078;
        _elementMasses["Tc"] = 98.9062547;
        _elementMasses["Ru"] = 101.9043493;
        _elementMasses["Pd"] = 105.903486;
        _elementMasses["Cd"] = 113.9033585;
        _elementMasses["Sb"] = 120.9038157;
        _elementMasses["Sn"] = 119.9021947;
        _elementMasses["I"] = 126.904468;
        _elementMasses["In"] = 114.903878;
        _elementMasses["Te"] = 129.9062244;
        _elementMasses["La"] = 138.9063533;
        _elementMasses["Ce"] = 139.9054387;
        _elementMasses["Xe"] = 131.9041535;
        _elementMasses["Ba"] = 137.9052472;
        _elementMasses["Cs"] = 132.905451933;
        _elementMasses["Pr"] = 140.9076528;
        _elementMasses["Nd"] = 141.9077233;
        _elementMasses["Pm"] = 144.912749;
        _elementMasses["Eu"] = 152.9212303;
        _elementMasses["Sm"] = 151.9197324;
        _elementMasses["Gd"] = 157.9241039;
        _elementMasses["Tb"] = 158.9253468;
        _elementMasses["Dy"] = 163.9291748;
        _elementMasses["Ho"] = 164.9303221;
        _elementMasses["Er"] = 165.9302931;
        _elementMasses["Tm"] = 168.9342133;
        _elementMasses["Yb"] = 173.9388621;
        _elementMasses["Lu"] = 174.9407718;
        _elementMasses["Hf"] = 179.94655;
        _elementMasses["Ta"] = 180.9479958;
        _elementMasses["Re"] = 186.9557531;
        _elementMasses["Ir"] = 192.9629264;
        _elementMasses["W"] = 183.9509312;
        _elementMasses["Os"] = 191.9614807;
        _elementMasses["Pt"] = 194.9647911;
        _elementMasses["Au"] = 196.966552;
        _elementMasses["Hg"] = 201.970626;
        _elementMasses["Pb"] = 207.9766521;
        _elementMasses["Tl"] = 204.9744275;
        _elementMasses["Bi"] = 208.9803987;
        _elementMasses["Po"] = 208.9824304;
        _elementMasses["At"] = 209.987148;
        _elementMasses["Rn"] = 222.0175777;
        _elementMasses["Fr"] = 223.0197359;
        _elementMasses["Ra"] = 226.0254098;
        _elementMasses["Ac"] = 227.0277521;
        _elementMasses["Th"] = 232.0380553;
        _elementMasses["Pa"] = 231.035884;
        _elementMasses["U"] = 238.0507882;
        _elementMasses["Np"] = 237.0481734;
        _elementMasses["Pu"] = 244.064204;
        _elementMasses["Am"] = 243.0613811;
        _elementMasses["Cm"] = 247.070354;
        _elementMasses["Bk"] = 247.070307;
        _elementMasses["Cf"] = 251.079587;
        _elementMasses["Es"] = 252.08298;
        _elementMasses["Fm"] = 257.095105;
        _elementMasses["Md"] = 258.098431;
        _elementMasses["No"] = 259.10103;
        _elementMasses["Lr"] = 262.10963;
        _elementMasses["Rf"] = 265.1167;
        _elementMasses["Db"] = 268.12545;
        _elementMasses["Sg"] = 271.13347;
        _elementMasses["Bh"] = 272.13803;
        _elementMasses["Hs"] = 270.13465;
        _elementMasses["Mt"] = 276.15116;
        _elementMasses["Ds"] = 281.16206;
        _elementMasses["Rg"] = 280.16447;
        _elementMasses["Cn"] = 285.17411;
    }
}
