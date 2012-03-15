/*
##############################################################################
# file: ms_mascotresprotein.hpp                                              #
# 'msparser' toolkit                                                         #
# Encapsulates a protein - either for protein summary or peptide summary     #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2002 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_mascotresprotein.hpp    $ #
#     $Author: davidc $ #
#       $Date: 2011-01-19 14:03:57 $ #
#   $Revision: 1.42 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASCOTRESPROTEIN_HPP
#define MS_MASCOTRESPROTEIN_HPP

#ifdef _WIN32
#pragma warning(disable:4251)   // Don't want all classes to be exported
#pragma warning(disable:4786)   // Debug symbols too long
#   ifndef _MATRIX_USE_STATIC_LIB
#       ifdef MS_MASCOTRESFILE_EXPORTS
#           define MS_MASCOTRESFILE_API __declspec(dllexport)
#       else
#           define MS_MASCOTRESFILE_API __declspec(dllimport)
#       endif
#   else
#       define MS_MASCOTRESFILE_API
#   endif
#else
#   define MS_MASCOTRESFILE_API
#endif

// for the sake of #include <string>
#ifdef __ALPHA_UNIX__
#include <ctype.h>
#endif

// Includes from the standard template library
#include <string>
#include <list>
#include <vector>
#include <set>
#include <map>


namespace matrix_science {

    class ms_mascotresults;
    class ms_proteinsummary;
    class ms_pepinfoSortByScore;

    /** @addtogroup resfile_group
     *  
     *  @{
     */

    //! This class encapsulates a protein in the mascot results file.
    /*!
     * Pointers to ms_protein objects are returned from 
     * ms_peptidesummary::getHit() or ms_proteinsummary::getHit(),
     * so there should be no need to create one of these from outside the
     * library.
     */
    class MS_MASCOTRESFILE_API ms_protein
    {
        public:
            //! Enum to say if a protein is similar to another higher scoring protein.
            /*!
             *
             * See \ref subsetsPage and \ref DynLangEnums.
             *
             * Note that if there are say 3 proteins with the same 4 
             * peptide matches, then the highest scoring protein will have
             * GROUP_NO, and the other two will have GROUP_COMPLETE. Calling
             * getSimilarProteinName() on the highest scoring protein 
             * will return an empty string. Calling it for the other two proteins
             * will return the accession for the highest scoring protein.
             *
             * \sa ms_mascotresults::getNextSimilarProtein(), ms_mascotresults::getNextSubsetProteinOf()
             */
            enum GROUP 
            { 
                GROUP_UNKNOWN,  //!< No information about grouping.
                GROUP_NO,       //!< Does not contain same set (or subset) of peptides as another proteins. A 'lead' protein.
                GROUP_SUBSET,   //!< Contains a subset of peptides in one ore more other proteins.
                GROUP_COMPLETE, //!< Contains an identical set of peptides to one or more other proteins.
                GROUP_FAMILY    //!< Second or subsequent family member when using new family grouping introduced in Mascot 2.3.
            };

            //! Enum for the each peptide in the protein to indicate if it is a duplicate.
            /*!
             * See \ref DynLangEnums.
             *
             * A protein match is made up of one or more peptides. Duplicate
             * peptides don't increase the coverage of the protein. They also
             * do not increase the score except for MudPIT scoring.
             */
            enum DUPLICATE 
            { 
                DUPE_NotDuplicate,           //!< There are no other peptides with the same sequence in this protein - from this query or other queries.
                DUPE_Duplicate,              //!< Another peptide from a different query with the same sequence as this got a higher score.
                DUPE_DuplicateSameQuery,     //!< Another match for the same query with the same peptide string got a higher score (different mods).
                DUPE_HighestScoringDuplicate //!< There is at least one other peptide the same as this with a lower score.
            };

            //! enum for each protein to specify what masses to select.
            /*!
             * See \ref DynLangEnums.
             *
             * Only a subset of all masses is used for scoring proteins.
             * However, all matching masses are usually reported for each
             * protein. Using these flags one can specify more precisely what
             * sub-set of masses one is interested in. The flags can be
             * combined with binary OR ("|"-operator in C++).
             */
            enum MASS_FLAGS
            {
                MASS_NON_SELECT_NON_MATCH   = 0x0001, //!< Only masses that are not selected and couldn't match.
                MASS_SELECT_NON_MATCH       = 0x0010, //!< Only masses that are selected but do not match.
                MASS_NON_SELECT_MATCH       = 0x0100, //!< Only masses that are not selected but otherwise would match.
                MASS_SELECT_MATCH           = 0x1000  //!< Only masses that are selected and do match.
            };

            //! Enum for getNumDistinctPeptides().
            /*! 
             * See \ref DynLangEnums.
             *
             * There are several possible defintions for 'distinct'!
             * 
             * One of more of these flags can be combined using a bitwise 'OR' 
             * operator to determine which peptide matches are treated as 
             * distinct matches when counting up matches. Imagine a protein 
             * that has the following matches 
             *
             * <ul>
             * <li> AGCMK - Charge state 2 </li>
             * <li> AGCMK - Charge state 3 </li>
             * <li> AGCMK - Charge state 2, Oxidised methionine </li>
             * <li> HSMTMR  - Charge state 2 </li>
             * <li> HSMTMR  - Charge state 2 </li>
             * <li> HSMTMR  - Charge state 2, Oxidised methionine </li>
             * </ul>
             *
             * In this case:
             *
             * <ul>
             * <li>If DPF_SEQUENCE is specified, getNumDistinctPeptides() will
             * return 2.</li>
             * <li>If DPF_SEQUENCE .OR. DPF_CHARGE is specified,
             * getNumDistinctPeptides() will return 3.</li>
             * <li>If DPF_SEQUENCE .OR. DPF_MODS is specified,
             * getNumDistinctPeptides() will return 4.</li>
             * <li>If DPF_SEQUENCE .OR. DPF_CHARGE .OR. DPF_MODS is specified,
             * getNumDistinctPeptides() will return 5.</li>
             * </ul>
             * 
             * For completeness, getNumDisplayPeptides() will return a count of
             * 6 and getNumPeptides() could return a count of 6 or could return
             * 7 if HSM*TMR and HSMTM*R (where the asterisk indicates the 
             * oxidised methioine) both appear in the top 10 matches to the 
             * final query. (Some of these functions apply a threshold to the 
             * match scores, so this example assumes either no threshold is 
             * used or all matches are above threshold.)
             *
             * The MCP guidelines require a count of "the total number of 
             * peptides assigned to the protein. To compute this number, 
             * multiple matches to peptides with the same primary sequence
             * count as one, even if they represent different charge states
             * or modification states". Specify DPF_SEQUENCE by itself 
             * to obtain this value.
             *
             * A flags value that does not include DPF_SEQUENCE is unlikely to
             * give a useful return value from getNumDistinctPeptides().
             */
            enum DISTINCT_PEPTIDE_FLAGS
            {
                DPF_SEQUENCE  = 0x0001,  //!< Peptide matches must have different primary sequences to be counted as distinct matches.
                DPF_CHARGE    = 0x0002,  //!< Peptide matches must have different charge states to be counted as distinct matches.
                DPF_MODS      = 0x0004   //!< Peptide matches must have different modification states to be counted as distinct matches. 
            };

            // Types for uniquely identifying a protein
            typedef std::pair<int, std::string> dbIdxPlusAcc_t;
            typedef std::vector<dbIdxPlusAcc_t> dbIdxPlusAccVect_t;
            typedef std::set<dbIdxPlusAcc_t>    dbIdxPlusAccSet_t;


            //! Constructors - used from ms_proteinsummary and ms_peptidesummary.
            ms_protein(const double score,
                       const std::string accession,
                       const bool updateScoreFromPepScores,
                       const int  proteinSummaryHit = 0);

            //! Copying constructor.
            ms_protein(const ms_protein& src);

            //! Destructor - called automatically - don't call explicitly from Perl or Java
            ~ms_protein();

#ifndef SWIG
            //! C++ assignment operator.
            ms_protein& operator=(const ms_protein& right);
#endif

            //! Copies all content from another instance of the class.
            void copyFrom(const ms_protein* src);

            //! Return the accession string for a protein
            std::string getAccession() const;

            //! Return the index of the database where the sequence is found
            int getDB() const;

            //! Set database index
            void setDB(int dbIdx);

            //! Return the protein score for this protein.
            double getScore()          const;

            //! Will only return a different score from getScore() if the MSRES_MUDPIT_PROTEIN_SCORE flag has been specified.
            double getNonMudpitScore()     const;

            //! Return the number of peptides that had a match in this protein
            int    getNumPeptides()    const;

            //! Return the number of peptides excluding those that with duplicate matches to same query
            int    getNumDisplayPeptides(bool aboveThreshold = false)    const;

            //! Returns a flag which shows if this protein only contain the same peptides as those in another protein
            GROUP getGrouping() const;

#ifndef DOXYGEN_SHOULD_SKIP_THIS
            //! Set grouping - called from within library - do not call from outside the library. 
            void setGrouping(GROUP g)         { group_ = g;       }

            //! \internal
            std::string getForCache(dbIdxPlusAccVect_t & supersetProteinsUnsorted,
                                    dbIdxPlusAccVect_t & components) const;

            //! \internal
            bool setFromCache(const std::string & str, ms_mascotresults & results,
                              const dbIdxPlusAccVect_t & supersetProteinsUnsorted,
                              const dbIdxPlusAccVect_t & components);
#endif

            //! Return the query number given the peptide 'number'.
            int    getPeptideQuery         (const int   pepNumber) const;

            //! Return the 'rank' number given the peptide 'number'.
            int    getPeptideP             (const int   pepNumber) const;

            //! Return the pepNumber given query and rank.
            int getPepNumber(const int q, const int p) const;

            //! Return the frame number given the peptide 'number'.
            int    getPeptideFrame         (const int   pepNumber) const;

            //! Return the peptide start residue given the peptide 'number'.
            long   getPeptideStart         (const int   pepNumber) const;

            //! Return the peptide end residue given the peptide 'number'.
            long   getPeptideEnd           (const int   pepNumber) const;

            //! Return the number of precursor matches in this protein for the specified peptide 'number'.
            long   getPeptideMultiplicity  (const int   pepNumber) const;

            //! Return the DUPLICATE status given the peptide 'number'.
            DUPLICATE getPeptideDuplicate  (const int   pepNumber) const;

            //! Return the ions score within this protein context given the peptide 'number'.
            double getPeptideIonsScore     (const int   pepNumber) const;
            
            //! Returns true if this peptide should be displayed in bold in a Mascot report.
            bool   getPeptideIsBold        (const int   pepNumber) const;
            
            //! \internal
            void   setPeptideIsBold        (const int   pepNumber);

            //! Returns true if a check box for repeat searches should be shown in a Mascot report.
            bool   getPeptideShowCheckbox  (const int   pepNumber) const;
            
            //! \internal
            void   setPeptideShowCheckbox  (const int   pepNumber);
            
            //! Returns 0 except for a UniGene entry or a PMF mixture entry.
            int    getPeptideComponentID   (const int   pepNumber) const;
            
            //! Returns the residue immediately before the peptide.
            char   getPeptideResidueBefore (const int   pepNumber) const;

            //! Returns the residue immediately after the peptide.
            char   getPeptideResidueAfter  (const int   pepNumber) const;

            //! Find a protein in the results.
            bool isASimilarProtein(const ms_protein       * prot, 
                                   const ms_mascotresults * results,
                                   const bool groupByQueryNumber = false);

            //! Return the accession of a protein that contains the same set (or a superset of) of the peptides in this protein.
            std::string getSimilarProteinName() const;

            //! Return the database index of a protein that contains the same set (or a superset of) of the peptides in this protein.
            int getSimilarProteinDB() const;

            //! Returns true if the specified protein has the sameset or a superset of peptides that this protein has.
            bool isSimilarProtein(const std::string & acc, const int dbIdx) const;

            //! Return a list of proteins that that contains the same set (or a superset of) of the peptides in this protein.
            int getSimilarProteins(std::vector<std::string> & accessions, std::vector<int> & dbIdxs) const;

            //! Sets the accession and dbIdx for a similar protein.
            void setSimilarProtein(const ms_protein * prot);

            //! \internal 
            //! Add a peptide from ms_peptidesummary or ms_proteinsummary.
            void addOnePeptide(      ms_mascotresults & results,
                               const int frame,
                               const long start, const long end, 
                               const long multiplicity,
                               const int q, const int p,
                               const double correctedScore,
                               const double uncorrectedScore,
                               const char residueBefore,
                               const char residueAfter,
                               const ms_protein * component,
                               const bool integratedET);


            //! Return the number of residues covered.
            long getCoverage() const;

            //! See if any match to this query.
            bool anyMatchToQuery(const int query) const;

            //! See if any match to this query and 'P' (rank / hit).
            bool anyMatchToQueryAndP(const int query, const int P) const;

            //! Return a list of comma separated experimental masses that don't match.
            std::string getUnmatchedMasses(ms_mascotresfile & resfile,
                                           const int numDecimalPlaces = 2) const;

            //! Return a list of comma separated experimental masses according to a specified filter.
            std::string getMasses(ms_mascotresfile & resfile,
                                  const ms_proteinsummary & summary,
                                  const unsigned int flags = MASS_SELECT_MATCH,
                                  const int numDecimalPlaces = 2) const;

            //! Returns the frame number for the protein.
            int getFrame() const;

            //! Returns true if any of the peptides in the match were top scoring and not seen before.
            bool anyBoldRedPeptides(const ms_mascotresults & results) const;

            //! Returns true if the 'protein' is actually a UniGene entry.
            bool isUnigene() const;

            //! \internal Used internally to set the isUnigene() flag.
            void setIsUnigeneEntry();

            //! Returns true if the 'protein' is actually a PMF mixture.
            bool isPMFMixture() const;

            //! \internal Used internally to set the isPMFMixture() flag.
            void setIsPMFMixture();

            //! Sorts the peptides into ascending query number.
            void sortPeptides(const ms_mascotresults & results);

            //! For UniGene and PMF mixture, return number of 'component' proteins.
            int getNumComponents() const;

            //! For UniGene and PMF mixture return the 'component' protein.
            const ms_protein * getComponent(const int componentNumber) const;

            //! For a protein from the protein summary \e only.
            int getProteinSummaryHit() const;

            //! Return the RMS value of the deltas between the calculated and experimental value.
            double getRMSDeltas(const ms_mascotresults & results) const;

            //! Returns the hit number in the results list. 
            int getHitNumber() const;

            //! \internal Used internally to set the hit number. See also getHitNumber().
            void setHitNumber(const int hit) { hitNum_ = hit;}

            //! Return the length (in residues) of the longest peptide in the protein.
            int getLongestPeptideLen() const;

            //! Return the number of distinct peptides in the protein sequence.
            int getNumDistinctPeptides(bool aboveThreshold = false,
                                       DISTINCT_PEPTIDE_FLAGS flags = DPF_SEQUENCE) const;

            //! Protein objects perform a simple sort of themselves by database ID and then accession.
            /*!
             * \note
             * This method can only be used by C++ programs. 
             *
             * Final sorting for proteins by score and then accession is more
             * complex.
             */
            friend inline bool operator<(const ms_protein & lhs, const ms_protein & rhs) { 
                if (lhs.dbIdx_ == rhs.dbIdx_) {
                    if ( lhs.proteinSummaryHit_ == 0 ) {
                        return lhs.accession_ < rhs.accession_;
                    } else { // i.e ms_proteinsummary - see parser bug 493
                        if ( lhs.accession_ == rhs.accession_) {
                            return lhs.getFrame() < rhs.getFrame();
                        } else {
                            return lhs.accession_ < rhs.accession_;
                        }
                    }
                } else {
                    return lhs.dbIdx_ < rhs.dbIdx_; 
                }
            }

            // Undocumented function for fast access
            const char * getAccessionStr() const { return accession_.c_str(); }

        private:
            // For each peptide, we have frame, start, end multiplicity
            // and we want to just have a reference to the peptide
            // structures - using query and 'p', where p is 1..10
            //
            // If you change this, then change getForCache()
            // and setFromCache()
            typedef struct
            {
                double      ionsScore;
                double      uncorScore;
                int         start;
                int         end;
                long        multiplicity;
                int         query;
                int         p:7;
                int         frame:5;
                int         componentID;
                DUPLICATE   duplicate:3;
                short       dupeStatus:4;
                bool        bold:1;
                bool        checkBox:1;
                bool        integratedET:1;
                DUPLICATE   nonETduplicate:4;
                short       nonETdupeStatus:4;
                char        residueBefore; // :8;
                char        residueAfter; //:8;
            } PEPINFO;


            // --- Start of uncached variables
            mutable std::vector<PEPINFO *> peptides_; // sort by query
            mutable std::vector<PEPINFO> allPeptides_;

            ms_mascotresults * results_;
//          bool loadedFromCache_;
            // --- End of uncached variables

            // Start of all cached variables
            unsigned char    flags_; // See FL_... not all bits are cached

            int numPeptides_;  // Only used if loadedFromCache_ is true - otherwise peptides_.size();
            mutable int numDisplayPeptides_;
            mutable int numDisplayPeptidesAboveThresh_;
            mutable int numDistinctPeptides_;
            mutable int numDistinctPeptidesAboveThresh_;
            mutable int frame_;
            dbIdxPlusAccSet_t  supersetProteins_;  // This one is filled when loading from cache
            dbIdxPlusAccVect_t supersetProteinsUnsorted_;

            // For unignene and PMF mixture, the protein is really a 'pseudo'
            // protein, made up from a number of 'real' proteins
            dbIdxPlusAccVect_t components_;

            std::string accession_;
            int dbIdx_;
            double score_;
            double nonMudPITScore_;
            GROUP group_;
            int proteinSummaryHit_;
            int hitNum_;
            int longestPeptideLen_;         // Useful with minPepLenInPepSummary
//          bool pmfMixture_;               // True if protein actually originates from a PMF mixture
//          bool sorted_;                   // Sorting the list of peptides is expensive - don't repeat...
//          bool unigene_;                  // For unigene, we need to get the description line from the unigene file
//          bool updateScoreFromPepScores_; // For protein summary, the protein score is calculated by
                                            // nph-mascot.exe, and is in the results file. For the
                                            // peptide summary, the score is calculated by adding the ions
                                            // scores

            // Functions
            void checkFromCache(const char * calledBy) const;
            void checkQPFromCache(const char * calledBy) const;
			bool isFlagSet(unsigned char fl) const { return (flags_ & fl)?true:false; }
            void setFlag(unsigned char fl, bool val) {
                if (val) {
                    flags_ |= fl;
                } else {
                    flags_ &= ~fl; 
                }
            }

            friend class prot_sort;
            friend class ms_pepinfoSortByScore;
    };
#ifndef SWIG
    // Helper class - don't use from outside library
    class ms_proteinPtrSortByAccession
    {
        public:
            bool operator() (const ms_protein * p1, const ms_protein * p2) const {
                return (*p1 < *p2);
            }
    };

    class ms_proteinPtrSortByScore
    {
        public:
            bool operator() (const ms_protein * p1, const ms_protein * p2) const {
                if (p1->getScore() != p2->getScore()) {
                    return (p1->getScore() > p2->getScore());
                } else {
                    return (*p1 < *p2);
                }
            }
    };


    class ms_pepinfoSortByScore
    {
    public:
        ms_pepinfoSortByScore(bool removeDiffPos = false) { removeDiffPos_ = removeDiffPos; }
        bool operator() (const ms_protein::PEPINFO * p1, const ms_protein::PEPINFO * p2) const;

    private:
        bool removeDiffPos_;

    };

#endif
    /** @} */ // end of resfile_group
}   // matrix_science namespace

#endif // MS_MASCOTRESPROTEIN_HPP

/*------------------------------- End of File -------------------------------*/




