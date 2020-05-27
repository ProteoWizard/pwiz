using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class PeptideSequenceMatcher
    {
        private ModificationMatcher _modificationMatcher;
        private Dictionary<string, CrosslinkLibraryKey> _foundCrosslinks;

        public PeptideSequenceMatcher(SrmSettings srmSettings, MappedList<string, StaticMod> defSetStatic, MappedList<string, StaticMod> defSetHeavy)
        {
            _modificationMatcher = new ModificationMatcher();
            StaticMods = defSetStatic;
            HeavyMods = defSetHeavy;
        }

        public SrmSettings SrmSettings { get; private set; }
        public MappedList<string, StaticMod> StaticMods { get; private set; }
        public MappedList<string, StaticMod> HeavyMods { get; private set; }

        public void CreateMatches(IList<string> peptideSequences)
        {
            // foreach (var peptideSequence in peptideSequences)
            // {
            //     if ()
            // }
        }

        public string GetDescriptionOfFoundModifications()
        {
            return _modificationMatcher.FoundMatches;
        }

        public PeptideDocNode GetModifiedNode(string peptideModSequence, FastaSequence fastaSequence)
        {
            if (FastaSequence.IsExSequence(peptideModSequence))
            {
                return _modificationMatcher.GetModifiedNode(peptideModSequence, fastaSequence);
            }

            CrosslinkLibraryKey crosslinkLibraryKey =
                CrosslinkSequenceParser.ParseCrosslinkLibraryKey(peptideModSequence, 1);
            throw new NotImplementedException();
        }

        public string ValidatePeptideSequence(string peptideSequence)
        {
            if (FastaSequence.IsExSequence(peptideSequence))
            {
                return null;
            }

            CrosslinkLibraryKey crosslinkLibraryKey;
            try
            {
                crosslinkLibraryKey = CrosslinkSequenceParser.ParseCrosslinkLibraryKey(peptideSequence, 0);
            }
            catch (CommonException)
            {
                return Resources.PasteDlg_ListPeptideSequences_This_peptide_sequence_contains_invalid_characters;
            }

            if (crosslinkLibraryKey.PeptideLibraryKeys.Count == 0)
            {
                return Resources.PasteDlg_ListPeptideSequences_This_peptide_sequence_contains_invalid_characters;
            }

            foreach (var peptideLibraryKey in crosslinkLibraryKey.PeptideLibraryKeys)
            {
                if (!FastaSequence.IsExSequence(peptideLibraryKey.ModifiedSequence))
                {
                    return Resources.PasteDlg_ListPeptideSequences_This_peptide_sequence_contains_invalid_characters;
                }
            }

            return null;
        }
    }
}
