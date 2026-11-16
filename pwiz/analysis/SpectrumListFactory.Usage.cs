namespace Pwiz.Analysis;

public static partial class SpectrumListFactory
{
    /// <summary>
    /// Help for the filters in <see cref="BuildDefaultRegistry"/>, in the order msconvert lists
    /// them (cpp's jumpTable_ order). An entry shows only while its filter is registered, so
    /// <c>lockmassRefiner</c> drops out of a NO_VENDOR_SUPPORT build. A test holds this table
    /// and the registry to the same set of names.
    /// </summary>
    private static readonly FilterUsage[] s_usage =
    {
        new("index", "<index_value_set>",
            "Selects spectra by index - an index value 0-based numerical order in which the spectrum appears in the input.\n" +
            "  <index_value_set> is an int_set of indexes."),
        new("id", "<id_set>",
            "Selects one or more spectra by native IDs separated by semicolon (;).\n" +
            "  <id_set> is a semicolon-delimited set of ids."),
        new("msLevel", "<mslevels>",
            "This filter selects only spectra with the indicated <mslevels>, expressed as an int_set."),
        new("chargeState", "<charge_states>",
            "This filter keeps spectra that match the listed charge state(s), expressed as an int_set.  Both known/single " +
            "and possible/multiple charge states are tested.  Use 0 to include spectra with no charge state at all."),
        new("mzRefiner", "input1.pepXML input2.mzid [msLevels=<1->] [thresholdScore=<CV_Score_Name>] [thresholdValue=<floatset>] [thresholdStep=<float>] [maxSteps=<count>]",
            "This filter recalculates the m/z and charges, adjusting precursors for MS2 spectra and spectra masses for MS1 spectra. " +
            "It uses an ident file with a threshold field and value to calculate the error and will then choose a shifting mechanism to correct masses throughout the file. " +
            "It only works on orbitrap, FT, and TOF data. It is designed to work on mzML files created by msconvert from a single dataset (single run), and with an identification file created using that mzML file. " +
            "It does not use any 3rd party (vendor DLL) code. " +
            "Recommended Scores and thresholds: MS-GF:SpecEValue,-1e-10 (<1e-10); MyriMatch:MVH,35- (>35); xcorr,3- (>3)"),
        new("lockmassRefiner", "mz=<real> mzNegIons=<real (mz)> tol=<real (1.0 Daltons)>",
            "For Waters data, adjusts m/z values according to the specified lockmass m/z and tolerance. Distinct m/z value for negative ions is optional and defaults to the given mz value. For other data, currently does nothing."),
        new("precursorRefine", "",
            "This filter recalculates the precursor m/z and charge for MS2 spectra. " +
            "It looks at the prior MS1 scan to better infer the parent mass.  It only works on orbitrap, FT, and TOF data. " +
            "It does not use any 3rd party (vendor DLL) code."),
        new("peakPicking", "[<PickerType> [snr=<minimum signal-to-noise ratio>] [peakSpace=<minimum peak spacing>] [msLevel=<ms_levels>]]",
            "This filter performs centroiding on spectra with the selected <ms_levels>, expressed as an int_set.  The value for <PickerType> must be \"vendor\" or \"cwt\": " +
            "when <PickerType> = \"vendor\", vendor (Windows DLL) code is used if available, and a low-quality (non-vendor) local maxima algorithm otherwise.  " +
            "IMPORTANT NOTE: since this filter operates on the raw data through the vendor DLLs, IT MUST BE THE FIRST FILTER IN ANY LIST OF FILTERS when \"vendor\" is used. " +
            "\"cwt\" (ProteoWizard's wavelet-based algorithm) is not yet available in this version of msconvert and falls back to the local maxima algorithm; " +
            "snr= and peakSpace= are accepted for compatibility but have no effect.\n" +
            "Defaults:\n" +
            " <PickerType> = \"vendor\"\n" +
            " <ms_levels> = 1-"),
        new("scanNumber", "<scan_numbers>",
            "This filter selects spectra by scan number.  Depending on the input data type, scan number and spectrum index are not always the same thing - scan numbers are not always contiguous, and are usually 1-based.\n" +
            "<scan_numbers> is an int_set of scan numbers to be kept."),
        new("scanEvent", "<scan_event_set>",
            "This filter selects spectra by scan event.  For example, to include all scan events except scan event 5, use " +
            "filter \"scanEvent 1-4 6-\".  A \"scan event\" is a preset scan configuration: a user-defined scan configuration that " +
            "specifies the instrumental settings in which a spectrum is acquired. An instrument may cycle through a list of preset " +
            "scan configurations to acquire data. This is a more generic term for the Thermo \"scan event\", which is defined in " +
            "the Thermo Xcalibur glossary as: \"a mass spectrometer scan that is defined by choosing the necessary scan parameter " +
            "settings. Multiple scan events can be defined for each segment of time.\"."),
        new("scanTime", "<scan_time_range>",
            "This filter selects only spectra within a given time range.\n" +
            "  <scan_time_range> is a time range, specified in seconds.  For example, to select only spectra within the " +
            "second minute of the run, use \"scanTime [60,119.99]\"."),
        new("sortByScanTime", "",
            "This filter reorders spectra, sorting them by ascending scan start time."),
        new("stripIT", "",
            "This filter rejects ion trap data spectra with MS level 1."),
        new("metadataFixer", "",
            "This filter is used to add or replace a spectra's TIC/BPI metadata, usually after " +
            "peakPicking where the change from profile to centroided data may make the TIC and BPI values inconsistent with " +
            "the revised scan data.  The filter traverses the m/z intensity arrays to find the sum and max. For example, in " +
            "msconvert it can be used as: --filter \"peakPicking true 1-\" --filter metadataFixer.  It can also be used without " +
            "peak picking for some strange results. Certainly adding up all the samples of profile data to get the TIC is " +
            "just wrong, but we do it anyway."),
        new("titleMaker", "<format_string>",
            "This filter adds or replaces spectrum titles according to specified " +
            "<format_string>. You can use it, for example, to customize the TITLE line in MGF output in msconvert.  The following " +
            "keywords are recognized: \n" +
            "   \"<RunId>\" - prints the spectrum's Run id - for example, \"Data.d\" from \"C:/Agilent/Data.d/AcqData/mspeak.bin\"\n" +
            "   \"<Index>\" - prints the spectrum's index\n" +
            "   \"<Id>\" - prints the spectrum's nativeID\n" +
            "   \"<SourcePath>\" - prints the path of the spectrum's source data\n" +
            "   \"<ScanNumber>\" - if the nativeID can be represented as a single number, prints that number, else index+1\n" +
            "   \"<IonMobility>\" - for the first scan of a spectrum, prints the \"ion mobility drift time\" or \"inverse reduced ion mobility\" value\n" +
            "   \"<ActivationType>\" - for the first precursor, prints the spectrum's \"dissociation method\" value\n" +
            "   \"<IsolationMz>\" - for the first precursor, prints the spectrum's \"isolation target m/z\" value\n" +
            "   \"<PrecursorSpectrumId>\" - prints the nativeID of the spectrum of the first precursor\n" +
            "   \"<SelectedIonMz>\" - prints the m/z value of the first selected ion of the first precursor\n" +
            "   \"<ChargeState>\" - prints the charge state for the first selected ion of the first precursor\n" +
            "   \"<SpectrumType>\" - prints the spectrum type\n" +
            "   \"<ScanStartTimeInSeconds>\" - prints the spectrum's first scan's start time, in seconds\n" +
            "   \"<ScanStartTimeInMinutes>\" - prints the spectrum's first scan's start time, in minutes\n" +
            "   \"<BasePeakMz>\" - prints the spectrum's base peak m/z\n" +
            "   \"<BasePeakIntensity>\" - prints the spectrum's base peak intensity\n" +
            "   \"<TotalIonCurrent>\" - prints the spectrum's total ion current\n" +
            "   \"<MsLevel>\" - prints the spectrum's MS level\n" +
            "For example, to create a TITLE line in msconvert MGF output with the \"name.first_scan.last_scan.charge\" style (eg. \"mydata.145.145.2\"), use\n" +
            "--filter \"titleMaker <RunId>.<ScanNumber>.<ScanNumber>.<ChargeState>\""),
        new("threshold", "<type> <threshold> <orientation> [<mslevels>]",
            "This filter keeps data whose values meet various threshold criteria.\n" +
            "   <type> must be one of:\n" +
            "      count - keep the n=<threshold> [most|least] intense data points, where n is an integer.  Any data points with the same intensity as the nth [most|least] intense data point are removed.\n" +
            "      count-after-ties - like \"count\", except that any data points with the same intensity as the nth [most|least] data point are retained.\n" +
            "      absolute - keep data whose absolute intensity is [more|less] than <threshold>\n" +
            "      bpi-relative - keep data whose intensity is [more|less] than <threshold> percent of the base peak intensity.  Percentage is expressed as a number between 0 and 1, for example 75 percent is \"0.75\".\n" +
            "      tic-relative - keep data whose individual intensities are [more|less] than <threshold> percent of the total ion current for the scan. Again, percentage is expressed as a number between 0 and 1.\n" +
            "      tic-cutoff - keep the [most|least] intense data points up to <threshold> percent of the total ion current.  That is, the TIC of the retained points is <threshold> percent (expressed as a number between 0 and 1) of the original TIC.\n" +
            "   <orientation> must be one of:\n" +
            "      most-intense (keep m/z-intensity pairs above the threshold)\n" +
            "      least-intense (keep m/z-intensity pairs below the threshold)\n" +
            "   <mslevels> is an optional int_set of MS levels - if provided, only scans with those MS levels will be filtered, and others left untouched."),
        new("mzWindow", "<mzrange>",
            "keeps mz/intensity pairs whose m/z values fall within the specified range.\n" +
            "  <mzrange> is formatted as [mzLow,mzHigh].  For example, in msconvert to retain data in the m/z range " +
            "100.1 to 307.5, use --filter \"mzWindow [100.1,307.5]\" ."),
        new("mzPrecursors", "<precursor_mz_list> [mzTol=<mzTol (10 ppm)>] [target=<selected|isolated> (selected)] [mode=<include|exclude (include)>]",
            "Filters spectra based on precursor m/z values found in the <precursor_mz_list>, with <mzTol> m/z tolerance. To retain " +
            "only spectra with precursor m/z values of 123.4 and 567.8, use --filter \"mzPrecursors [123.4,567.8]\". " +
            "Note that this filter will drop MS1 scans unless you include 0.0 in the list of precursor values." +
            "   <mzTol> is optional and must be specified as a number and units (PPM or MZ). For example, \"5 PPM\" or \"2.1 MZ\".\n" +
            "   <target> is optional and must be either \"selected\" (the default) or \"isolated\". It determines whether the isolated m/z or the selected m/z is used for the \"precursor m/z\"\n" +
            "   <mode> is optional and must be either \"include\" (the default) or \"exclude\".  If \"exclude\" is " +
            "used, the filter drops spectra that match the various criteria instead of keeping them."),
        new("isolationWindows", "<isolation_window_list> [mzTol=<mzTol (10 ppm)>] [mode=<include|exclude (include)>]",
            "Filters spectra based on precursor isolation windows found in the <isolation_window_list>, with <mzTol> m/z tolerance. To retain " +
            "only spectra with isolation windows [123.4,153.4] and [567.8,617.8], use --filter \"isolationWindows [123.4,153.4] [567.8,617.8]\". " +
            "Note that this filter will drop MS1 scans unless you include [0,0] in the list of isolation windows." +
            "   <mzTol> is optional and must be specified as a number and units (PPM or MZ). For example, \"5 PPM\" or \"2.1 MZ\".\n" +
            "   <mode> is optional and must be either \"include\" (the default) or \"exclude\".  If \"exclude\" is " +
            "used, the filter drops spectra that match the various criteria instead of keeping them."),
        new("isolationWidth", "<isolation_width_list> [mzTol=<mzTol (10 ppm)>] [mode=<include|exclude (include)>]",
            "Filters spectra based on precursor isolation width values found in the <isolation_width_list>, with <mzTol> m/z tolerance. To retain " +
            "only spectra with isolation width values of 1.5 and 2.3, use --filter \"isolationWidth [1.5,2.3]\". " +
            "Note that this filter will drop MS1 scans unless you include 0.0 in the list of isolation width values." +
            "   <mzTol> is optional and must be specified as a number and units (PPM or MZ). For example, \"5 PPM\" or \"2.1 MZ\".\n" +
            "   <mode> is optional and must be either \"include\" (the default) or \"exclude\".  If \"exclude\" is " +
            "used, the filter drops spectra that match the various criteria instead of keeping them."),
        new("defaultArrayLength", "<peak_count_range>",
            "Keeps only spectra with peak counts within <peak_count_range>, expressed as an int_set. (In mzML the peak list " +
            "length is expressed as \"defaultArrayLength\", hence the name.)  For example, to include only spectra with 100 " +
            "or more peaks, you would use filter \"defaultArrayLength 100-\" ."),
        new("zeroSamples", "<mode> [<MS_levels>]",
            "This filter deals with zero values in spectra - either removing them, or adding them where they are missing.\n" +
            "  <mode> is either removeExtra or addMissing[=<flankingZeroCount>] .\n" +
            "  <MS_levels> is optional, when provided (as an int_set) the filter is applied only to spectra with those MS levels.\n" +
            "When <mode> is \"removeExtra\", consecutive zero intensity peaks are removed from spectra.  For example, a peak list\n" +
            "  \"100.1,1000 100.2,0 100.3,0 100.4,0 100.5,0 100.6,1030\"\n" +
            "would become \n" +
            "  \"100.1,1000 100.2,0 100.5,0 100.6,1030\"\n" +
            "and a peak list \n" +
            "  \"100.1,0 100.2,0 100.3,0 100.4,0 100.5,0 100.6,1030 100.7,0 100.8,1020 100.9,0 101.0,0\"\n" +
            "would become \n" +
            "  \"100.5,0 100.6,1030 100.7,0 100.8,1020 100.9,0\"\n" +
            "When <mode> is \"addMissing\", each spectrum's sample rate is automatically determined (the rate can change but only " +
            "gradually) and flanking zeros are inserted around non-zero data points.  The optional [=<flankingZeroCount>] value " +
            "can be used to limit the number of flanking zeros, otherwise the spectrum is completely populated between nonzero points. " +
            "For example, to make sure spectra have at least 5 flanking zeros around runs on nonzero points, use filter \"addMissing=5\"."),
        new("mzPresent", "<mz_list> [mzTol=<tolerance> (0.5 mz)] [type=<type> (count)] [threshold=<threshold> (10000)] [orientation=<orientation> (most-intense)] [mode=<include|exclude (include)>]",
            "This filter includes or excludes spectra depending on whether the specified peaks are present.\n" +
            "   <mz_list> is a list of mz values of the form [mz1,mz2, ... mzn] (for example, \"[100, 300, 405.6]\"). " +
            "Spectra which contain peaks within <tolerance> of any of these values will be kept.\n" +
            "   <tolerance> is specified as a number and units (PPM or MZ). For example, \"5 PPM\" or \"2.1 MZ\".\n" +
            "   <type>, <threshold>, and <orientation> operate as in the \"threshold\" filter (see above).\n" +
            "   <include|exclude> is optional and has value \"include\" (the default) or \"exclude\".  If \"exclude\" is " +
            "used the filter drops spectra that match the various criteria instead of keeping them."),
        new("scanSumming", "[precursorTol=<precursor tolerance>] [scanTimeTol=<scan time tolerance in seconds>] [ionMobilityTol=<ion mobility tolerance>]",
            "This filter sums MS2 sub-scans whose precursors are within <precursor tolerance> (default: 0.05 m/z)" +
            ", <scan time tolerance> (default: 10 s), and for ion mobility data, <ion mobility tolerance> (default 0.01 ms or vs/cm^2). It is intended for some Waters DDA data and Bruker PASEF data, where sub-scans " +
            "should be summed together to increase the SNR."),
        new("thermoScanFilter", "<exact|contains> <include|exclude> <match string>",
            "   Includes or excludes scans based on the scan filter (Thermo instrumentation only).\n" +
            "   <exact|contains>: If \"exact\" is set, spectra only match the search string if there is an exact match.\n" +
            "                     If \"contains\" is set, spectra match the search string if the search string is \n" +
            "                     contained somewhere in the scan filter.\n" +
            "   <include|exclude>: If \"include\" is set, only spectra that match the filter will be kept.\n" +
            "                      If \"exclude\" is set, only spectra that do NOT match the filter will be kept.\n" +
            "   <match string> specifies the search string to be compared to each scan filter (it may contain spaces)\n"),
        new("mzShift", "<m/z shift as number and units (e.g. 10ppm or 1Da)> [msLevels=int_set (1-)]",
            "Shifts all m/z values by the specified amount if a spectrum's ms level is in the msLevels set.\n" +
            "Both metadata (e.g. scan window, base peak m/z) and binary data (m/z array) are shifted.\n" +
            "Precursor metadata is only shifted if the spectrum's precursor ms level (ms level - 1) is in the msLevels set.\n" +
            "This allows applying different shifts for different ms levels."),

        // MSn spectrum processing/filtering
        new("MS2Denoise", "[<peaks_in_window> [<window_width_Da> [multicharge_fragment_relaxation]]]",
            "Noise peak removal for spectra with precursor ions.\n" +
            "   <peaks_in_window> - the number peaks to select in moving window, default is 6.\n" +
            "   <window_width_Da> - the width of the window in Da, default is 30.\n" +
            "   <multicharge_fragment_relaxation> - if \"true\" (the default), allows more data below multiply charged precursors.\n" +
            "The filter first removes any m/z values above the precursor mass minus the mass of glycine.\n" +
            "It then removes any m/z values within .5 Da of the unfragmented precursor mass.\n" +
            "Finally it retains only the <peaks_in_window> most intense ions within a " +
            "sliding window of <window_width_Da>.\n" +
            "If <multicharge_fragment_relaxation> is true, allows more peaks at lower mass (i.e. below precursor).\n" +
            "If <window_width_Da> is set to 0, the window size defaults to the highest observed mass in the spectrum " +
            "(this leaving only <peaks_in_window> ions in the output spectrum).\n" +
            "Reference: \"When less can yield more - Computational preprocessing of MS/MS spectra for peptide " +
            "identification\", Bernhard Y. Renard, Marc Kirchner, Flavio Monigatti, Alexander R. Ivanov, " +
            "Juri Rappsilber, Dominic Winter, Judith A. J. Steen, Fred A. Hamprecht and Hanno Steen  " +
            "Proteomics, 9, 4978-4984, 2009.\n"),
        new("MS2Deisotope", "[hi_res [mzTol=<mzTol>]] [Poisson [minCharge=<minCharge>] [maxCharge=<maxCharge>]]",
            "Deisotopes ms2 spectra using the Markey method or a Poisson model.\n" +
            "  For the Markey method, hi_res sets high resolution mode to \"false\" (the default) or \"true\".\n" +
            "  <mzTol> sets the mz tolerance.  It defaults to .01 in high resolution mode, otherwise it defaults to 0.5.\n" +
            "  Poisson activates a Poisson model based on the relative intensity distribution.\n" +
            "  <minCharge> (default: 1) and <maxCharge> (default: 3) define the charge search range within the Poisson deisotoper. (default: 1)"),
        new("ETDFilter", "[<removePrecursor> [<removeChargeReduced> [<removeNeutralLoss> [<blanketRemoval> [<matchingTolerance> ]]]]]",
            "Filters ETD MSn spectrum data points, removing unreacted precursors, charge-reduced precursors, and neutral losses.\n" +
            "  <removePrecursor> - if \"true\", remove unreacted precursor (default is \"true\")\n" +
            "  <removeChargeReduced> - if \"true\", remove charge reduced precursor (default is \"true\")\n" +
            "  <removeNeutralLoss> - if \"true\", remove neutral loss species from charge reduced precursor (default is \"true\")\n" +
            "  <blanketRemoval> - if \"true\", remove neutral losses in a charge-scaled 60 Da swath rather than only around known loss species (default is \"true\")\n" +
            "  <matchingTolerance> - specify matching tolerance in m/z or ppm (examples: \"3.1 mz\" (the default) or \"2.2 ppm\")"),
        new("demultiplex",
            "massError=<tolerance and units, eg 0.5Da (default 10ppm)>" +
            " nnlsMaxIter=<int (50)> nnlsEps=<real (1e-10)>" +
            " noWeighting=<bool (false)>" +
            " demuxBlockExtra=<real (0)>" +
            " variableFill=<bool (false)>" +
            " noSumNormalize=<bool (false)>" +
            " optimization=<(none)|overlap_only>" +
            " interpolateRT=<bool (true)>" +
            " minWindowSize=<real (0.2)>" +
            " removeNonOverlappingEdges=<bool (false)>",
            "Separates overlapping or MSX multiplexed spectra into several demultiplexed spectra by inferring from adjacent multiplexed spectra. Optionally handles variable fill times (for Thermo)."),
        new("chargeStatePredictor", "[overrideExistingCharge=<true|false (false)>] [maxMultipleCharge=<int (3)>] [minMultipleCharge=<int (2)>] [singleChargeFractionTIC=<real (0.9)>] [maxKnownCharge=<int (0)>] [makeMS2=<true|false (false)>]",
            "Predicts MSn spectrum precursors to be singly or multiply charged depending on the ratio of intensity above and below the precursor m/z, or optionally using the \"makeMS2\" algorithm\n" +
            "  <overrideExistingCharge> : always override existing charge information (default:\"false\")\n" +
            "  <maxMultipleCharge> (default 3) and <minMultipleCharge> (default 2): range of values to add to the spectrum's existing \"MS_possible_charge_state\" values." +
            "If these are the same values, the spectrum's MS_possible_charge_state values are removed and replaced with this single value.\n" +
            "  <singleChargeFractionTIC> : is a percentage expressed as a value between 0 and 1 (the default is 0.9, or 90 percent). " +
            "This is the value used as the previously mentioned ratio of intensity above and below the precursor m/z.\n" +
            "  <maxKnownCharge> (default is 0, meaning no maximum): the maximum charge allowed for \"known\" charges even if override existing charge is false. " +
            "This allows overriding junk charge calls like +15 peptides.\n" +
            "  <algorithmMakeMS2> : default is \"false\", when set to \"true\" the \"makeMS2\" algorithm is used instead of the one described above."),
        new("turbocharger", "[minCharge=<minCharge>] [maxCharge=<maxCharge>] [precursorsBefore=<before>] [precursorsAfter=<after>] [halfIsoWidth=<half-width of isolation window>] [defaultMinCharge=<defaultMinCharge>] [defaultMaxCharge=<defaultMaxCharge>] [useVendorPeaks=<useVendorPeaks>]",
            "Predicts MSn spectrum precursor charge based on the isotopic distribution associated with the survey scan(s) of the selected precursor\n" +
            "  <maxCharge> (default: 8) and <minCharge> (default 1): defines range of possible precursor charge states.\n" +
            "  <before> (default: 2) and <after> (default 0): number of survey (MS1) scans to check for precursor isotopes, before and after a MS/MS in retention time.\n" +
            "  <half-width of isolation window> (default: 1.25): half-width of the isolation window (in Th.) from which precursor is derived. Window is centered at target m/z with a total size of +/- the value entered.\n" +
            "  <defaultMinCharge> (default: 0) and <defaultMaxCharge> (default: 0): in the event that no isotope is found in the isolation window, a range of charges between these two values will be assigned to the spectrum. If both values are left at zero, no charge will be assigned to the spectrum."),
        new("activation", "<precursor_activation_type> [mode=<include|exclude (include)>]",
            "Keeps only spectra whose precursors have the specified activation type. It doesn't affect non-MS spectra, and doesn't " +
            "affect MS1 spectra. Use it to create output files containing only ETD or CID MSn data where both activation modes " +
            "have been interleaved within a given input vendor data file (eg: Thermo's Decision Tree acquisition mode).\n" +
            "   <precursor_activation_type> is any one of: ETD CID SA HCD HECID BIRD ECD IRMPD PD PSD PQD SID or SORI.\n" +
            "   <mode> is optional and must be either \"include\" (the default) or \"exclude\". If \"exclude\" is used, the filter drops spectra that match the activation type instead of keeping them."),
        new("collisionEnergy", "low=<real> high=<real> [mode=<include|exclude (include)>] [acceptNonCID=<true|false (true)] [acceptMissingCE=<true|false (false)]",
            "Includes/excludes MSn spectra with CID collision energy within the specified range [<low>, <high>].\n" +
            "   Non-MS and MS1 spectra are always included. Non-CID MS2s and MS2s with missing CE are optionally included/excluded."),
        new("analyzer", "<analyzer>",
            "This filter keeps only spectra with the indicated mass analyzer type. \n" +
            "  <analyzer> is any one of \"quad\" \"orbi\" \"FT\" \"IT\" or \"TOF\".\n" +
            "Sometimes people use the terms FT and Orbi interchangeably, which is OK " +
            "because there are no hybrid FT+Orbi instruments - so this filter does too.\n"),
        new("analyzerType", "<analyzer>",
            "This is deprecated syntax for filtering by mass analyzer type.\n" +
            "  <analyzer> can be \"FTMS\" or \"ITMS\"."),
        new("polarity", "<polarity>",
            "Keeps only spectra with scan of the selected <polarity>.\n" +
            "   <polarity> is any one of \"positive\" \"negative\" \"+\" or \"-\"."),
        new("diaUmpire", "params=<filepath to DiaUmpire .params file>",
            "Separates DIA spectra into pseudo-DDA spectra using the DIA Umpire algorithm."),
    };

    /// <summary>Help for each registered filter that has any, in the order msconvert lists them.</summary>
    public static IEnumerable<FilterUsage> FilterUsages =>
        s_usage.Where(u => s_builders.ContainsKey(NormalizeName(u.Name)));

    /// <summary>
    /// The "Spectrum List Filters" section of msconvert's usage. Port of cpp's
    /// <c>SpectrumListFactory::usage(bool, const char*, int)</c>.
    /// </summary>
    /// <param name="detailedHelp">Include each filter's description and the int_set explanation.</param>
    /// <param name="moreHelpPrompt">Line shown under the heading when <paramref name="detailedHelp"/> is false.</param>
    /// <param name="maxLineLength">Wrap width; zero or less means no wrapping.</param>
    public static string Usage(bool detailedHelp, string? moreHelpPrompt = null, int maxLineLength = 0) =>
        FilterUsageFormatter.Format("Spectrum List Filters",
            "Note: Filters are applied sequentially in the order that you list them, and the sequence order " +
            "can make a large difference in your output.  In particular, the peakPicking filter must be first " +
            "in line if you wish to use the vendor-supplied centroiding algorithms since these use the vendor " +
            "DLLs, which only operate on raw untransformed data.",
            FilterUsages, detailedHelp, moreHelpPrompt, maxLineLength);

    /// <summary>
    /// Detailed help for one filter, or the list of filter names when <paramref name="filterName"/>
    /// is not one of them. Port of cpp's <c>SpectrumListFactory::usage(const std::string&amp;)</c>.
    /// </summary>
    public static string Usage(string filterName) => FilterUsageFormatter.FormatSingle(FilterUsages, filterName);
}
