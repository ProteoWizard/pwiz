
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Surendra Dasari
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Security.AccessControl;

using IDPicker.DataModel;

using msdata = pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using proteome = pwiz.CLI.proteome;
using chemistry = pwiz.CLI.chemistry;
using combs = Facet.Combinatorics;
using phosphoRS = IMP.PhosphoRS;

namespace IDPicker.Forms
{
    public partial class PTMAttestationForm : DockableForm, phosphoRS.ThreadManagement.IDataConection
    {
        public PTMAttestationForm(IDPickerForm owner)
        {
            InitializeComponent();

            this.owner = owner;

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "PTM Attestation";
            Icon = Properties.Resources.BlankIcon;
        }

        private IDPickerForm owner;
        private NHibernate.ISession session;
        private DataFilter basicDataFilter;
        private static BackgroundWorker _bgWorkerAttestation;
        private bool _bgWorkerCancelled;
        private CancellationTokenSource cancelAttestation;

        private delegate void reportStatusDelegate(string statusText);
        private void setStatus(string statusText)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new reportStatusDelegate(setStatus), statusText);
                return;
            }
            tbStatus.AppendText(statusText);
        }

        private delegate void reportProgressDelegate(int progress, string progressText);
        private void setProgress(int progress, string progressText)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new reportProgressDelegate(setProgress), progress, progressText);
                return;
            }
            if (progress == -1)
            {
                progressBar.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Value = progress;
            }
            lblStatusToolStrip.Text = progressText;
        }

        private string reportSecondsElapsed(double seconds)
        {
            return string.Format("{0:0.0} seconds elapsed\r\n", seconds);
        }

        private List<PhosphoPeptideAttestationRow> getPhosphoPSMs(ref reportStatusDelegate reportStatus, ref reportProgressDelegate reportProgress)
        {
            DateTime startTime = DateTime.Now;
            string database = session.Connection.GetDataSource();
            reportStatus("Querying spectra...");
            reportProgress(1, "(1/4) Querying spectra...");
            // Locate phosphorylated PSMs
            /* SELECT psm.Id AS PSMId, spectrum.Id AS SpectrumID, source.Name, NativeID, PrecursorMZ, Charge,   
                            GROUP_CONCAT(DISTINCT mod.MonoMassDelta || '@' || pepMod.Offset) AS Modifications,
                            IFNULL(SUBSTR(proteinData.Sequence, pepInst.Offset+1, pepInst.Length), pep.DecoySequence) As PeptideSequence,
                            (CASE WHEN SUM(DISTINCT CASE WHEN protein.IsDecoy=1 THEN 1 ELSE 0 END) + SUM(DISTINCT CASE WHEN protein.IsDecoy=0 THEN 1 ELSE 0 END) = 2 THEN 2 ELSE SUM(DISTINCT CASE WHEN protein.IsDecoy=1 THEN 1 ELSE 0 END) END) AS DecoyState 
                            FROM PeptideSpectrumMatch psm
                            JOIN Spectrum spectrum ON spectrum.Id = psm.Spectrum
                            JOIN SpectrumSource source ON spectrum.Source = source.Id
                            JOIN PeptideInstance pepInst ON pepInst.Peptide = psm.Peptide
                            LEFT JOIN Peptide pep ON pep.Id = pepInst.Peptide
                            LEFT JOIN Protein protein ON protein.Id = pepInst.Protein
                            LEFT JOIN ProteinData proteinData ON proteinData.Id = pepInst.Protein
                            LEFT JOIN PeptideModification pepMod ON PSM.Id = pepMod.PeptideSpectrumMatch
                            LEFT JOIN Modification mod ON pepMod.Modification = mod.Id
                            GROUP BY psm.Id
                            HAVING LENGTH(Modifications)>0 */
            IList<object[]> queryRows;
            lock (session)
                queryRows = session.CreateSQLQuery(@"SELECT psm.Id AS PSMId, spectrum.Id AS SpectrumID, source.Name, NativeID, PrecursorMZ, Charge,   
                                                        GROUP_CONCAT(DISTINCT mod.MonoMassDelta || '@' || pepMod.Offset) AS Modifications,
                                                        IFNULL(SUBSTR(proteinData.Sequence, pepInst.Offset+1, pepInst.Length), pep.DecoySequence) As PeptideSequence,
                                                        (CASE WHEN SUM(DISTINCT CASE WHEN protein.IsDecoy=1 THEN 1 ELSE 0 END) + SUM(DISTINCT CASE WHEN protein.IsDecoy=0 THEN 1 ELSE 0 END) = 2 THEN 2 ELSE SUM(DISTINCT CASE WHEN protein.IsDecoy=1 THEN 1 ELSE 0 END) END) AS DecoyState 
                                                        FROM PeptideSpectrumMatch psm
                                                        JOIN Spectrum spectrum ON spectrum.Id = psm.Spectrum
                                                        JOIN SpectrumSource source ON spectrum.Source = source.Id
                                                        JOIN PeptideInstance pepInst ON pepInst.Peptide = psm.Peptide
                                                        LEFT JOIN Peptide pep ON pep.Id = pepInst.Peptide
                                                        LEFT JOIN Protein protein ON protein.Id = pepInst.Protein
                                                        LEFT JOIN ProteinData proteinData ON proteinData.Id = pepInst.Protein
                                                        LEFT JOIN PeptideModification pepMod ON PSM.Id = pepMod.PeptideSpectrumMatch
                                                        LEFT JOIN Modification mod ON pepMod.Modification = mod.Id
                                                        GROUP BY psm.Id HAVING LENGTH(Modifications)>0"
                                                                   ).List<object[]>();

            var phosphoPSMs = queryRows.Select(o => new PhosphoPeptideAttestationRow(o)).OrderBy(o => o.SourceName).ToList();
            // Delete non-phospho peptides
            List<PhosphoPeptideAttestationRow> deletePSMs = new List<PhosphoPeptideAttestationRow>();
            phosphoPSMs.ForEach(variant => { if (!variant.PhosphoPeptide || variant.DecoyState > 0) deletePSMs.Add(variant); });
            deletePSMs.ForEach(removePSM => { phosphoPSMs.Remove(removePSM); });
            reportStatus(reportSecondsElapsed((DateTime.Now - startTime).TotalSeconds));
            reportStatus("Found " + phosphoPSMs.Count + " phospho-peptides.");
            return phosphoPSMs;
        }

        private string PeptideToString(proteome.Peptide peptide)
        {
            string format = String.Format("[{{0:f{0}}}]", 4);
            StringBuilder sb = new StringBuilder();
            if (peptide.modifications().ContainsKey(proteome.ModificationMap.NTerminus()))
                sb.AppendFormat(format, peptide.modifications()[proteome.ModificationMap.NTerminus()].monoisotopicDeltaMass());
            for (int i = 0; i < peptide.sequence.Length; ++i)
            {
                sb.Append(peptide.sequence[i]);
                if (peptide.modifications().ContainsKey(i))
                {
                    double modMass = peptide.modifications()[i].monoisotopicDeltaMass();
                    sb.AppendFormat(format, modMass);
                }
            }
            if (peptide.modifications().ContainsKey(proteome.ModificationMap.CTerminus()))
                sb.AppendFormat(format, peptide.modifications()[proteome.ModificationMap.CTerminus()].monoisotopicDeltaMass());
            return sb.ToString();
        }

        public class PhosphoRSConfig
        {
            public double fragmentMassTolerance;
            public string scoreNLToo;
            public IMP.PhosphoRS.SpectrumType spectrumType;

            public int phosphorylationSymbol;
            public int maxIsoformCount;
            public int maxPTMCount;
            public int maxPackageSize;

            public PhosphoRSConfig()
            {
                fragmentMassTolerance = 0.5;
                scoreNLToo = "true";
                phosphorylationSymbol = 1;
                maxIsoformCount = 200;
                maxPTMCount = 20;
                maxPackageSize = 3000;
            }

            public void setSpectrumType(string spectrum)
            {
                if(spectrum.CompareTo("CID/CAD")==0)
                    spectrumType = phosphoRS.SpectrumType.CID_CAD;
                else if(spectrum.CompareTo("HCD")==0)
                    spectrumType = phosphoRS.SpectrumType.HCD;
                else if(spectrum.CompareTo("ETD/ECD")==0)
                    spectrumType = phosphoRS.SpectrumType.ECD_ETD;
                else
                    spectrumType = phosphoRS.SpectrumType.None;
            }
        }

        private List<Tuple<phosphoRS.PeptideSpectrumMatch, List<Tuple<int, List<int>>>>> items;

        private void ExecutePhosphoRS(PhosphoRSConfig config)
        {
            // Set up the cancel token.
            cancelAttestation = new CancellationTokenSource();
            // Initialize  time stamps and status reporting variables.
            DateTime startTime = DateTime.Now;
            reportProgressDelegate reportProgress = new reportProgressDelegate(setProgress);
            reportStatusDelegate reportStatus = new reportStatusDelegate(setStatus);
            this.progressBar.ProgressBar.Visible = true;
            this.progressBar.Maximum = 4;
            // Init the variables used by phosphoRS. Clear its internal variables also.
            items = new List<Tuple<phosphoRS.PeptideSpectrumMatch, List<Tuple<int, List<int>>>>>();
            // Get all phospho peptide-spectrum matches.
            List<PhosphoPeptideAttestationRow> phosphoPSMs = this.getPhosphoPSMs(ref reportStatus, ref reportProgress);
            reportProgress(2, "(2/4) Making phosphoRS objects...");
            if (phosphoPSMs.Count == 0)
                return;
            reportStatus("Finding spectra for the peptides...\n");
            // Figure out the unique sources in the data range and get their readers
            Dictionary<string, string> uniqueSources = new Dictionary<string, string>();
            phosphoPSMs.ForEach(variant =>
            {
                string currentSourcePath = IDPickerForm.LocateSpectrumSource(variant.SourceName, session.Connection.GetDataSource());
                if (!uniqueSources.ContainsKey(variant.SourceName))
                    uniqueSources.Add(variant.SourceName, currentSourcePath);
            });
            Dictionary<string, msdata.MSDataFile> rawDataReaders = new Dictionary<string, msdata.MSDataFile>();
            uniqueSources.ToList().ForEach(source =>
            {
                msdata.MSDataFile msd = new pwiz.CLI.msdata.MSDataFile(source.Value);
                rawDataReaders.Add(source.Key, msd);
            });
            reportStatus("Making phosphoRS objects for scoring...\n");
            // Get the raw spectra and convert them for phosphoRS use.
            int variantID = 1;
            phosphoPSMs.ForEach(variant =>
            {
                if (rawDataReaders.ContainsKey(variant.SourceName))
                {
                    if ((++variantID % 500) == 0 && cancelAttestation.IsCancellationRequested)
                    {
                        this.progressBar.ProgressBar.Visible = false;
                        _bgWorkerCancelled = true;
                        reportProgress(-1, "Cancelled.");
                        return;
                    }
                    var rawDataReader = rawDataReaders[variant.SourceName];
                    var spectrumList = rawDataReader.run.spectrumList;
                    var pwizSpectrum = spectrumList.spectrum(spectrumList.find(variant.SpectrumNativeID), true); //may create indexoutofrange error if no spectrum nativeID                   

                    var OriginalMZs = pwizSpectrum.getMZArray().data; //getMZArray().data returns IList<double>
                    var OriginalIntensities = pwizSpectrum.getIntensityArray().data;
                    variant.Peaks = new List<phosphoRS.Peak>();
                    for (int peakIndex = 0; peakIndex < OriginalMZs.Count; ++peakIndex)
                        variant.Peaks.Add(new phosphoRS.Peak(OriginalMZs[peakIndex], OriginalIntensities[peakIndex]));
                }
            });

            // Set the modification to be scored.
            phosphoRS.AminoAcidModification scoredAA = new phosphoRS.AminoAcidModification(config.phosphorylationSymbol.ToString()[0], "Phosphorylation", "Pho", "H3PO4", 79.966331, 97.976896, phosphoRS.AminoAcidSequence.ParseAASequence("STY"));
            Map<int, long> variantID2PSMId = new Map<int, long>();
            Map<int, proteome.Peptide> varintID2Ppetide = new Map<int, proteome.Peptide>();
            // reset variantID to 1
            variantID = 1;
            // Get the raw spectra and convert them for phosphoRS use.
            phosphoPSMs.ForEach(variant =>
              {
                  if ((variantID % 500) == 0 && cancelAttestation.IsCancellationRequested)
                  {
                      this.progressBar.ProgressBar.Visible = false;
                      reportProgress(-1, "Cancelled.");
                      _bgWorkerCancelled = true;
                      return;
                  }
                  if (variant.HasPeaks)
                  {

                      // Get the phosphorylated peptide and add all modifications to the base sequence.
                      proteome.Peptide phosphoPeptide = new proteome.Peptide(variant.UnphosphorylatedSequence, proteome.ModificationParsing.ModificationParsing_Auto, proteome.ModificationDelimiter.ModificationDelimiter_Brackets);
                      proteome.ModificationMap variantPeptideMods = phosphoPeptide.modifications();
                      variant.OriginalPhosphoSites.ToList().ForEach(location => { variantPeptideMods[location].Add(new proteome.Modification("H1P1O3")); });
                      // This modification ID is used to tell phosphoRS how to modify the sequence.
                      int modificationID = config.phosphorylationSymbol + 1;
                      // Build a string representation of all modificaitons in a peptide for phospoRS
                      // "0.00011000000000.0" : 1 is the ID of the modification. All phosphos in a data
                      // set need to have one ID. This ID is used by the PhosphoRS to figure out which
                      // mods need to be scored.
                      StringBuilder ptmRepresentation = new StringBuilder();
                      // Stote all modifications in phosphoRS modificartion objects
                      List<phosphoRS.AminoAcidModification> modifications = new List<phosphoRS.AminoAcidModification>();
                      // Get the n-terminal modifications.
                      if (variantPeptideMods.ContainsKey(proteome.ModificationMap.NTerminus()))
                      {
                          phosphoRS.AminoAcidModification otherMod = new phosphoRS.AminoAcidModification(modificationID.ToString()[0], "unknown", "unk", "none", variantPeptideMods[proteome.ModificationMap.NTerminus()].monoisotopicDeltaMass(), 0.0, null);
                          modifications.Add(otherMod);
                          ptmRepresentation.Append(modificationID.ToString() + ".");
                          ++modificationID;
                      }
                      else
                      {
                          ptmRepresentation.Append("0.");
                      }
                      // Process all other modifications.
                      for (int aaIndex = 0; aaIndex < phosphoPeptide.sequence.Length; ++aaIndex)
                      {
                          // If phosphorylation, use the existing scoredAA variable.
                          if (variantPeptideMods.ContainsKey(aaIndex))
                          {
                              if (variant.OriginalPhosphoSites.Contains(aaIndex))
                              {
                                  modifications.Add(scoredAA);
                                  ptmRepresentation.Append(config.phosphorylationSymbol.ToString()[0]);
                              }
                              else
                              {
                                  // Otherwise, make an "unknown" modification with a separate modification ID.
                                  phosphoRS.AminoAcidModification otherMod = new phosphoRS.AminoAcidModification(modificationID.ToString()[0], "unknown", "unk", "none", variantPeptideMods[aaIndex].monoisotopicDeltaMass(), 0.0, phosphoRS.AminoAcidSequence.ParseAASequence("" + phosphoPeptide.sequence[aaIndex]));
                                  modifications.Add(otherMod);
                                  ptmRepresentation.Append(modificationID.ToString());
                                  ++modificationID;
                              }
                          }
                          else
                          {
                              ptmRepresentation.Append("0");
                          }
                      }
                      // Process any c-terminal modifications.
                      if (variantPeptideMods.ContainsKey(proteome.ModificationMap.CTerminus()))
                      {
                          phosphoRS.AminoAcidModification otherMod = new phosphoRS.AminoAcidModification(modificationID.ToString()[0], "unknown", "unk", "none", variantPeptideMods[proteome.ModificationMap.CTerminus()].monoisotopicDeltaMass(), 0.0, null);
                          modifications.Add(otherMod);
                          ptmRepresentation.Append("." + modificationID.ToString());
                      }
                      else
                      {
                          ptmRepresentation.Append(".0");
                      }

                      // Get the phosphoRS peptide sequence.
                      // Assign spectrum ID, amino acid sequence, list of all modifications, a so-called 'modification position string' (here every digit represents an amino acid within the peptide sequence
                      // '0' indicates not modified, values != '0' indicate the unique identifier of the amino acid's modification the first digit represents the n-terminus the last digit represents the c-terminus)
                      phosphoRS.AminoAcidSequence AAS = phosphoRS.AminoAcidSequence.Create((int)variant.SpectrumId, phosphoPeptide.sequence, modifications, ptmRepresentation.ToString());
                      // Make a phosphoRS peptide-spectrum match.
                      phosphoRS.PeptideSpectrumMatch psm = new phosphoRS.PeptideSpectrumMatch((int)variant.PSMId, config.spectrumType, (int)variant.Charge, variant.PrecursorMZ, variant.Peaks.ToArray(), AAS);
                      // Few things to remember for results storage
                      varintID2Ppetide[variantID] = phosphoPeptide;
                      variantID2PSMId[variantID] = variant.PSMId;
                      // DEBUG
                      //tbStatus.AppendText(PeptideToString(phosphoPeptide) + "," + AAS.ToOneLetterCodeString() + "," + ptmRepresentation.ToString() + "\n");
                      // Init the mod map of original variant for this PSM.
                      List<Tuple<int, List<int>>> id2ModMap = new List<Tuple<int, List<int>>>();
                      id2ModMap.Add(new Tuple<int, List<int>>(variantID++, variant.OriginalPhosphoSites.ToList<int>()));
                      items.Add(new Tuple<phosphoRS.PeptideSpectrumMatch, List<Tuple<int, List<int>>>>(psm, id2ModMap));
                  }
              });
            reportProgress(3, "(3/4) Peforming localization...");
            // Initialize the localization.
            phosphoRS.ThreadManagement.InitializePTMLocalisation(this, config.maxIsoformCount, config.maxPTMCount, config.scoreNLToo, config.fragmentMassTolerance, scoredAA, phosphoPSMs.Count);
            // Start the site localization (takes advantage of multi-threading)
            try
            {
                phosphoRS.ThreadManagement.StartPTMLocalisation(cancelAttestation);
            }
            catch (OperationCanceledException e)
            {
                this.progressBar.ProgressBar.Visible = false;
                _bgWorkerCancelled = true;
                reportProgress(-1, "Cancelled.");
                return;
            }
            // Saftey if the attestation module doesn't throw the exception.
            if (cancelAttestation.IsCancellationRequested)
            {
                this.progressBar.ProgressBar.Visible = false;
                _bgWorkerCancelled = true;
                reportProgress(-1, "Cancelled.");
                return;
            }
            IDictionary<int, double> propMap;
            IDictionary<int, double> peptideScoreMap;
            IDictionary<int, string> sitepropMap;
            // peptide ID to site probabilities map
            sitepropMap = phosphoRS.ThreadManagement.ptmResult.PeptideIdPrsSiteProbabilitiesMap;
            // peptid ID to isoform confidence probability map
            propMap = phosphoRS.ThreadManagement.ptmResult.PeptideIdPrsProbabilityMap;
            // peptide ID to binomial score map
            peptideScoreMap = phosphoRS.ThreadManagement.ptmResult.PeptideIdPrsScoreMap;
            reportProgress(4, "(4/4) Injecting results into the database...");
            // A map of PSMId to localization representation in string format.
            Dictionary<long, string> localizationStrings = new Dictionary<long, string>();
            foreach (var isoform in phosphoRS.ThreadManagement.ptmResult.isoformGroupList)
            {
                //tbStatus.AppendText(isoform.GetSiteProbabilityString() + "\n");
                //tbStatus.AppendText(isoform.peptides.First<phosphoRS.PTMResultClass.Peptide>().id + "," + isoform.peptides.Count + "\n");
                variantID = isoform.peptides.First<phosphoRS.PTMResultClass.Peptide>().id;
                long PSMId = variantID2PSMId[variantID];
                StringBuilder localizedString = new StringBuilder();
                foreach (var pepIds in isoform.siteProbabilities)
                {
                    if (Math.Round(pepIds.Probability * 100, 1) > 0)
                    {
                        if (localizedString.Length > 0)
                            localizedString.Append("; ");
                        char aa = isoform.sequenceString[pepIds.SequencePosition - 1];
                        localizedString.Append("80@" + aa + "[" + pepIds.SequencePosition + "]:" + Math.Round(pepIds.Probability * 100, 1) + "%");
                    }
                }
                tbStatus.AppendText(PSMId + "," + variantID + "," + /*PeptideToString(varintID2Ppetide[variantID]) + "," +*/ localizedString.ToString() + "\n");
            }
            this.progressBar.ProgressBar.Visible = false;
            reportProgress(-1, "Finished.");
        }

        int currentNr = 0;

        // in one package there have to be at most maxSizeOfPackage of spectra
        List<phosphoRS.ThreadManagement.SpectraPackageItem> phosphoRS.ThreadManagement.IDataConection.GetNewDataPackage(int maxSizeOfPackage, out int numberOfSpectraPacked, out int numberOfPeptidesPacked)
        {

            List<phosphoRS.ThreadManagement.SpectraPackageItem> package;

            numberOfSpectraPacked = 0;
            numberOfPeptidesPacked = 0;
            if (currentNr >= items.Count)
                return null;

            package = new List<phosphoRS.ThreadManagement.SpectraPackageItem>();
            for (int i = currentNr; i < items.Count && i - currentNr < maxSizeOfPackage; i++)
                package.Add(new phosphoRS.ThreadManagement.SpectraPackageItem(items[i].Item1, items[i].Item2));

            currentNr += package.Count;
            numberOfSpectraPacked = package.Count;
            numberOfPeptidesPacked = package.Count;

            return package;
        }

        void phosphoRS.ThreadManagement.IDataConection.ShowProgress(int numberOfSpectraProcessed, int numberOfPeptidesProcessed)
        {
            // it is possible to show the progress. This message will be called after each package finished processing.
            // The number of finished spectra and peptides will be send. This is not total. Therefore you have to calculate
            // the totalnumber of processed spectra;
            tbStatus.AppendText("Processed " + numberOfSpectraProcessed + "\n");
        }

        private void ExecuteAttestButton_Click(object sender, EventArgs e)
        {
            btnAttestPTMs.Enabled = false;
            btnCancelAttestaion.Enabled = true;
            
            _bgWorkerAttestation = new BackgroundWorker();
            //_bgWorkerClustering.WorkerReportsProgress = true;
            _bgWorkerAttestation.WorkerSupportsCancellation = true;

            _bgWorkerCancelled = false;

            _bgWorkerAttestation.DoWork += new DoWorkEventHandler(_bgWorkerClustering_DoWork);
            _bgWorkerAttestation.RunWorkerCompleted += new RunWorkerCompletedEventHandler(_bgWorkerClustering_RunWorkerCompleted);
            _bgWorkerAttestation.RunWorkerAsync();
        }

        private void _bgWorkerClustering_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                PhosphoRSConfig config = new PhosphoRSConfig();
                config.setSpectrumType(listBoxDissociationType.SelectedItem.ToString());
                config.fragmentMassTolerance = Double.Parse(FragmentMZToleranceTextBox.Text);

                if (config.spectrumType == phosphoRS.SpectrumType.None)
                {
                    tbStatus.AppendText("Failed to find the fragmentation type of the MS/MS\r\n");
                    _bgWorkerCancelled = true;
                    return;
                }
                ExecutePhosphoRS(config);
            }
            catch (Exception exception)
            {
                e.Result = exception;
            }
        }

        private void _bgWorkerClustering_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception)
                Program.HandleException(e.Result as Exception);

            Text = TabText = "PTM Attestation";
            if (_bgWorkerCancelled)
            {
                tbStatus.AppendText("Cancelled\r\n");
            }
            else
            {
                tbStatus.AppendText("Completed\r\n");
            }

            btnAttestPTMs.Enabled = true;
            btnCancelAttestaion.Enabled = false;
        }

        private void btnCancelAttestaion_Click(object sender, EventArgs e)
        {
            tbStatus.AppendText("Cancelling...\r\n");
            if (_bgWorkerAttestation != null && _bgWorkerAttestation.IsBusy)
            {
                _bgWorkerAttestation.CancelAsync();
                cancelAttestation.Cancel();
            }
        }

        public void ResetForm()
        {
            Text = TabText = "PTM Attestation";
            lblStatusToolStrip.Text = "Ready";
            progressBar.Value = 0;
        }

        public void ResetForm(bool clearBasicFilter)
        {
            if (clearBasicFilter)
                basicDataFilter = null;
            ResetForm();
        }

        ////use basicFilter to process all source files and PSMs in session
        public void SetData(NHibernate.ISession session, DataFilter basicFilter)
        {
            this.session = session;
            basicDataFilter = basicFilter;
            ResetForm();
        }

        // This class stores all the information needed for attesting a single PSM.
        public class PhosphoPeptideAttestationRow
        {
            public long PSMId { get; private set; }
            public long SpectrumId { get; private set; }
            public string SourceName { get; private set; }
            public string SpectrumNativeID { get; private set; }
            public double PrecursorMZ { get; private set; }
            public int Charge { get; private set; }
            public string UnphosphorylatedSequence { get; private set; }
            public int DecoyState { get; private set; }
            public List<int> PossiblePhosphoSites { get; private set; }
            public List<int> OriginalPhosphoSites { get; private set; }
            public List<phosphoRS.Peak> Peaks { get; set; }

            // Properties
            public bool PhosphoPeptide { get { return OriginalPhosphoSites.Count > 0; } }
            public bool HasPeaks { get { return Peaks.Count > 0; } }
            public int NumPhosphorylations { get { return OriginalPhosphoSites.Count; } }

            #region Constructor
            public PhosphoPeptideAttestationRow(object[] queryRow)
            {
                PSMId = (long)queryRow[0];
                SpectrumId = (long)queryRow[1];
                SourceName = (string)queryRow[2];
                SpectrumNativeID = (string)queryRow[3];
                PrecursorMZ = Convert.ToDouble(queryRow[4]);
                Charge = Convert.ToInt32(queryRow[5]);

                // Build the peptide sequence with modifications. Leave the phospho sites out of the string. They
                // will reunited with the string right before the PSM is submitted to phosphoRS. This is necessary 
                // because phosphoRS requires all phospho sites marked with a single numerical representation across all
                // PSMs.
                OriginalPhosphoSites = new List<int>();
                var mods = new Dictionary<int, List<double>>();
                string peptideSequence = (string)queryRow[7];
                if (!String.IsNullOrEmpty((string)queryRow[6]))
                {
                    var offsetMassDeltaPairs = ((string)queryRow[6]).Split(',');
                    foreach (var pair in offsetMassDeltaPairs)
                    {
                        var offsetAndMassDelta = pair.Split('@');
                        int roundedDeltaMass = (int)Math.Round(Convert.ToDouble(offsetAndMassDelta[0]));
                        int offset = Convert.ToInt32(offsetAndMassDelta[1]);
                        if (roundedDeltaMass == 80 && (peptideSequence[offset] == 'S' || peptideSequence[offset] == 'T' || peptideSequence[offset] == 'Y'))
                            OriginalPhosphoSites.Add(offset);
                        else
                        {
                            if (!mods.ContainsKey(offset))
                                mods[offset] = new List<double>();
                            mods[offset].Add(Convert.ToDouble(offsetAndMassDelta[0]));
                        }
                    }
                }

                string format = String.Format("[{{0:f{0}}}]", 4);
                StringBuilder sb = new StringBuilder(peptideSequence);
                foreach (var mod in (from m in mods orderby m.Key descending select m))
                    foreach (var massDelta in mod.Value)
                        if (mod.Key == int.MinValue)
                            sb.Insert(0, String.Format(format, massDelta));
                        else if (mod.Key == int.MaxValue || mod.Key >= sb.Length)
                            sb.AppendFormat(format, massDelta);
                        else
                            sb.Insert(mod.Key + 1, String.Format(format, massDelta));
                UnphosphorylatedSequence = sb.ToString();
                DecoyState = Convert.ToInt16(queryRow[8]);

                // Determine the location of phosphorylation sites
                PossiblePhosphoSites = new List<int>();
                for (int residueIndex = 0; residueIndex < peptideSequence.Length; ++residueIndex)
                    if (peptideSequence[residueIndex] == 'S' || peptideSequence[residueIndex] == 'T' || peptideSequence[residueIndex] == 'Y')
                        PossiblePhosphoSites.Add(residueIndex);
            }
            #endregion
        }

        private void PTMAttestationForm_Load(object sender, EventArgs e)
        {
            this.listBoxDissociationType.SelectedIndex = 0;
        }
    }
}
