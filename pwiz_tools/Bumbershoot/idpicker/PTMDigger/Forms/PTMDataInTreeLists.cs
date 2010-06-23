using System;
using System.Collections.Generic;
using System.Collections;
using System.Data;
using IDPicker;

namespace Forms
{
    /// <summary>
    /// This class maps all delta mass amino acid pairs to protein and peptides that
    /// contain the pair
    /// </summary>
    public class PTMDataInTreeLists
    {
        /// <summary>
        /// delta mass amino acid pairs
        /// </summary>
        public Map<int, Map<string, Map<string, Protein>>> dataTrees;

        /// <summary>
        /// delta mass spectral count tables
        /// </summary>
        public Map<int, Map<string, int>> spectralCounts;

        /// <summary>
        /// Holds the delta mass table
        /// </summary>
        public DataTable deltaMassTable;

        /// <summary>
        /// Holds all the columns of the delta mass table
        /// </summary>
        public Set<string> uniqueCols;

        public Map<int, Map<string, Set<string>>> annotations;

        public PTMDataInTreeLists()
        {
            dataTrees = new Map<int, Map<string, Map<string, Protein>>>();
            spectralCounts = new Map<int, Map<string, int>>();
            annotations = new Map<int, Map<string, Set<string>>>();
            uniqueCols = new Set<string>();
            uniqueCols.Add("Total");
        }

        /// <summary>
        /// This function takes a delta mass table key and a protein. It looks up
        /// the table to see if the protein is already present. It merges the 
        /// peptides of this entry with peptides of existing entry
        /// </summary>
        /// <param name="aminoacid"></param>
        /// <param name="mass"></param>
        /// <param name="newProtein"></param>
        public void addRecord(string aminoacid, int mass, string locus, int CID, VariantInfo var, SpectrumInfo res)
        {
            // Get the list of proteins that are mapped to this key
            Map<string, Protein> proteins = dataTrees[mass][aminoacid];
            if (proteins == null)
                proteins = new Map<string, Protein>();
            Protein prot = proteins[locus];
            prot.clusterID = CID;
            prot.proteinID = locus;

            prot.peptides.Add(var.peptide);
            string interp = var.ToInsPecTStyle();
            prot.interpretations[var.peptide].Add(interp);
            prot.variants[interp].Add(var);
            prot.spectra[var].Add(res);
        }

        /// <summary>
        /// This function takes the spectral counts in the delta mass table
        /// and prepares it for display
        /// </summary>
        public void makeDeltaMassTable()
        {
            // Get a new table and add the "DeltaMass" column 
            // and other "amino acid" columns.
            deltaMassTable = new DataTable();
            deltaMassTable.Columns.Add(new DataColumn("DeltaMass", typeof(int)));
            foreach (var col in uniqueCols)
                deltaMassTable.Columns.Add(new DataColumn(col, typeof(int)));

            // Scan through the spectral count table 
            // and add the data to the table
            foreach (var row in spectralCounts.Keys)
            {
                // Get a new row and add columns
                DataRow dataRow = deltaMassTable.NewRow();
                deltaMassTable.Rows.Add(dataRow);
                dataRow["DeltaMass"] = row.ToString();
                foreach (var col in spectralCounts[row].Keys)
                    dataRow[col] = spectralCounts[row][col];
            }
        }

        /// <summary>
        /// This function creates a map of aminoacid,mass to 
        /// respective unimod annotations.
        /// </summary>
        /// <param name="ws">IDPicker Workspace</param>
        public void makeAnnotationsTable(Workspace ws)
        {
            // Get the mods one-by-one
            foreach (var modList in ws.modificationAnnotations.Values)
                foreach (var mod in modList)
                {
                    int mass = (int)Math.Round(mod.monoIsotopicMass, 0);
                    // Get their specificity
                    foreach (var specificity in mod.specificities)
                    {
                        string aa = specificity.aminoAcid;
                        if (aa == "n")
                            aa = "N-term";
                        else if (aa == "c")
                            aa = "C-term";
                        // Insert the mod at mass,aa index.
                        annotations[mass][aa].Add(mod.title + " of " + aa + " [" + Math.Round(mod.monoIsotopicMass, 2) + "]");
                    }
                }
        }

        public void loadFromWorkspace(Workspace space, ref Map<UniqueSpectrumID, PTMDigger.AttestationStatus> validationResults,
                                      PTMDigger.AttestationStatus[] validationStatus)
        {
            Map<UniqueSpectrumID, VariantInfo> primSeqs = null;
            loadFromWorkspace(space, ref primSeqs, ref validationResults, validationStatus);
        }

        public void loadFromWorkspace(Workspace space, ref Map<UniqueSpectrumID, VariantInfo> primSeqs)
        {
            Map<UniqueSpectrumID, PTMDigger.AttestationStatus> noValidation = null;
            loadFromWorkspace(space, ref primSeqs, ref noValidation, new PTMDigger.AttestationStatus[]{PTMDigger.AttestationStatus.IGNORE});
        }

        /// <summary>
        /// This function takes an existing IDPicker Workspace object and
        /// extracts the delta mass table information. For each entry in 
        /// the delta mass table, it also creates a list of peptides that
        /// went into making that entry.
        /// </summary>
        /// <param name="ws"></param>
        public void loadFromWorkspace(Workspace space, ref Map<UniqueSpectrumID, VariantInfo> primSeqs,
                                      ref Map<UniqueSpectrumID, PTMDigger.AttestationStatus> validationResults,
                                      PTMDigger.AttestationStatus[] validationStatus)
        {
            // for each source
            foreach (SourceGroupList.MapPair groupItr in space.groups)
            {
                foreach (SourceInfo source in groupItr.Value.getSources(true))
                {
                    foreach (SpectrumList.MapPair sItr in source.spectra)
                        foreach (ResultInstance i in sItr.Value.results.Values)
                            foreach (VariantInfo pep in i.info.peptides)
                            {
                                // For each variant get the peptide
                                PeptideInfo pepInfo = pep.peptide;
                                if (primSeqs != null)
                                {
                                    UniqueSpectrumID uniqId = UniqueSpectrumID.extractUniqueSpectrumID(sItr.Value);
                                    // Store the PSM
                                    primSeqs[uniqId] = pep;
                                }
                                bool considerPeptide = false;
                                if (validationStatus[0] == PTMDigger.AttestationStatus.IGNORE)
                                    considerPeptide = true;
                                else
                                {

                                    UniqueSpectrumID uniqId = UniqueSpectrumID.extractUniqueSpectrumID(sItr.Value);
                                    PTMDigger.AttestationStatus peptideStatus = PTMDigger.AttestationStatus.UNKNOWN;
                                    if (validationResults.Contains(uniqId))
                                        peptideStatus = validationResults[uniqId];
                                    foreach(var requiredStatus in validationStatus)
                                        if(peptideStatus == requiredStatus)
                                            considerPeptide = true;
                                }

                                if (!considerPeptide)
                                    continue;
                                // March through the sequence and check for any mods
                                for (int aa = 0; aa < pep.peptide.sequence.Length; ++aa)
                                {
                                    ModMap.Enumerator posMod = pep.mods.Find(Convert.ToChar(aa + 1));
                                    // If we find a mod
                                    if (posMod.IsValid)
                                    {
                                        // Get the mass and amino acid
                                        int mass = (int)Math.Round(pep.mods.getMassAtResidue(Convert.ToChar(aa + 1)), 0);
                                        string aminoacid = pep.peptide.sequence[aa].ToString();
                                        // Get the proteins that match to this peptide
                                        ProteinInstanceList.Enumerator proItr = pep.peptide.proteins.GetEnumerator();
                                        while (proItr.MoveNext())
                                        {
                                            // Get the clusterID and protein locus
                                            int protID = proItr.Current.Value.protein.proteinGroup.id;
                                            string locus = proItr.Current.Value.protein.locus;
                                            Protein prot = new Protein(locus, protID);
                                            prot.peptides.Add(pepInfo);
                                            this.addRecord(aminoacid, mass, locus, protID, pep, i.spectrum);
                                            // Update the spectral counts for the entry
                                            ++spectralCounts[mass][aminoacid];
                                            ++spectralCounts[mass]["Total"];
                                            uniqueCols.Add(aminoacid);
                                        }
                                    }
                                }
                                // Check for n-terminal and c-terminal mods
                                ModMap.Enumerator termMod = pep.mods.Find('n');
                                if (termMod.IsValid)
                                {
                                    int mass = (int)Math.Round(pep.mods.getMassAtResidue('n'), 0);
                                    string aminoacid = "N-term";
                                    ProteinInstanceList.Enumerator proItr = pep.peptide.proteins.GetEnumerator();
                                    while (proItr.MoveNext())
                                    {
                                        // Get the clusterID and protein locus
                                        int protID = proItr.Current.Value.protein.proteinGroup.id;
                                        string locus = proItr.Current.Value.protein.locus;
                                        this.addRecord(aminoacid, mass, locus, protID, pep, i.spectrum);
                                        // Updated the spectral counts for the entry
                                        ++spectralCounts[mass][aminoacid];
                                        ++spectralCounts[mass]["Total"];
                                        uniqueCols.Add(aminoacid);
                                    }
                                }
                                termMod = pep.mods.Find('c');
                                if (termMod.IsValid)
                                {
                                    int mass = (int)Math.Round(pep.mods.getMassAtResidue('c'), 0);
                                    string aminoacid = "C-term";
                                    ProteinInstanceList.Enumerator proItr = pep.peptide.proteins.GetEnumerator();
                                    while (proItr.MoveNext())
                                    {
                                        // Get the clusterID and protein locus
                                        int protID = proItr.Current.Value.protein.proteinGroup.id;
                                        string locus = proItr.Current.Value.protein.locus;
                                        Protein prot = new Protein(locus, protID);
                                        prot.peptides.Add(pepInfo);
                                        this.addRecord(aminoacid, mass, locus, protID, pep, i.spectrum);
                                        // Updated the spectral counts for the entry
                                        ++spectralCounts[mass][aminoacid];
                                        ++spectralCounts[mass]["Total"];
                                        uniqueCols.Add(aminoacid);
                                    }
                                }
                            }
                }
            }
        }
    }

    /// <summary>
    /// This class maps a protein to all peptides containing
    /// a particular modification
    /// </summary>
    public class Protein
    {
        /// <summary>
        /// Cluster ID of the whole protein and the peptides
        /// </summary>
        public int clusterID;

        /// <summary>
        /// Protein locus 
        /// </summary>
        public string proteinID;

        /// <summary>
        /// List of peptides, spectra, variants, and interpretations
        /// </summary>
        public Set<PeptideInfo> peptides;
        public Map<VariantInfo, Set<SpectrumInfo>> spectra;
        public Map<string, Set<VariantInfo>> variants;
        public Map<PeptideInfo, Set<string>> interpretations;

        public Protein()
        {
            peptides = new Set<PeptideInfo>();
            spectra = new Map<VariantInfo, Set<SpectrumInfo>>();
            variants = new Map<string, Set<VariantInfo>>();
            interpretations = new Map<PeptideInfo, Set<string>>();
        }

        public Protein(string locus, int CID)
        {
            proteinID = locus;
            clusterID = CID;
            peptides = new Set<PeptideInfo>();
            spectra = new Map<VariantInfo, Set<SpectrumInfo>>();
            variants = new Map<string, Set<VariantInfo>>();
            interpretations = new Map<PeptideInfo, Set<string>>();
        }
    }
}
