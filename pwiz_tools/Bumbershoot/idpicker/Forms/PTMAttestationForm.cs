//
// $Id$
//
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
// Copyright 2015 Vanderbilt University
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
using NHibernate.Linq;

using msdata = pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using pwiz.CLI.cv;
using proteome = pwiz.CLI.proteome;
using chemistry = pwiz.CLI.chemistry;
using phosphoRS = IMP.PhosphoRS;
using System.Collections.Concurrent;

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
        private int currentNr; // used for PhosphoRS's batch management

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

        private IDictionary<long, PhosphoPeptideAttestationRow> getPhosphoPSMs(long sourceId)
        {
            string database = session.Connection.GetDataSource();

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
                                                        GROUP_CONCAT(DISTINCT pepMod.Id || ':' || mod.MonoMassDelta || ':' || pepMod.Offset) AS Modifications,
                                                        IFNULL(SUBSTR(proteinData.Sequence, pepInst.Offset+1, pepInst.Length), pep.DecoySequence) As PeptideSequence,
                                                        (CASE WHEN SUM(DISTINCT CASE WHEN protein.IsDecoy=1 THEN 1 ELSE 0 END) + SUM(DISTINCT CASE WHEN protein.IsDecoy=0 THEN 1 ELSE 0 END) = 2 THEN 2 ELSE SUM(DISTINCT CASE WHEN protein.IsDecoy=1 THEN 1 ELSE 0 END) END) AS DecoyState 
                                                        FROM PeptideSpectrumMatch psm
                                                        JOIN Spectrum spectrum ON spectrum.Id = psm.Spectrum
                                                        JOIN SpectrumSource source ON spectrum.Source = source.Id
                                                        JOIN PeptideInstance pepInst ON pepInst.Peptide = psm.Peptide
                                                        LEFT JOIN Peptide pep ON pep.Id = pepInst.Peptide
                                                        LEFT JOIN Protein protein ON protein.Id = pepInst.Protein
                                                        LEFT JOIN ProteinData proteinData ON proteinData.Id = pepInst.Protein
                                                        JOIN PeptideModification pepMod ON PSM.Id = pepMod.PeptideSpectrumMatch
                                                        JOIN Modification mod ON pepMod.Modification = mod.Id
                                                        WHERE source.Id = ?
                                                        GROUP BY psm.Spectrum, psm.Peptide
                                                        HAVING DecoyState=0"
                                                                   ).SetInt64(0, sourceId).List<object[]>();

            return queryRows.Select(o => new PhosphoPeptideAttestationRow(o)).Where(o => o.NumPhosphorylations > 0 && o.PossiblePhosphoSites.Count > 1).OrderBy(o => o.SourceName).ToDictionary(o => o.PSMId);
        }

        private string PeptideToString(proteome.Peptide peptide, IList<phosphoRS.PTMSiteProbability> localizationProbabilities, PhosphoRSConfig config)
        {
            var probabilityMap = localizationProbabilities.ToDictionary(o => o.SequencePosition, o => o.Probability);

            string format = String.Format("[{{0:f{0}}}]", 0);
            StringBuilder sb = new StringBuilder();
            if (peptide.modifications().ContainsKey(proteome.ModificationMap.NTerminus()))
                sb.AppendFormat(format, peptide.modifications()[proteome.ModificationMap.NTerminus()].monoisotopicDeltaMass());
            for (int i = 0; i < peptide.sequence.Length; ++i)
            {
                sb.Append(peptide.sequence[i]);
                if (probabilityMap.ContainsKey(i + 1))
                {
                    if (probabilityMap[i + 1] > 0)
                        sb.AppendFormat("[{0:f0}({1:f0}%)]", config.scoredAA.MassDelta, probabilityMap[i + 1] * 100);
                    //else
                    //    sb.AppendFormat("({0:f0})", config.scoredAA.MassDelta, probabilityMap[i + 1]);
                }
                else if (peptide.modifications().ContainsKey(i))
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
            public IMP.PhosphoRS.AminoAcidModification scoredAA;
            public proteome.Modification pwizMod;

            public int phosphorylationSymbol;
            public int maxIsoformCount;
            public int maxPTMCount;
            public int maxPackageSize;

            public PhosphoRSConfig()
            {
                fragmentMassTolerance = 0.5;
                scoreNLToo = "true";
                phosphorylationSymbol = 1;
                maxIsoformCount = 2000;
                maxPTMCount = 20;
                maxPackageSize = 3000;

                scoredAA = new phosphoRS.AminoAcidModification(phosphorylationSymbol.ToString()[0], "Phosphorylation", "Pho", "H3PO4", 79.966331, 97.976896, phosphoRS.AminoAcidSequence.ParseAASequence("STY"));
                pwizMod = new proteome.Modification("H1P1O3");
            }

            public void setSpectrumType(string spectrum)
            {
                if (spectrum == "Auto")
                    spectrumType = phosphoRS.SpectrumType.None;
                else if (spectrum == "Trap CID")
                    spectrumType = phosphoRS.SpectrumType.CID_CAD;
                else if (spectrum == "Beam CID (HCD)")
                    spectrumType = phosphoRS.SpectrumType.HCD;
                else if (spectrum == "ETD/ECD")
                    spectrumType = phosphoRS.SpectrumType.ECD_ETD;
                else
                    throw new ArgumentException("invalid value \"" + spectrum + "\" for spectrum argument");
            }
        }

        private List<System.Tuple<phosphoRS.PeptideSpectrumMatch, List<System.Tuple<int, List<int>>>>> items;

        private phosphoRS.PeptideSpectrumMatch getPhosphoRS_PSM(PhosphoRSConfig config, PhosphoPeptideAttestationRow variant)
        {
            // Get the phosphorylated peptide and add all modifications to the base sequence.
            proteome.Peptide phosphoPeptide = new proteome.Peptide(variant.UnphosphorylatedSequence, proteome.ModificationParsing.ModificationParsing_Auto, proteome.ModificationDelimiter.ModificationDelimiter_Brackets);
            proteome.ModificationMap variantPeptideMods = phosphoPeptide.modifications();
            variant.OriginalPhosphoSites.Keys.ToList().ForEach(location => { variantPeptideMods[location].Add(config.pwizMod); });

            // This modification ID is used to tell phosphoRS how to modify the sequence.
            int modificationID = config.phosphorylationSymbol + 1;

            // Build a string representation of all modificaitons in a peptide for phospoRS
            // "0.00011000000000.0" : 1 is the ID of the modification. All phosphos in a data
            // set need to have one ID. This ID is used by the PhosphoRS to figure out which
            // mods need to be scored.
            var ptmRepresentation = new StringBuilder();

            // Store all modifications in phosphoRS modification objects
            var modifications = new List<phosphoRS.AminoAcidModification>();

            // Get the n-terminal modifications.
            if (variantPeptideMods.ContainsKey(proteome.ModificationMap.NTerminus()))
            {
                phosphoRS.AminoAcidModification otherMod = new phosphoRS.AminoAcidModification('2', "unknown", "unk", "none", variantPeptideMods[proteome.ModificationMap.NTerminus()].monoisotopicDeltaMass(), 0.0, null);
                modifications.Add(otherMod);
                ptmRepresentation.Append(modificationID.ToString() + ".");
                //++modificationID;
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
                    if (variant.OriginalPhosphoSites.Keys.Contains(aaIndex))
                    {
                        modifications.Add(config.scoredAA);
                        ptmRepresentation.Append(config.phosphorylationSymbol.ToString()[0]);
                    }
                    else
                    {
                        // Otherwise, make an "unknown" modification with a separate modification ID.
                        var otherMod = new phosphoRS.AminoAcidModification(modificationID.ToString()[0], "unknown", "unk", "none", variantPeptideMods[aaIndex].monoisotopicDeltaMass(), 0.0, phosphoRS.AminoAcidSequence.ParseAASequence("" + phosphoPeptide.sequence[aaIndex]));
                        modifications.Add(otherMod);
                        ptmRepresentation.Append(modificationID.ToString());
                        //++modificationID;
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
                var otherMod = new phosphoRS.AminoAcidModification(modificationID.ToString()[0], "unknown", "unk", "none", variantPeptideMods[proteome.ModificationMap.CTerminus()].monoisotopicDeltaMass(), 0.0, null);
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
            var AAS = phosphoRS.AminoAcidSequence.Create((int)variant.SpectrumId, phosphoPeptide.sequence, modifications, ptmRepresentation.ToString());
            // Make a phosphoRS peptide-spectrum match.
            return new phosphoRS.PeptideSpectrumMatch((int)variant.PSMId, variant.SpectrumType, variant.Charge, variant.PrecursorMZ, variant.Peaks, AAS);
        }

        Map<CVID, phosphoRS.SpectrumType> spectrumTypeByDissociationMethod = new Map<CVID, phosphoRS.SpectrumType>
        {
            {CVID.MS_collision_induced_dissociation, phosphoRS.SpectrumType.CID_CAD},
            {CVID.MS_beam_type_collision_induced_dissociation, phosphoRS.SpectrumType.HCD}, // HCD
            {CVID.MS_trap_type_collision_induced_dissociation, phosphoRS.SpectrumType.CID_CAD},
            {CVID.MS_higher_energy_beam_type_collision_induced_dissociation, phosphoRS.SpectrumType.HCD}, // TOF-TOF
            {CVID.MS_electron_transfer_dissociation, phosphoRS.SpectrumType.ECD_ETD},
            {CVID.MS_electron_capture_dissociation, phosphoRS.SpectrumType.ECD_ETD},
        };

        // the most specific analyzer types should be listed first, i.e. a special type of TOF or ion trap
        Map<CVID, double> mzToleranceByAnalyzer = new Map<CVID, double>
        {
            {CVID.MS_ion_trap, 0.5},
            {CVID.MS_quadrupole, 0.5},
            {CVID.MS_FT_ICR, 0.02},
            {CVID.MS_orbitrap, 0.05},
            {CVID.MS_TOF, 0.1},
        };

        private phosphoRS.PTMResultClass RunOnSource(string sourceFilepath, int currentSource, int totalSources, PhosphoRSConfig config, IDictionary<long, PhosphoPeptideAttestationRow> phosphoRows)
        {
            var msd = new pwiz.CLI.msdata.MSDataFile(sourceFilepath);
            var spectrumList = msd.run.spectrumList;

            int rowNumber = 0;
            int totalRows = phosphoRows.Count();
            items.Clear();

            var spectrumTypes = new Set<CVID>();

            foreach (var row in phosphoRows)
            {
                if (rowNumber == 0 || (rowNumber % 100) == 0)
                {
                    if (cancelAttestation.IsCancellationRequested)
                    {
                        this.progressBar.ProgressBar.Visible = false;
                        _bgWorkerCancelled = true;
                        setProgress(-1, "Cancelled.");
                        return null;
                    }
                    else
                    {
                        if (rowNumber == 0)
                            setStatus(String.Format("Reading peaks and creating PhosphoRS objects for source {0} of {1} ({2}): {3} spectra\r\n", currentSource, totalSources, Path.GetFileName(sourceFilepath), totalRows));
                        setProgress((rowNumber + 1) / totalRows * 100, String.Format("Reading peaks and creating PhosphoRS objects for source {0} of {1} ({2}): {3}/{4} spectra", currentSource, totalSources, Path.GetFileName(sourceFilepath), rowNumber + 1, totalRows));
                    }
                }

                var pwizSpectrum = spectrumList.spectrum(spectrumList.find(row.Value.SpectrumNativeID), true); //may create indexoutofrange error if no spectrum nativeID                   

                var OriginalMZs = pwizSpectrum.getMZArray().data; //getMZArray().data returns IList<double>
                var OriginalIntensities = pwizSpectrum.getIntensityArray().data;
                row.Value.Peaks = new phosphoRS.Peak[OriginalMZs.Count];
                for (int i = 0; i < OriginalMZs.Count; ++i)
                    row.Value.Peaks[i] = new phosphoRS.Peak(OriginalMZs[i], OriginalIntensities[i]);

                if (config.spectrumType == phosphoRS.SpectrumType.None)
                {
                    row.Value.SpectrumType = phosphoRS.SpectrumType.None;
                    foreach (var precursor in pwizSpectrum.precursors)
                        foreach (var method in precursor.activation.cvParamChildren(CVID.MS_dissociation_method))
                        {
                            // if dissociation method is set to "Auto" but could not be determined from the file, alert the user
                            if (!spectrumTypeByDissociationMethod.Contains(method.cvid))
                                throw new InvalidDataException("cannot handle unmapped dissociation method \"" + CV.cvTermInfo(method.cvid).shortName() + "\" for spectrum \"" + row.Value.SourceName + "/" + row.Value.SpectrumNativeID + "\"; please override the method manually");
                            else if (row.Value.SpectrumType != phosphoRS.SpectrumType.ECD_ETD) // don't override ETD (e.g. if there is also supplemental CID)
                            {
                                row.Value.SpectrumType = spectrumTypeByDissociationMethod[method.cvid];
                                spectrumTypes.Add(method.cvid);
                            }
                        }

                    if (row.Value.SpectrumType == phosphoRS.SpectrumType.None)
                        throw new InvalidDataException("cannot find a dissociation method for spectrum \"" + row.Value.SourceName + "/" + row.Value.SpectrumNativeID + "\"; please set the method manually");
                }
                else
                    row.Value.SpectrumType = config.spectrumType;

                var psm = getPhosphoRS_PSM(config, row.Value);

                // DEBUG
                //tbStatus.AppendText(PeptideToString(phosphoPeptide) + "," + AAS.ToOneLetterCodeString() + "," + ptmRepresentation.ToString() + "\n");
                // Init the mod map of original variant for this PSM.
                var id2ModMap = new List<System.Tuple<int, List<int>>> { new System.Tuple<int, List<int>>((int) row.Value.PSMId, row.Value.OriginalPhosphoSites.Keys.ToList<int>()) };

                items.Add(new System.Tuple<phosphoRS.PeptideSpectrumMatch, List<System.Tuple<int, List<int>>>>(psm, id2ModMap));

                ++rowNumber;
            }

            // report automatically found fragmentation method
            if (config.spectrumType == phosphoRS.SpectrumType.None)
                setStatus(String.Format("Found {0} fragmentation types: {1}\r\n", spectrumTypes.Count, String.Join(", ", spectrumTypes.Keys.Select(o => CV.cvTermInfo(o).shortName()))));

            setProgress(currentSource / totalSources * 100, String.Format("Running PhosphoRS on source {0} of {1} ({2})...", currentSource, totalSources, Path.GetFileName(sourceFilepath)));

            // Initialize the localization.
            currentNr = 0;
            var phosphoRS_Context = new phosphoRS.ThreadManagement(this, cancelAttestation, config.maxIsoformCount, config.maxPTMCount, config.scoreNLToo, config.fragmentMassTolerance, config.scoredAA, items.Count);

            // Start the site localization (takes advantage of multi-threading)
            try
            {
                phosphoRS_Context.StartPTMLocalisation();

                // Safety if the attestation module doesn't throw the exception.
                if (cancelAttestation.IsCancellationRequested)
                {
                    this.progressBar.ProgressBar.Visible = false;
                    _bgWorkerCancelled = true;
                    setProgress(-1, "Cancelled.");
                    return null;
                }

                return phosphoRS_Context.PTMResult;
            }
            catch (OperationCanceledException)
            {
                this.progressBar.ProgressBar.Visible = false;
                _bgWorkerCancelled = true;
                setProgress(-1, "Cancelled.");
                return null;
            }
            finally
            {
                msd.Dispose();
            }
        }

        private void ExecutePhosphoRS(PhosphoRSConfig config)
        {
            // Set up the cancel token.
            cancelAttestation = new CancellationTokenSource();

            // Initialize  time stamps and status reporting variables.
            DateTime startTime = DateTime.Now;

            Invoke(new MethodInvoker(() =>
            {
                progressBar.ProgressBar.Visible = true;
                progressBar.Maximum = 100;
                //tbStatus.Font = new Font(FontFamily.GenericMonospace, 10.0f);
            }));

            session.CreateSQLQuery("CREATE TABLE IF NOT EXISTS PeptideModificationProbability (PeptideModification INTEGER PRIMARY KEY, Probability NUMERIC)").ExecuteUpdate();
            session.CreateSQLQuery("DELETE FROM PeptideModificationProbability").ExecuteUpdate();
            var insertSiteProbabilityCommand = session.Connection.CreateCommand();
            var PepModParameter = insertSiteProbabilityCommand.CreateParameter();
            var ProbParameter = insertSiteProbabilityCommand.CreateParameter();
            insertSiteProbabilityCommand.Parameters.Add(PepModParameter);
            insertSiteProbabilityCommand.Parameters.Add(ProbParameter);
            insertSiteProbabilityCommand.CommandText = "INSERT INTO PeptideModificationProbability VALUES (?,?)";

            // Init the variables used by phosphoRS. Clear its internal variables also.
            items = new List<System.Tuple<phosphoRS.PeptideSpectrumMatch, List<System.Tuple<int, List<int>>>>>();

            var distinctSources = session.Query<SpectrumSource>().ToList();

            var sourceFilepaths = new Dictionary<string, string>();
            distinctSources.ForEach(source => sourceFilepaths[source.Name] = IDPickerForm.LocateSpectrumSource(source.Name, session.Connection.GetDataSource()));

            // group the spectra by source and run each source as a batch
            int totalSources = distinctSources.Count;
            int currentSource = 0;

            //This task listen to this collection and takes any message that is send... 
            Action progressListenerAction = () =>
            {
                try
                {
                    phosphoRS.ThreadManagement.progressMessage msg;
                    double lastProgress = -1;
                    while (!progressMessageQueue.IsCompleted)
                    {
                        msg = progressMessageQueue.Take();
                        if (msg.type == phosphoRS.ThreadManagement.progressMessage.typeOfMessage.stringMessage && !msg.message.IsNullOrEmpty())
                        {
                            //setStatus(String.Format("PhosphoRS message: {0}\r\n", msg.message));
                        }
                        else
                        {
                            if (msg.spectraProcessed == 1.0 || msg.spectraProcessed > lastProgress + 0.01)
                            {
                                lastProgress = msg.spectraProcessed;
                                double baseProgress = (double) currentSource / totalSources;
                                int currentProgress = Math.Min(100, (int) Math.Round((baseProgress + msg.spectraProcessed * 1.0 / totalSources) * 100));
                                setProgress(currentProgress, String.Format("Running PhosphoRS on source {0} of {1}...", currentSource, totalSources));
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            };

            foreach (var source in distinctSources)
            {
                Task progressListener = Task.Factory.StartNew(progressListenerAction);
                progressMessageQueue = new BlockingCollection<phosphoRS.ThreadManagement.progressMessage>(new ConcurrentQueue<phosphoRS.ThreadManagement.progressMessage>());

                // get phospho peptide-spectrum matches for the source
                setStatus(String.Format("Finding phosphopeptides in \"{0}\"... ", source.Name));
                setProgress(-1, String.Format("Finding phosphopeptides in \"{0}\"... ", source.Name));
                DateTime findTime = DateTime.Now;
                var phosphoPSMs = getPhosphoPSMs(source.Id.Value);
                setStatus(String.Format("found {0} phosphopeptides ({1} seconds elapsed).\r\n", phosphoPSMs.Count, (DateTime.Now - findTime).TotalSeconds));

                if (phosphoPSMs.Count == 0)
                    continue;

                phosphoRS.PTMResultClass result = RunOnSource(sourceFilepaths[source.Name], ++currentSource, totalSources, config, phosphoPSMs);

                if (cancelAttestation.IsCancellationRequested)
                    return;

                if (result == null)
                    throw new Exception("Error running PhosphoRS on source " + source.Name);

                IDictionary<int, double> propMap;
                IDictionary<int, double> peptideScoreMap;
                IDictionary<int, string> sitepropMap;
                // peptide ID to site probabilities map
                sitepropMap = result.PeptideIdPrsSiteProbabilitiesMap;
                // peptid ID to isoform confidence probability map
                propMap = result.PeptideIdPrsProbabilityMap;
                // peptide ID to binomial score map
                peptideScoreMap = result.PeptideIdPrsScoreMap;

                //setProgress(4, "(4/4) Injecting results into the database...");
                // A map of PSMId to localization representation in string format.
                //Dictionary<long, string> localizationStrings = new Dictionary<long, string>();
                var transaction = session.BeginTransaction();
                foreach (var isoform in result.IsoformGroupList)
                {
                    if (isoform.Count == 0)
                        continue;

                    PhosphoPeptideAttestationRow row;
                    if (isoform.Error)
                    {
                        long PSMId = (long) isoform.PeptideIDs[0];
                        row = phosphoPSMs[PSMId];
                        setStatus(String.Format("Error running on {0} ({1}): {2}\r\n", PSMId, phosphoPSMs[PSMId].Peptide.sequence, isoform.Message));
                        continue;
                    }
                    else
                    {
                        long PSMId = (long) isoform.Peptides.First().ID;
                        row = phosphoPSMs[PSMId];
                    }

                    long pmId = -1;
                    foreach(var site in isoform.SiteProbabilities)
                    {
                        bool gotSite = row.OriginalPhosphoSites.TryGetValue(site.SequencePosition - 1, out pmId);
                        if (!gotSite)
                            continue;

                        PepModParameter.Value = pmId;
                        ProbParameter.Value = site.Probability;
                        insertSiteProbabilityCommand.ExecuteNonQuery();
                    }

                    if (pmId == -1)
                        throw new InvalidDataException("no PhosphoRS site probability matches to an original phospho site");
                }
                transaction.Commit();
            }

            Invoke(new MethodInvoker(() =>
            {
                progressBar.ProgressBar.Visible = false;
                setProgress(-1, "Finished.");
            }));
        }

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
                package.Add(new phosphoRS.ThreadManagement.SpectraPackageItem(items[i].Item1, 1.0 / items.Count, items[i].Item2));

            currentNr += package.Count;
            numberOfSpectraPacked = package.Count;
            numberOfPeptidesPacked = package.Count;

            return package;
        }

        private BlockingCollection<phosphoRS.ThreadManagement.progressMessage> progressMessageQueue;

        BlockingCollection<phosphoRS.ThreadManagement.progressMessage> phosphoRS.ThreadManagement.IDataConection.GetProgressMessageQueue()
        {
            return progressMessageQueue;
        }

        private void ExecuteAttestButton_Click(object sender, EventArgs e)
        {
            btnAttestPTMs.Enabled = false;
            btnCancelAttestaion.Enabled = true;
            
            _bgWorkerAttestation = new BackgroundWorker();
            //_bgWorkerClustering.WorkerReportsProgress = true;
            _bgWorkerAttestation.WorkerSupportsCancellation = true;

            _bgWorkerCancelled = false;

            var config = new PhosphoRSConfig();
            config.setSpectrumType(dissociationTypeComboBox.SelectedItem.ToString());
            config.fragmentMassTolerance = Double.Parse(FragmentMZToleranceTextBox.Text);

            _bgWorkerAttestation.DoWork += _bgWorkerClustering_DoWork;
            _bgWorkerAttestation.RunWorkerCompleted += _bgWorkerClustering_RunWorkerCompleted;
            _bgWorkerAttestation.RunWorkerAsync(config);
        }

        private void _bgWorkerClustering_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var config = (PhosphoRSConfig) e.Argument;
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
            public IDictionary<int, long> OriginalPhosphoSites { get; private set; }
            public phosphoRS.Peak[] Peaks { get; set; }
            public proteome.Peptide Peptide { get; private set; }
            public phosphoRS.SpectrumType SpectrumType { get; set; }

            // Properties
            public bool HasPeaks { get { return Peaks.Length > 0; } }
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
                OriginalPhosphoSites = new SortedDictionary<int, long>();
                var mods = new Dictionary<int, List<double>>();
                string peptideSequence = (string)queryRow[7];
                Peptide = new proteome.Peptide(peptideSequence);
                var pwizMods = Peptide.modifications();
                if (!String.IsNullOrEmpty((string)queryRow[6]))
                {
                    var IdMassDeltaAndOffsetTriplets = ((string)queryRow[6]).Split(',');
                    foreach (var triplet in IdMassDeltaAndOffsetTriplets)
                    {
                        var tokens = triplet.Split(':');
                        long pmId = Convert.ToInt64(tokens[0]);
                        double deltaMass = Convert.ToDouble(tokens[1]);
                        int roundedDeltaMass = (int) Math.Round(deltaMass);
                        int offset = Convert.ToInt32(tokens[2]);
                        pwizMods[offset].Add(new proteome.Modification(deltaMass, deltaMass));
                        if (roundedDeltaMass == 80 && (peptideSequence[offset] == 'S' || peptideSequence[offset] == 'T' || peptideSequence[offset] == 'Y'))
                            OriginalPhosphoSites[offset] = pmId;
                        else
                        {
                            if (!mods.ContainsKey(offset))
                                mods[offset] = new List<double>();
                            mods[offset].Add(deltaMass);
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
            this.dissociationTypeComboBox.SelectedIndex = 0;
        }
    }
}
