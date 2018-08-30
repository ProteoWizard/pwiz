using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CsvHelper;
// ReSharper disable LocalizableElement

namespace TFExportTool
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum InternalStandardType { none, light, heavy }
        private static bool _isAutoFill;
        private static bool _isAutoFillAll;
        private static bool _isAutoFillConfirming;
        private static InternalStandardType _internalStandardType;
        private static string _rtFile;
        private static string _intensityFile;
        private static bool _useAvgRt;
        private static bool _useAvgIntensity;
        private static int _rtWindow;

        private static readonly string TARGET_PEAK = "TargetPeak";
        private static readonly string CONFIRMING = "Confirming";
        private static readonly string FRAGMENT = "Fragment";

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(@"Error opening report file from Skyline, try re-installing TFExport.");
                return;
            }
            var reportFilePath = args[0];
            var peptideTransitions = GetTransitions(reportFilePath);
            var filePaths = new List<string>();
            foreach (var transitions in peptideTransitions.Values)
            {
                foreach (var t in transitions)
                {
                    if (!filePaths.Contains(t.FileName))
                        filePaths.Add(t.FileName);
                }
            }
            filePaths.Sort();
            using (var tf = new TFExportDlg(filePaths))
            {
                if (tf.ShowDialog() != DialogResult.OK)
                    return;
                _isAutoFill = tf.IsAutoFill();  // if false don't auto fill anything
                _isAutoFillAll = tf.IsAutoFillAll(); // auto fill all as 'TargetPeak'
                _isAutoFillConfirming = tf.IsAutoFillConfirming();  // auto fill one as 'TargetPeak' and rest as 'Confirming' or 'Fragment'
                _internalStandardType = (InternalStandardType) tf.GetStandardType();
                _rtFile = tf.GetSelectedRTFile();
                _intensityFile = tf.GetSelectedIntensityFile();
                _rtWindow = tf.GetRtWindow();
                if (_rtFile == null)
                {
                    _useAvgRt = true;
                    _rtFile = filePaths[0];
                }
                if (_intensityFile == null)
                    _useAvgIntensity = true;
                // Strip peptideTransitions to either file or first one
                var updatedPeptideTransitions = new Dictionary<string, List<TransitionRecord>>();
                foreach (var k in peptideTransitions.Keys)
                {
                    var values = peptideTransitions[k];
                    var transitionRecords = new List<TransitionRecord>();
                    foreach (var v in values)
                    {
                        if (v.FileName.Equals(_rtFile))
                            transitionRecords.Add(v);
                    }
                    
                    var fileAreas = new Dictionary<string, List<double>>();
                    if (_useAvgIntensity) // if average intensity then calculate values
                    {
                        foreach (var v in values)
                        {
                            var key = v.ModifiedSequence + "," + v.PrecursorMz + "," + v.FragmentIon + "," +
                                      v.IsotopeLabelType + "," + v.ProductMz;
                            if (!fileAreas.ContainsKey(key))
                                fileAreas.Add(key, new List<double>());
                            fileAreas[key].Add(v.Area);
                        }
                    } // if use intensity from specific file
                    else
                    {
                        foreach (var v in values)
                        {
                            if(!v.FileName.Equals(_intensityFile))
                                continue;
                            var key = v.ModifiedSequence + "," + v.PrecursorMz + "," + v.FragmentIon + "," +
                                      v.IsotopeLabelType + "," + v.ProductMz;
                            if (!fileAreas.ContainsKey(key))
                                fileAreas.Add(key, new List<double>());
                            fileAreas[key].Add(v.Area);
                        }
                    }
                    // even if area from a single file is used will still funnel through this averageing code 
                    // but instead will just have an int array with one value so the average will be that value(value in selected file)
                    var avgAreas = new Dictionary<string, double>();
                    foreach (var fileArea in fileAreas)
                    {
                        var avg = fileArea.Value.Average();
                        avgAreas.Add(fileArea.Key, avg);
                    }
                    foreach (var avgArea in avgAreas)
                    {
                        var keyInfo = avgArea.Key.Split(',');
                        var seq = keyInfo[0];
                        var precursorMz = keyInfo[1];
                        var fragmentIon = keyInfo[2];
                        var label = keyInfo[3];
                        var prodMz = keyInfo[4];
                        foreach (var transitionRecord in transitionRecords)
                        {
                            if (transitionRecord.ModifiedSequence.Equals(seq) &&
                                transitionRecord.PrecursorMz.ToString(CultureInfo.InvariantCulture).Equals(precursorMz) &&
                                transitionRecord.FragmentIon.Equals(fragmentIon) &&
                                transitionRecord.IsotopeLabelType.Equals(label) &&
                                transitionRecord.ProductMz.ToString(CultureInfo.InvariantCulture).Equals(prodMz))
                                transitionRecord.Area = avgArea.Value;
                        }
                    }
                    updatedPeptideTransitions.Add(k, transitionRecords); // add the transitions for this peptide
                    var standards = new List<TransitionRecord>();
                    var nonStandards = new List<TransitionRecord>();
                    foreach (var v in updatedPeptideTransitions[k])
                    {
                        if (v.IsotopeLabelType.Equals(_internalStandardType.ToString()))
                        {
                            standards.Add(v);
                        }
                        else nonStandards.Add(v);
                    }
                    // make non-standards sort same as standards by giving them the same area 
                    // area will not be seen by user but is only used to sort in this case 
                    foreach (var v in nonStandards)
                    {
                        if (!v.IsotopeLabelType.Equals(_internalStandardType.ToString()))
                        {
                            foreach (var transitionRecord in standards)
                            {
                                if (transitionRecord.PeptideModifiedSequence.Equals(v.PeptideModifiedSequence) &&
                                    transitionRecord.ProductCharge == v.ProductCharge &&
                                    transitionRecord.PrecursorCharge == v.PrecursorCharge &&
                                    transitionRecord.FragmentIon.Equals(v.FragmentIon))
                                {
                                    v.Area = transitionRecord.Area;
                                }
                            }
                        }
                    }
                    var all = standards.Concat(nonStandards).ToList();
                    // Sort final list of transitions by peptide, then label type, then peak area
                    all.Sort((x, y) =>
                    {
                        if(x.IsotopeLabelType == y.IsotopeLabelType)
                            return y.Area.CompareTo(x.Area); 
                        if (_internalStandardType.ToString().Equals(x.IsotopeLabelType))
                            return -1;
                        if (_internalStandardType.ToString().Equals(y.IsotopeLabelType))
                            return 1;
                        return y.Area.CompareTo(x.Area);
                    });
                    updatedPeptideTransitions[k] = all;
                }
                // EXPORT
                using (var saveFileDialog = new SaveFileDialog
                {
                    FileName = @"TFExport.csv",
                    Filter = @"csv files (*.csv)|*.csv"
                })
                {
                    if (saveFileDialog.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }
                    var saveFileLocation = saveFileDialog.FileName;
                    var res = WriteToCsv(saveFileLocation, updatedPeptideTransitions);
                    if (string.IsNullOrEmpty(res))
                    {
                        Console.WriteLine("File saved to {0}.", saveFileLocation);
                    }
                    else // if not success
                    {
                        Console.WriteLine("Error: {0}", res);
                    }
                }
            }
           
        }
        private static string ValidateTransitions(Dictionary<String, List<TransitionRecord>> peptideTransitions)
        {
            return string.Empty; // TODO pointless
            foreach (var transitions in peptideTransitions.Values)
            {
                foreach (TransitionRecord transition in transitions)
                {
                    if (string.IsNullOrEmpty(transition.TFExport_WorkFlow))
                        return string.Format("Missing Annotation 'TFExport_WorkFlow' for a precursor in {0} - {1}",
                            transition.ProteinName, transition.PeptideModifiedSequence);
                    if(transition.ProductCharge <= 0)
                        return string.Format("Product charge is less less than or equal to 0 for a precursor in {0} - {1}",
                           transition.ProteinName, transition.PeptideModifiedSequence);
                }
            }
            return string.Empty;
        }

        // Auto poulate transitions based on the users settings
        private static void AutoFillTransitions(Dictionary<String, List<TransitionRecord>> records)
        {
            if(!_isAutoFill) // Do nothing if is not autofill
                return;
            if (_isAutoFillAll) {
                foreach (var transitions in records.Values)
                {
                    foreach (TransitionRecord t in transitions)
                    {
                        t.TFExport_WorkFlow = TARGET_PEAK;
                    }
                }
            } else {
                foreach (var transitions in records.Values)
                {
                    var standards = new Dictionary<string, List<TransitionRecord>>();
                    foreach (TransitionRecord t in transitions)
                    {
                        if (!standards.ContainsKey(t.IsotopeLabelType))
                            standards[t.IsotopeLabelType] = new List<TransitionRecord>();
                        standards[t.IsotopeLabelType].Add(t);
                    }
                    foreach (var key in standards.Keys)
                    {
                        var hasTargetPeak = standards[key].Any(p => p.TFExport_WorkFlow.Equals(TARGET_PEAK));
                        if (hasTargetPeak) // if user defined target peak populate rest with confirming or fragment
                        {
                            for (var i = 0; i < standards[key].Count; i++)
                            {
                                var currentWorkFlow = standards[key][i].TFExport_WorkFlow;
                                if (!currentWorkFlow.Equals(TARGET_PEAK) && currentWorkFlow.Equals(string.Empty))
                                    standards[key][i].TFExport_WorkFlow = _isAutoFillConfirming ? CONFIRMING : FRAGMENT;
                            }
                        } else {
                            for (var i = 0; i < standards[key].Count; i++)
                            {
                                if (i == 0)
                                    standards[key][0].TFExport_WorkFlow = TARGET_PEAK;
                                else
                                    standards[key][i].TFExport_WorkFlow = _isAutoFillConfirming ? CONFIRMING : FRAGMENT;
                            }
                        }
                        
                    }
                }
            }
        }

        private static Dictionary<string, List<TransitionRecord>> GetTransitions(String reportLocation)
        {
            var csv = new CsvReader(File.OpenText(reportLocation));
            // loops through each row as csv.CurrentRecord
            var peptideTransitions = new Dictionary<String, List<TransitionRecord>>();
            // creates dictionary from Skyline report of peptides -> precursors
            while (csv.Read())
            {
                try
                {
                    var proteinName = csv.GetField<string>(0);
                    var peptideModifiedSequence = csv.GetField<string>(1);
                    var isotopeLabelType = csv.GetField<string>(2);
                    var modifiedSequence = csv.GetField<string>(3);
                    // ReSharper disable InconsistentNaming
                    var TFExport_IntegrationStrategy = csv.GetField<string>(4);
                    var TFExport_WindowType = csv.GetField<string>(5);
                    var TFExport_WorkFlow = csv.GetField<string>(6);
                    // ReSharper restore InconsistentNaming
                    var precursorMz = csv.GetField<double>(7);
                    var productMz = csv.GetField<double>(8);
                    var productCharge = csv.GetField<int>(9);
                    var bestRetentionTime = csv.GetField<string>(10);
                    var fragmentIon = csv.GetField<string>(11);
                    var fragmentIonType = csv.GetField<string>(12);
                    var standardType = csv.GetField<string>(13);
                    var collisionEnergy = csv.GetField<string>(14);
                    var retentionTime = csv.GetField<string>(15);
                    var fileName = csv.GetField<string>(16);
                    var area = csv.GetField<string>(17);
                    var precursorCharge = csv.GetField<int>(18);
                    double areaint;
                    double.TryParse(area, out areaint);

                    var record = new TransitionRecord(proteinName, peptideModifiedSequence, isotopeLabelType,
                        modifiedSequence,
                        TFExport_IntegrationStrategy, TFExport_WindowType, TFExport_WorkFlow, precursorMz, productMz,
                        productCharge,
                        bestRetentionTime, fragmentIon, fragmentIonType, standardType,
                        collisionEnergy, fileName, retentionTime, areaint, precursorCharge);
                    var key = record.ProteinName + "-" +record.PeptideModifiedSequence;
                    if (!peptideTransitions.ContainsKey(key))
                        peptideTransitions.Add(key, new List<TransitionRecord>());
                    peptideTransitions[key].Add(record);
                }
                catch(Exception e)
                {
                    Console.WriteLine("There was an issue processing the skyline report.  " +
                                    "Ensure that the tool is using the TFExport report in Skyline." + e.StackTrace);
                }
            }
            return peptideTransitions;
        }
        private static string WriteToCsv(string saveLocation, Dictionary<string, List<TransitionRecord>> peptideTransitions )
        {
            int startRow = 6;
            int rowCount = 0;

            foreach (var peptide in peptideTransitions.Values)
                rowCount += peptide.Count;

            // Auto Fill Transition Wofkflow information
            AutoFillTransitions(peptideTransitions);
            // Validate rows
            // Ensure necessary data exists to export a TF formatted file
            var res = ValidateTransitions(peptideTransitions);
            if (!string.IsNullOrEmpty(res))
                return res; // if error validating return here, res will be logged to Immediate Window in Skyline

            var emptyLine = ",,,,,,,,,,,,,,,,,,,,,,,,";
            List<string> csvLines = new List<string>(); // each object is a line that will be outputted when the result csv file is saved
            // Header row/version info
            csvLines.Add("TraceFinder Compound Database Mass List Export,,,,,,,,,,,,,,,,,,,,,,,,"); // line1
            csvLines.Add(emptyLine); // line2
            csvLines.Add("Schema Version,Peak Header Line Number,Peak Last Row Line Number,Compound Header Line Number,,,,,,,,,,,,,,,,,,,,,");// line3
            csvLines.Add(string.Format("1,{0},{1},{2},,,,,,,,,,,,,,,,,,,,,", startRow, rowCount + startRow, rowCount + startRow + 2));// line4
            csvLines.Add(",,,,,,,,,,,,,,,,,,,,,,,,"); // line5
            // First section column headers
            csvLines.Add("Protein Name,Compound Name,Workflow,Associated Target Peak,MS Order,Precursor m/z,Product m/z,m/z,Height Threshold,Area Threshold,Collision Energy,Modification,Lens,Energy Ramp,Ion Coelution,Ratio Window,Target Ratio,Window Type,Ion Type,PeakPolarity,Adduct,Charge State,Retention Time,Retention Time Window,Integration Strategy"); // line6 - column headers
            // First section data rows

            Dictionary<string, List<TransitionRecord>> peptideAnalytes = new Dictionary<string, List<TransitionRecord>>();
            foreach (var k in peptideTransitions.Keys)
            {
                var transitions = peptideTransitions[k];
                foreach (var transitionRecord in transitions)
                {
                    var newKey = k + transitionRecord.IsotopeLabelType;
                    if(!peptideAnalytes.ContainsKey(newKey))
                        peptideAnalytes.Add(newKey, new List<TransitionRecord>());
                    peptideAnalytes[newKey].Add(transitionRecord);
                }
            }
            foreach (var transitions in peptideAnalytes.Values)
            {
                // find transition with highest product mz, this will be the target peak
                TransitionRecord highestTransitionMz = null;
                var highestTransitionIndex = 0;
                for (var i = 0; i < transitions.Count; i++)
                {
                    TransitionRecord transition = transitions[i];

                    if (highestTransitionMz == null && transition.TFExport_WorkFlow.Equals(TARGET_PEAK))
                    {
                        highestTransitionMz = transition;
                        highestTransitionIndex = i+1; // index starts at 1 instead of 0
                    }
                    if (highestTransitionMz != null && transition.ProductMz > highestTransitionMz.ProductMz &&
                        transition.TFExport_WorkFlow.Equals(TARGET_PEAK))
                    {
                        highestTransitionIndex = i + 1; // index starts at 1 instead of 0
                        highestTransitionMz = transition;
                    }
                }
                foreach (TransitionRecord transition in transitions)
                {
                    var proteinName = transition.ProteinName;
                    var compoundName = transition.PeptideModifiedSequence;
                    if (transition.IsotopeLabelType.Equals("heavy")) {
                        if (_internalStandardType == InternalStandardType.heavy)
                            compoundName += "[heavyIS]";
                        else if (_internalStandardType == InternalStandardType.none)
                            compoundName += "[heavy]";
                    } else if (transition.IsotopeLabelType.Equals("light")) {
                             if (_internalStandardType == InternalStandardType.light)
                                compoundName += "[lightIS]";
                             else if (_internalStandardType == InternalStandardType.none)
                                 compoundName += "[light]";
                    }

                    
                    var workflow = transition.TFExport_WorkFlow;
                    var associatedTargetPeak = string.Empty;
                    var msOrder = "ms2";
                    if (transition.FragmentIonType.Equals("precursor"))
                        msOrder = "ms1";
                    var precursorMz = transition.PrecursorMz;
                    var productMz = transition.ProductMz.ToString(CultureInfo.InvariantCulture);
                    if (msOrder.Equals("ms1"))
                        productMz = string.Empty;
                    var mz = transition.PrecursorMz;
                    var heightThreshold = string.Empty;
                    var areaThreshold = string.Empty;
                    var collisionEnergy = transition.CollisionEnergy;
                    var modification = transition.IsotopeLabelType;
                    var lens = 0;
                    var energyRamp = 0;
                    var ionCoelution = string.Empty;
                    var ratioWindow = string.Empty;
                    var targetRatio = string.Empty;
                    var windowType = transition.TFExport_WindowType;
                    var ionType = transition.FragmentIon;

                    var peakPolarity = string.Empty;
                    var adduct = string.Empty;

                    if (transition.TFExport_WorkFlow.Equals(CONFIRMING) || 
                        transition.TFExport_WorkFlow.Equals(FRAGMENT))
                        associatedTargetPeak = highestTransitionIndex.ToString();

                    if (transition.ProductCharge > 0) // should never never be 0 or less
                    {
                        peakPolarity = "Positive";
                        adduct = "M+";
                    }

                    var chargeState = transition.ProductCharge;
                    if (chargeState < 0) // should never happen
                        chargeState = 0;
                    var retentionTime = transition.RetentionTime;
                    if (_useAvgRt)
                        retentionTime = transition.BestRetentionTime;

                    if (retentionTime.Equals("#N/A"))
                        retentionTime = string.Empty;
                    var retentionTimeWindow = _rtWindow;
                    var integrationStrategy = transition.TFExport_IntegrationStrategy;

                    csvLines.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}," +
                                               "{18},{19},{20},{21},{22},{23},{24}",
                        proteinName, compoundName, workflow, associatedTargetPeak, msOrder, precursorMz, productMz, mz, heightThreshold,
                        areaThreshold, collisionEnergy, modification, lens, energyRamp, ionCoelution, ratioWindow, targetRatio, windowType, ionType,
                        peakPolarity, adduct, chargeState, retentionTime, retentionTimeWindow, integrationStrategy));
                }
            }
            csvLines.Add(emptyLine); // add empty line to break for second section
            // Second section column headers
            csvLines.Add("Protein Name,Compound Name,Peptide Sequence,Cas Number,Category,Compound Type,Internal Standard Concentration,ISTD Protein Name,ISTD Compound Name,Ionization Field,Compound Group,,,,,,,,,,,,,,");
            // Second section data rows
            foreach (var transitions in peptideTransitions.Values)
            {
                transitions.Sort((a,b) => a.PrecursorMz.CompareTo(b.PrecursorMz));
                bool heavyUsed = false;
                bool lightUsed = false;
                foreach (TransitionRecord transition in transitions)
                {
                    if (!heavyUsed && transition.IsotopeLabelType.Equals("heavy"))
                    {
                        heavyUsed = true;
                        var proteinName = transition.ProteinName;
                        var compoundName = transition.PeptideModifiedSequence;
                        if (_internalStandardType == InternalStandardType.heavy)
                            compoundName += "[heavyIS]";
                        else
                            compoundName += "[heavy]";
                        var peptideSequence = transition.PeptideModifiedSequence;
                        var casNumber = string.Empty;
                        var category = string.Empty;
                        var compoundType = string.Empty;
                        var internalStandardConcentration = string.Empty;
                        var iStdProteinName = string.Empty;
                        var iStdCompoundName = string.Empty;
                        var ionizationField = "None";
                        var compoundGroup = string.Empty;

                        csvLines.Add(
                            string.Format(CultureInfo.InvariantCulture,
                                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},,,,,,,,,,,,,,", proteinName, compoundName,
                                peptideSequence, casNumber, category, compoundType, internalStandardConcentration, iStdProteinName,
                                iStdCompoundName, ionizationField, compoundGroup));
                    }
                    if (!lightUsed && transition.IsotopeLabelType.Equals("light"))
                    { 
                        lightUsed = true;
                        var proteinName = transition.ProteinName;
                        var compoundName = transition.PeptideModifiedSequence;
                        if (_internalStandardType == InternalStandardType.light)
                            compoundName += "[lightIS]";
                        else
                            compoundName += "[light]";

                        var peptideSequence = transition.PeptideModifiedSequence;
                        var casNumber = string.Empty;
                        var category = string.Empty;
                        var compoundType = string.Empty;
                        var internalStandardConcentration = string.Empty;
                        var iStdProteinName = string.Empty;
                        var iStdCompoundName = string.Empty;
                        var ionizationField = "None";
                        var compoundGroup = string.Empty;

                        csvLines.Add(
                            string.Format(CultureInfo.InvariantCulture,
                                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},,,,,,,,,,,,,,", proteinName, compoundName,
                                peptideSequence, casNumber, category, compoundType, internalStandardConcentration, iStdProteinName,
                                iStdCompoundName, ionizationField, compoundGroup));
                    }
                }
            }


            var success = WriteCsv(csvLines, saveLocation);
            if (success)
                return string.Empty;
            return "There was an error writing to the output file.";
        }

        private static bool WriteCsv(List<string> csvLines, string saveLocation)
        {
            var sb = new StringBuilder();
            foreach (var line in csvLines)
            {
                sb.Append(line + Environment.NewLine);
            }
            try
            {
                using (StreamWriter outfile =
                    new StreamWriter(
                        saveLocation)
                    )
                {
                    outfile.Write(sb.ToString());
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
        // one TransitionRecord is one row of the Skyline output so it must stay consistent with the report
        class TransitionRecord
        {
            public String ProteinName { get; private set; }
            public String PeptideModifiedSequence { get; private set; }
            public String IsotopeLabelType { get; private set; }
            public String ModifiedSequence { get; private set; }
            // ReSharper disable InconsistentNaming
            public string TFExport_IntegrationStrategy { get; set; }
            public String TFExport_WindowType { get; set; }
            public String TFExport_WorkFlow { get; set; }
            // ReSharper restore InconsistentNaming
            public double PrecursorMz { get; private set; }
            public double ProductMz { get; private set; }
            public int ProductCharge { get; private set; }
            public String BestRetentionTime { get; private set; }
            public String FragmentIon { get; private set; }
            public String FragmentIonType { get; private set; }
            public String StandardType { get; private set; }
            public String CollisionEnergy { get; private set; }
            public String FileName { get; private set; }
            public String RetentionTime { get; private set; }
            public double Area { get; set; }
            public int PrecursorCharge { get; set; }

            public TransitionRecord(string proteinName, string peptideModifiedSequence, string isotopeLabelType,
                string modifiedSequence, string tfExportIntegrationStrategy, string tfExportWindowType,
                string tfExportWorkFlow, double precursorMz, double productMz, int productCharge,
                string bestRetentionTime, string fragmentIon, string fragmentIonType, string standardType,
                string collisionEnergy, string fileName, string retentionTime, double area, int precursorCharge)
            {
                TFExport_IntegrationStrategy = tfExportIntegrationStrategy;
                ProteinName = proteinName;
                PeptideModifiedSequence = peptideModifiedSequence;
                IsotopeLabelType = isotopeLabelType;
                ModifiedSequence = modifiedSequence;
                TFExport_WindowType = tfExportWindowType;
                TFExport_WorkFlow = tfExportWorkFlow;
                PrecursorMz = precursorMz;
                ProductMz = productMz;
                ProductCharge = productCharge;
                BestRetentionTime = bestRetentionTime;
                FragmentIon = fragmentIon;
                StandardType = standardType;
                CollisionEnergy = collisionEnergy;
                FragmentIonType = fragmentIonType;
                FileName = fileName;
                RetentionTime = retentionTime;
                Area = area;
                PrecursorCharge = precursorCharge;
            }
        }
    }
}
