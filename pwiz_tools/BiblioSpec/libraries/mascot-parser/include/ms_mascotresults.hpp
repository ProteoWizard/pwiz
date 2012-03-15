/*
##############################################################################
# file: ms_mascotresults.hpp                                                 #
# 'msparser' toolkit                                                         #
# Abstract class for either ms_peptidesummary or ms_proteinsummary           #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2002 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotresul $ #
#     $Author: davidc $ #
#       $Date: 2011-08-08 11:53:02 $ #
#   $Revision: 1.91 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASCOTRESULTS_HPP
#define MS_MASCOTRESULTS_HPP

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

//#define DEBUG_LOGGING
#ifdef DEBUG_LOGGING
//#   define DEBUG_LOGGING_USE_STD_ERR
#   ifdef  DEBUG_LOGGING_USE_STD_ERR
#       define DEBUG_LOG(x,y,z) { \
                                    const time_t timeNow = time(NULL); \
                                    std::cerr << std::string(asctime(localtime(&timeNow)), 24)  << " :" << x << ", " << y << ", " << z << std::endl; \
                                }
#   else
#       define DEBUG_LOG_FILENAME "/tmp/parser.log"
#       define DEBUG_LOG(x,y,z) { \
                                    const time_t timeNow = time(NULL); \
                                    std::ofstream LOG_STREAM(DEBUG_LOG_FILENAME, std::ios_base::app); \
                                    LOG_STREAM \
                                        << std::string(asctime(localtime(&timeNow)), 24) \
                                        << " : " << x << ", " << y << ", " << z << std::endl; \
                                }
#   endif
#else
#   define DEBUG_LOG(x, y, z)
#endif

#ifdef __AIX__
#undef SCORE
#endif

namespace msparser_internal {
    class ms_proteininference;
    class ms_unassigned;
}


namespace matrix_science {
    /** @addtogroup resfile_group
     *  
     *  @{
     */

    class ms_unigene;
    class ms_protein;

    //! Abstract class for either ms_peptidesummary or ms_proteinsummary.
    /*!
     * The following functions provide threshold values: 
     *
     * <UL>
     * <li>getPeptideIdentityThreshold() </li>
     * <li>getAvePeptideIdentityThreshold() </li>
     * <li>getMaxPeptideIdentityThreshold() </li>
     * <li>getPeptideThreshold() </li>
     * <li>getProteinThreshold() </li>
     * <li>getHomologyThreshold() </li>
     * <li>getHomologyThresholdForHistogram() </li>
     * <li>getProbFromScore() </li>
     * <li>getProbOfPepBeingRandomMatch() </li>
     * <li>double getProbOfProteinBeingRandomMatch() </li>
     * </UL>
     * 
     */
    class MS_MASCOTRESFILE_API ms_mascotresults
    {
        friend class ms_protein;
        friend class msparser_internal::ms_proteininference;
        friend class msparser_internal::ms_peptidesumcdb;

        public:

            //! Flags for the type of results.
            /*!
             * See \ref DynLangEnums.
             *
             * Not all of the flags applicable for protein summary (e.g. 
             * \c MSRES_REQUIRE_BOLD_RED); see ms_proteinsummary.
             *
             */
            enum FLAGS 
            { 

                MSRES_NOFLAG                    = 0x00000000, //!< Does nothing.
                MSRES_GROUP_PROTEINS            = 0x00000001, //!< Group proteins with same peptide matches. See \ref subsetsPage.
                MSRES_SHOW_SUBSETS              = 0x00000002, //!< Show proteins that only match a subset of peptides. See \ref subsetsPage.
                MSRES_SUBSETS_DIFF_PROT         = 0x00000004, //!< Proteins that contain a subset of peptides are treated as a unique protein. See \ref subsetsPage.
                MSRES_REQUIRE_BOLD_RED          = 0x00000008, //!< Only proteins that have a top scoring peptide not seen before will be returned.
                MSRES_SHOW_ALL_FROM_ERR_TOL     = 0x00000010, //!< If this flag is set, then all hits from error tolerant search are shown. See \ref errorTolerantPage.
                MSRES_IGNORE_PMF_MIXTURE        = 0x00000020, //!< If this flag is set, then PMF mixtures are ignored. See \ref pmfmixturePage.
                MSRES_MUDPIT_PROTEIN_SCORE      = 0x00000040, //!< Protein scoring for the peptide summary was changed at Mascot 2.0 for large (MudPIT) searches. See ms_protein::getScore().
                MSRES_DECOY                     = 0x00000080, //!< If this flag is set, then use the results from searching against the decoy database. See \ref decoySearchPage.
                MSRES_INTEGRATED_ERR_TOL        = 0x00000100, //!< If this flag is set, then create a ms_peptidesummary object that contains results from the summary and et_summary section. See \ref errorTolerantPage.
                MSRES_ERR_TOL                   = 0x00000200, //!< If this flag is set, then create a ms_peptidesummary object that contains results from the et_summary section. See \ref errorTolerantPage.
                MSRES_MAXHITS_OVERRIDES_MINPROB = 0x00000400, //!< If minProbability and maxHitsToReport are both non zero, then minProbability is ignored when determining the number of proteins to be displayed. See ms_mascotresults::ms_mascotresults.
                MSRES_CLUSTER_PROTEINS          = 0x00000800, //!< Protein clustering introduced in Mascot 2.3. See \ref Grouping2.
                
                MSRES_DUPE_INCL_IN_SCORE_NONE   = 0x00000000, //!< See \ref duplicatesPage.
                MSRES_DUPE_INCL_IN_SCORE_A      = 0x00002000, //!< See \ref duplicatesPage.
                MSRES_DUPE_INCL_IN_SCORE_B      = 0x00004000, //!< See \ref duplicatesPage.
                MSRES_DUPE_INCL_IN_SCORE_C      = 0x00008000, //!< See \ref duplicatesPage.
                MSRES_DUPE_INCL_IN_SCORE_D      = 0x00010000, //!< See \ref duplicatesPage.
                MSRES_DUPE_INCL_IN_SCORE_E      = 0x00020000, //!< See \ref duplicatesPage.
                MSRES_DUPE_INCL_IN_SCORE_F      = 0x00040000, //!< See \ref duplicatesPage.
                MSRES_DUPE_INCL_IN_SCORE_G      = 0x00080000, //!< See \ref duplicatesPage.
                MSRES_DUPE_INCL_IN_SCORE_H      = 0x00100000, //!< See \ref duplicatesPage.
                MSRES_DUPE_INCL_IN_SCORE_I      = 0x00200000, //!< See \ref duplicatesPage.
                MSRES_DUPE_REMOVE_NONE          = 0x00400000, //!< See \ref duplicatesPage.
                MSRES_DUPE_REMOVE_A             = 0x00800000, //!< See \ref duplicatesPage.
                MSRES_DUPE_REMOVE_B             = 0x01000000, //!< See \ref duplicatesPage.
                MSRES_DUPE_REMOVE_C             = 0x02000000, //!< See \ref duplicatesPage.
                MSRES_DUPE_REMOVE_D             = 0x04000000, //!< See \ref duplicatesPage.
                MSRES_DUPE_REMOVE_E             = 0x08000000, //!< See \ref duplicatesPage.
                MSRES_DUPE_REMOVE_F             = 0x10000000, //!< See \ref duplicatesPage.
                MSRES_DUPE_REMOVE_G             = 0x20000000, //!< See \ref duplicatesPage.
                MSRES_DUPE_REMOVE_H             = 0x40000000, //!< See \ref duplicatesPage.
                MSRES_DUPE_REMOVE_I             = 0x80000000, //!< See \ref duplicatesPage.
                MSRES_DUPE_DEFAULT              = 0x04800000  //!< Default parameter for treatment of duplicates. See \ref duplicatesPage.
            };

            //! Flags for createUnassignedList().
            /*!
             * See \ref DynLangEnums.
             */
            enum sortBy 
            {
                QUERY,    //!< Sort the unassigned list by ascending query number - this is the same as ascending relative mass order.
                SCORE,    //!< Sort the unassigned list by descending score.
                INTENSITY //!< Sort the unassigned list by descending intensity. Intensity values are taken from the \c qintensity value in the results file if they are available (from PKL files, or some MGF files). If these values are not available, then the intensity is calculated from the sum of all the ions values. For a very large MS-MS file, this option can take some time to process unless there are \c qintensity value in the results file. 

            };

            /*! \internal
             *
             * Used internally in the library by queryScoreThisPeptide(),
             * queryRemoveThisPeptide() and ms_protein.
             *
             * See \ref DynLangEnums.
             */
            enum dupeStatus
            {
                dupe_query_same = 0x0001,  //!< See \ref duplicatesPage.
                dupe_seq_same   = 0x0002,  //!< See \ref duplicatesPage.
                dupe_mods_same  = 0x0004,  //!< See \ref duplicatesPage.
                dupe_pos_same   = 0x0008   //!< See \ref duplicatesPage.
            };

            //! Flags for getTreeClusterNodes().
            /*!
             * See \ref DynLangEnums.
             */
            enum TREE_CLUSTER_METHOD
            {
                TCM_FIRST_VALUE       = 0x0001, // For looping - undocumented
                TCM_PAIRWISE_SINGLE   = 0x0001, //!< 's': pairwise single-linkage clustering.
                TCM_PAIRWISE_MAXIMUM  = 0x0002, //!< 'm': pairwise maximum- (or complete-) linkage clustering.
                TCM_PAIRWISE_AVERAGE  = 0x0003, //!< 'a': pairwise average-linkage clustering.

                TCM_LAST_VALUE        = 0x0003  // For looping - undocumented
            };

            //! Flags for findPeptides() and findProteins().
            /*!
             * Details what needs to be searched for. Any of the FT_PEPTIDE_ 
             * flags may be used for either findPeptides() or findProteins(), 
             * but the FT_PROTEIN flags may only be used for findProteins()
             * 
             * See \ref DynLangEnums.
             */
            enum FIND_FLAGS
            {
                FT_PEPTIDE_EXP_MZ        = 0x00000001, //!< Find an experimental m/z value.
                FT_PEPTIDE_EXP_MR        = 0x00000002, //!< Find an experimental relative mass.
                FT_PEPTIDE_CALC_MZ       = 0x00000004, //!< Find a calculated m/z value.
                FT_PEPTIDE_CALC_MR       = 0x00000008, //!< Find a calculated relative mass.
                FT_PEPTIDE_STRING        = 0x00000010, //!< Find a peptide string.
                FT_PEPTIDE_QUERY         = 0x00000020, //!< Find a query number.
                FT_PEPTIDE_VARMOD        = 0x00000040, //!< Find a variable modification. Specifiy the modification 'number' as the string.
                FT_PEPTIDE_FIXMOD        = 0x00000080, //!< Find a fixed modification. Specifiy the modification 'number' as the string.
                FT_PEPTIDE_FIND_MASK     = 0x00000FFF, //!< Bit mask for any of the peptide values to be found

                FT_PROTEIN_ACCESSION     = 0x00001000, //!< Find an accession - findProteins() only.
                FT_PROTEIN_DESCRIPTION   = 0x00002000  //!< Find a description - findProteins() only.
            };

            //! Flags to specify how comparisons are performed in the find functions.
            /*!
             * See \ref DynLangEnums.
             *
             * These flags are used in findProteins(), findPeptides() and the 
             * deprecated functions: findProteinsByAccession() and 
             * findProteinsByDescription().
             * The flags are used to specify
             * how the comparison is performed and whether it should be
             * a forward or reverse seach. Typically, three values will be
             * combined together, using an OR operator; however, any
             * <i>default</i> values do not need to be specifically specified.
             *
             * \li Choose 1 of: \c FC_COMPLETESTR, \c FC_SUBSTR, \c FC_STARTSTR, \c FC_STRTOK
             * \li Choose 1 of: \c FC_CASE_INSENSITIVE, \c FC_CASE_SENSITIVE
             * \li Choose 1 of: \c FC_FORWARD, \c FC_REVERSE
             * \li Optionally choose \c FC_RESTRICT_TO_HIT or (\c FC_LOOP_INTO_UNASSIGNED and/or \c FC_LOOP_FROM_UNASSIGNED)
             * \li Optionally choose \c FC_SEARCH_ALL_RANKS
             * \li Optionally choose \c FC_ALL_PEPTIDES or \c FC_SIGNIFICANT_PEPTIDES
             * \li Optionally choose one or more of the FC_PROTEIN_IGN_ flags when calling findProteins()
             *
             */
            enum FIND_COMPARE_FLAGS
            {
                FC_COMPLETESTR          = 0x00000001, //!< Search for the complete string.
                FC_SUBSTR               = 0x00000002, //!< Search for any substring.
                FC_STARTSTR             = 0x00000003, //!< String must match to start of target string.
                FC_STRTOK               = 0x00000004, //!< Supplied string is a set of tokens, for example "STY" could be used to search for S or T or Y in a peptide sequence.
                FC_MASK_STR_PART        = 0x0000000F, //!< Bit mask to extract which one of FC_COMPLETESTR, FC_SUBSTR, FC_STARTSTR, FC_STRTOK has been specified.

                FC_CASE_INSENSITIVE     = 0x00000000, //!< Case insensitive search <i>(default)</i>.
                FC_CASE_SENSITIVE       = 0x00000010, //!< Case sensitive search.
                FC_MASK_CASE            = 0x000000F0, //!< Bit mask to extract which one of FC_CASE_INSENSITIVE, FC_CASE_SENSITIVE has been specified.

                FC_FORWARD              = 0x00000000, //!< Forward search. The returned hit number will be the same as or higher than the start hit number. <i>(default)</i>.
                FC_REVERSE              = 0x00000100, //!< Reverse search. The returned hit number will be the same as or lower than the start hit number.
                FC_MASK_DIRECTION       = 0x00000F00, //!< Bit mask to extract which one of FC_FORWARD, FC_REVERSE has been specified.

                FC_RESTRICT_TO_HIT      = 0x00001000, //!< Don't search beyond the specified hit number. Cannot be used with FC_LOOP_INTO_UNASSIGNED or FC_LOOP_FROM_UNASSIGNED.
                FC_LOOP_INTO_UNASSIGNED = 0x00002000, //!< If no matches are found in the passed hit, or any subsequent hit, then search the unassigned list.
                FC_LOOP_FROM_UNASSIGNED = 0x00004000, //!< If the passed hit number is 0, and no match is found in the unassigned list, then start searching at 1 if FC_FORWARD is specified or start searching at 'numHits' if FC_REVERSE is specified.
                FC_UNASSIGNED_MASK      = 0x00006000, //!< Bit mask to extract FC_LOOP_INTO_UNASSIGNED or FC_LOOP_FROM_UNASSIGNED.

                FC_SEARCH_ALL_RANKS     = 0x00008000, //!< For use with findPeptides() only. Ordinarily only those queries and ranks are searched that are assigned to a protein hit. Use this flag to search all ranks in such queries instead.

                FC_ALL_PEPTITDES        = 0x00000000, //!< Search all peptides regardless of score  <i>(default)</i>.
                FC_SIGNIFICANT_PEPTIDES = 0x00010000, //!< Only search peptides above identitity or homology threshold
                FC_SCORING_MASK         = 0x000F0000, //!< Bit mask to extract FC_ALL_PEPTITDES or FC_SIGNIFICANT_PEPTIDES.

                FC_PROTEIN_IGN_SAMESETS = 0x00100000, //!< Ignore sameset proteins - only used for findProteins() 
                FC_PROTEIN_IGN_SUBSETS  = 0x00200000, //!< Ignore subset proteins - only used for findProteins() 
                FC_PROTEIN_IGN_FAMILY   = 0x00400000, //!< Ignore family member proteins - only used for findProteins() 
                FC_PROTEIN_IGN_MASK     = 0x00F00000  //!< Ignore proteins flags
            };

            //! Flags for getIonsScoreHistogram().
            /*!
             * See \ref DynLangEnums.
             */
            enum IONS_HISTOGRAM
            {
                IH_INCLUDE_TOP_MATCHES           = 0x0000, //!< The default. Just include the top match to each spectrum.
                IH_INCLUDE_TOP_10_MATCHES        = 0x0001  //!< Instead of just the top match, use the top 10 matches to each spectrum.
/*              IH_INCLUDE_TOP_ERRTOL_MATCHES    = 0x0002, //!< Not yet implemented. Include top error tolerant match
                IH_INCLUDE_TOP_10_ERRTOL_MATCHES = 0x0004, //!< Not yet implemented. Include top 10 error tolerant match
                IH_INCLUDE_TOP_DECOY_MATCHES     = 0x0008, //!< Not yet implemented. Include top decoy matche
                IH_INCLUDE_TOP_10_DECOY_MATCHES  = 0x0010  //!< Not yet implemented. Include top 10 decoy matches */
            };
           //! Flags for isPeptideUnique().
            /*!
             * See \ref DynLangEnums.
             *
             * Choose UPR_WITHIN_FAMILY or UPR_WITHIN_FAMILY_MEMBER and then optionally 'or' UPR_IGNORE_SUBSET_PROTEINS
             */
            enum UNIQUE_PEP_RULES
            {
                UPR_WITHIN_FAMILY                = 0x0001, //!< The peptide is unique if it occurs in proteins that are part of a single family
                UPR_WITHIN_FAMILY_MEMBER         = 0x0002, //!< The peptide is unique if it occurs in proteins that just belong to a single family member
                UPR_IGNORE_SUBSET_PROTEINS       = 0x0004, //!< Ignore any susbset proteins that contain the match when deciding if a peptide is unique. However, if the peptide just belongs to subset proteins for the same hit, then it is still considered to be unique.

                UPR_DEFAULT                      = (UPR_WITHIN_FAMILY_MEMBER + UPR_IGNORE_SUBSET_PROTEINS) //!< Set to UPR_WITHIN_FAMILY_MEMBER | UPR_IGNORE_SUBSET_PROTEINS
            };

           //! Flags for getPeptideThreshold()
            /*!
             * See \ref DynLangEnums.
             */
            enum THRESHOLD_TYPE
            {
                TT_HOMOLOGY       = 0x0000, //!< Homology threshold
                TT_IDENTITY       = 0x0001, //!< Identity threshold
                TT_PEPSUM_DEFAULT = 0x0002  //!< If ms_peptidesummary::MSPEPSUM_USE_HOMOLOGY_THRESH is specified in the constructor, then this will resolve to TT_HOMOLOGY, otherwise it will resolve to TT_IDENTITY
            };
        public:

            // Some useful types
            typedef std::set<ms_protein> proteinSet;  // ms_protein has operator< which compares accession and dbIdx
            typedef std::set<std::pair<std::string, int> > acc_dbidx_set_t;
            typedef std::set<std::pair<int, int> > q_p_set_t;
            typedef std::vector<std::pair<std::string, int> > acc_dbidx_vect_t;
            typedef std::vector<std::pair<int, int> > q_p_vect_t;


            //! ms_mascotresults is an abstract class; use ms_peptidesummary::ms_peptidesummary or ms_proteinsummary::ms_proteinsummary.
            ms_mascotresults(ms_mascotresfile  &resfile,
                             const unsigned int flags,
                             double             minProbability,
                             int                maxHitsToReport,
                             const char *       unigeneIndexFile,
                             const char *       singleHit = 0);
            virtual ~ms_mascotresults();
            
            //! Create the summary after the ms_mascotresults object has been created
            virtual bool createSummary();

            //! Return progress for the createSummary() call
            bool getCreateSummaryProgress(int          * cspTotalPercentComplete,
                                          unsigned int * cspCurrTask, 
                                          int          * cspCurrTaskPercentageComplete, 
                                          std::string  * cspAccession, 
                                          int          * cspHit, 
                                          int          * cspQuery,
                                          std::string  * cspKeepAliveText) const;

            //! Cancel the call to createSummary()
            void cancelCreateSummary(bool newValue = true);

            //! Return the ms_protein hit - returns null if \c hit > number of hits.
            virtual ms_protein * getHit(const int hit) const;

            //! Frees any memory associated with the passed hit number
            virtual void freeHit(const int hit);

            //! Returns the number of hits in the results.
            virtual int getNumberOfHits() const;

            //! Return the total number of family members.
            virtual int getNumberOfFamilyMembers() const;

            //! Return protein description if available.
            std::string getProteinDescription(const char * accession, const int dbIdx = 1) const;

            //! Return protein mass if available.
            double getProteinMass(const char * accession, const int dbIdx = 1) const;

            //! Return the mass of a sequence (protein or peptide).
            double getSequenceMass(const char * seq) const;

            //! Return the taxonomy ID(s), if any, from the results file
            void getProteinTaxonomyIDs(const char * accession, const int dbIdx,
                                       std::vector<int> & gpt_ids, std::vector<std::string> & gpt_accessions) const;

            //! Return a pointer to the protein entry given an accession.
            virtual const ms_protein * getProtein(const char * accession, const int dbIdx = 1) const;

            //! Return a pointer to the protein entry given an accession.
            virtual const ms_protein * getComponentProtein(const char * accession, const int dbIdx = 1) const;

            //! Return the next protein that contains all the peptides in the 'master' protein.
            virtual ms_protein * getNextSimilarProtein(const int masterHit, const int id) const;

            //! Return the next protein that contains all the peptides in the 'master' protein.
            virtual ms_protein * getNextSimilarProteinOf(const char * masterAccession, const int masterDB, const int id) const;

            //! Find the next protein in the family \c masterHit. 
            virtual ms_protein * getNextFamilyProtein(const int masterHit, const int id) const;


            //! Return the next protein that contains some of the peptides in the 'master' protein.
            virtual ms_protein * getNextSubsetProtein(const int masterHit, const int id,
                                                      const bool searchWholeFamily = true) const;

            //! Return the next protein that contains some of the peptides in the 'master' protein.
            virtual ms_protein * getNextSubsetProteinOf(const char * masterAccession, const int masterDB, const int id) const;

            //! Return the ms_peptide object given the query and either the rank (ms_peptidesummary) or the hit (ms_proteinsummary).
            virtual ms_peptide getPeptide(const int q, const int p) const = 0;

            //! Return the ms_peptide object given the query and either the rank (ms_peptidesummary) or the hit (ms_proteinsummary).
            virtual bool getPeptide(const int q, const int p, ms_peptide * & pep) const = 0;

            //! Returns true if this peptide match is unique to one protein or one protein family.
            virtual bool isPeptideUnique(const int q, const int p, const UNIQUE_PEP_RULES rules = UPR_DEFAULT) const = 0;

            //! Return the number of peptides with masses that matched this query.
	        virtual int getQmatch(const int query) const;

            //! Return the threshold value for this ms-ms data being a random match.
	        virtual int getPeptideIdentityThreshold(const int query, double OneInXprobRnd) const;

            //! Return the average threshold value for all MS-MS data sets.
            virtual int getAvePeptideIdentityThreshold(double OneInXprobRnd) const;

            //! Return the max threshold value for all MS-MS data sets.
            virtual int getMaxPeptideIdentityThreshold(double OneInXprobRnd) const;

            //! Return either the identity or the homology threshold.
            double getPeptideThreshold(const int query, double OneInXprobRnd, const int rank=1, const THRESHOLD_TYPE thresholdType=TT_PEPSUM_DEFAULT) const;

            enum ERROR_TOLERANT_PEPTIDE { ETPEP_YES, ETPEP_NO, ETPEP_UNKNOWN };
#ifndef SWIG
            double getPeptideThresholdProtected(const int query, double OneInXprobRnd, const int rank,
                                                const ERROR_TOLERANT_PEPTIDE etPep,
                                                const ms_mascotresfile::section secSummary,
                                                const THRESHOLD_TYPE thresholdType) const;
#endif

            //! Return the 'protein' score value for cutting off results (different for peptide and protein summary).
            virtual int getProteinScoreCutoff(double OneInXprobRnd) const = 0;

            //! Return a threshold value for the protein summary report.
            virtual int getProteinThreshold(double OneInXprobRnd) const;

            //! Returns the 'homology' threshold.
            virtual int getHomologyThreshold(const int query,
                                             double OneInXprobRnd,
                                             const int rank=1) const;

            //! Returns the value for the 'yellow section' in the histogram.
            virtual int getHomologyThresholdForHistogram(double OneInXprobRnd) const;

            //! Returns a probability value given a score.
            virtual int getProbFromScore(const double score) const;

            //! Returns the expectation value for the given peptide score and query.
            virtual double getPeptideExpectationValue(const double score, 
                                                      const int query) const;

            //! \deprecated Use getPeptideExpectationValue().
            virtual double getProbOfPepBeingRandomMatch(const double score, 
                                                        const int query) const;

            //! Returns the expectation value for the given protein score.
            virtual double getProteinExpectationValue(const double score) const;

            //! \deprecated Use getProteinExpectationValue().
            virtual double getProbOfProteinBeingRandomMatch(const double score) const;

            //! Return a partial list of proteins that matched the same peptide.
            virtual std::string getProteinsWithThisPepMatch(const int q, const int p, const bool quotes=false) = 0;

            //! Return a complete list of proteins that contain this same peptide match.
            virtual std::vector<std::string> getAllProteinsWithThisPepMatch(const int q, const int p, 
                                                                            std::vector<int> & start, 
                                                                            std::vector<int> & end,
                                                                            std::vector<std::string> &pre,
                                                                            std::vector<std::string> &post,
                                                                            std::vector<int> & frame,
                                                                            std::vector<int> & multiplicity,
                                                                            std::vector<int> & db) const = 0;

            //! Return a list of (top level) family proteins that have a match to the specified q and p.
            virtual int getAllFamilyMembersWithThisPepMatch(const int hit,
                                                            const int q,
                                                            const int p,
                                                            std::vector< int >& db,
                                                            std::vector< std::string >& acc,
                                                            std::vector< int >& dupe_status) const = 0;


            //! Return the complete error tolerant mod string from \c h1_q2_et_mods or \c q1_p1_et_mods.
            virtual std::string getErrTolModString(const int q, const int p) const = 0;

            //! Return the error tolerant mod primary neutral loss string from \c h1_q2_et_mods_master or \c q1_p1_et_mods_master.
            virtual std::string getErrTolModMasterString(const int q, const int p) const = 0;

            //! Return the error tolerant mod slave neutral loss string from \c h1_q2_et_mods_slave or \c q1_p1_et_mods_slave.
            virtual std::string getErrTolModSlaveString(const int q, const int p) const = 0;

            //! Return the error tolerant mod peptide neutral loss string from \c h1_q2_et_mods_pep or \c q1_p1_et_mods_pep.
            virtual std::string getErrTolModPepString(const int q, const int p) const = 0;

            //! Return the error tolerant mod required peptide neutral loss string from \c h1_q2_et_mods_reqpep or \c q1_p1_et_mods_reqpep.
            virtual std::string getErrTolModReqPepString(const int q, const int p) const = 0;

            //! Return the error tolerant mod name from \c h1_q2_et_mods or \c q1_p1_et_mods.
            virtual std::string getErrTolModName(const int q, const int p) const;

            //! Return the error tolerant mod delta from \c h1_q2_et_mods or \c q1_p1_et_mods.
            virtual double getErrTolModDelta(const int q, const int p) const;

            //! Return the error tolerant mod neutral loss from \c h1_q2_et_mods or \c q1_p1_et_mods.
            virtual double getErrTolModNeutralLoss(const int q, const int p) const;

            //! Return the error tolerant mod additional primary neutral losses from \c h1_q2_et_mods_master or \c q1_p1_et_mods_master.
            virtual std::vector<double> getErrTolModMasterNeutralLoss(const int q, const int p) const;

            //! Return the error tolerant mod slave neutral losses from \c h1_q2_et_mods_slave or \c q1_p1_et_mods_slave.
            virtual std::vector<double> getErrTolModSlaveNeutralLoss(const int q, const int p) const;

            //! Return the error tolerant mod peptide neutral losses from \c h1_q2_et_mods_pep or \c q1_p1_et_mods_pep.
            virtual std::vector<double> getErrTolModPepNeutralLoss(const int q, const int p) const;

            //! Return the error tolerant mod peptide neutral losses from \c h1_q2_et_mods_reqpep or \c q1_p1_et_mods_reqpep.
            virtual std::vector<double> getErrTolModReqPepNeutralLoss(const int q, const int p) const;

            //! Return a 'human readable' string with the variable and error tolerant mods.
            virtual std::string getReadableVarMods(const int q, const int p,
                                                   const int numDecimalPlaces=2) const;

            //! Return the complete tag string from \c h1_q2_tag or \c q1_p1_tag.
            virtual std::string getTagString(const int q, const int p) const = 0;

            //! Return the start position for the tag-match from \c h1_q2_tag or \c q1_p1_tag.
            virtual int getTagStart(const int q, const int p, const int tagNumber) const;

            //! Return the end position for the tag-match from \c h1_q2_tag or \c q1_p1_tag.
            virtual int getTagEnd(const int q, const int p, const int tagNumber) const;

            //! Return the series ID for the tag-match from \c h1_q2_tag or \c q1_p1_tag.
            virtual int getTagSeries(const int q, const int p, const int tagNumber) const;

            //! Return the first number from <code>h1_q2_drange=0,256</code>.
            virtual int getTagDeltaRangeStart(const int q, const int p) const = 0;

            //! Return the second number from <code>h1_q2_drange=0,256</code>.
            virtual int getTagDeltaRangeEnd(const int q, const int p) const = 0;

            //! Return the complete terminal residue string from \c h1_q1_terms or \c q1_p1_terms.
            virtual std::string getTerminalResiduesString(const int q, const int p) const = 0;

            //! Return \c q1_p2_comp string value; for \c h1_q2 this string is always empty.
            virtual std::string getComponentString(const int q, const int p) const = 0;

            //! Returns the maximum 'rank' or 'hit' or 'p' value.
            virtual int getMaxRankValue() const;

            //! Returns a list of counts for binned ions scores.
            virtual std::vector<int> getIonsScoreHistogram(IONS_HISTOGRAM flags = IH_INCLUDE_TOP_MATCHES) const;

            //! To have a list of unassigned peptides, need to call this first.
            bool createUnassignedList(sortBy s  = QUERY);

            //! Return the number of peptides in the unassigned list.
            int getNumberOfUnassigned() const;

            //! Need to call createUnassignedList() before calling this.
            ms_peptide getUnassigned(const int num) const;

            //! Returns true if the item indexed by num in the assigned list should be bold.
            bool getUnassignedIsBold(const int num) const;

            //! Returns true if the item indexed by num in the assigned list should have a check box next to it.
            bool getUnassignedShowCheckbox(const int num) const;

            //! Return distances and structure suitable for a dendrogram plot.
            virtual bool getTreeClusterNodes(const int hit,
                                             std::vector<int>    &left, 
                                             std::vector<int>    &right, 
                                             std::vector<double> &distance,
                                             TREE_CLUSTER_METHOD  tcm = TCM_PAIRWISE_MAXIMUM,
                                             double           *** reserved1 = 0,
                                             unsigned int       * reserved2 = 0) const;

            //! Find the next hit that contains proteins with the specified attributes
            virtual int findProteins(const int startHit, 
                                     const std::string & str, 
                                     const int dbIdx,
                                     FIND_FLAGS item,
                                     FIND_COMPARE_FLAGS compareFlags,
                                     std::vector<std::string> & accessions,
                                     std::vector<int> & dbIndexes) const = 0;

            //! Find the next hit that contains proteins with the specified accession.
            virtual int findProteinsByAccession(const int startHit, 
                                                const std::string & str, 
                                                const int dbIdx,
                                                FIND_COMPARE_FLAGS compareFlags,
                                                std::vector<std::string> & accessions,
                                                std::vector<int> & dbIndexes) const = 0;

            //! Find the next hit that contains proteins with the specified description.
            virtual int findProteinsByDescription(const int startHit, 
                                                  const std::string & str, 
                                                  FIND_COMPARE_FLAGS compareFlags,
                                                  std::vector<std::string> & accessions,
                                                  std::vector<int> & dbIndexes) const = 0;

            //! Find the next hit that contains peptides with the specified attribute.
            virtual int findPeptides(const int startHit, 
                                     const std::string & str, 
                                     FIND_FLAGS item,
                                     FIND_COMPARE_FLAGS compareFlags,
                                     std::vector<int> & q,
                                     std::vector<int> & p) const = 0;

#ifndef SWIG
            void addProtein(const std::string & accession, 
                            const int dbIdx,
                            const int frame,
                            const long start, const long end, 
                            const long multiplicity,
                            const int q, const int p,
                            const double score,
                            const double uncorrectedScore,
                            const char residueBefore,
                            const char residueAfter,
                            const ms_protein * component = 0,
                            const bool integratedET = false,
                            const bool isUnigene = false);

            const ms_protein * addComponentProtein(const std::string & accession, 
                                                   const int dbIdx,
                                                   const int frame,
                                                   const long start, 
                                                   const long end, 
                                                   const long multiplicity,
                                                   const int q, const int p,
                                                   const double score,
                                                   const double uncorrectedScore,
                                                   const char residueBefore,
                                                   const char residueAfter,
                                                   const ms_protein * component = 0,
                                                   const bool integratedET = false);
#endif
            //! Returns scores for top 50 proteins, even if less in the peptidesummary or proteinsummary.
            virtual double getProteinScoreForHistogram(const int num) const;

            //! Returns TRUE for a search against a nucelic acid database.
            bool isNA() const;

            //! Returns the \a flags value passed to the constructor
            /*! 
             * \return the ms_mascotresults::FLAGS value.
             */
            unsigned int getFlags() const { return flags_; }

            //! Return the \a flags2 value passed to the ms_peptidesummary constructor.
            /*! For an ms_proteinsummary, will always return 0 as there is no 
             *  option to set the flags2 value.
             *  \return the ms_peptidesummary::MSPEPSUM value.
             */
            unsigned int getFlags2() const { return flags2_; }

            //! Peptides shorter than this are ignored when putting proteins into groups.
            int getMinPepLenInPepSummary() const;

            //! Return the number of hits with a score at or above the identity threshold.
            virtual long getNumHitsAboveIdentity(double OneInXprobRnd);

            //! Return the number of hits from the decoy search with a score at or above the identity threshold.
            virtual long getNumDecoyHitsAboveIdentity(double OneInXprobRnd);

            //! Return the number of hits with a score at or above the homology threshold.
            virtual long getNumHitsAboveHomology(double OneInXprobRnd);

            //! Return the number of hits from the decoy search with a score at or above the homology threshold.
            virtual long getNumDecoyHitsAboveHomology(double OneInXprobRnd);

            //! Specifies which subset proteins should be reported.
            virtual void setSubsetsThreshold(const double scoreFraction);

            //! Return the minProbability value passed to the ms_mascotresults::ms_mascotresults constructor .
            virtual double getProbabilityThreshold() const;

            //! Given a target FDR, return the probability threshold to use with the constructor that gives an FDR closest to it above the identity threshold.
            bool getThresholdForFDRAboveIdentity(double targetFDR, double *closestFDR, double *minProbability);

            //! Given a target FDR, return the probability threshold to use with the constructor that gives an FDR closest to it above the identity or homology threshold.
            bool getThresholdForFDRAboveHomology(double targetFDR, double *closestFDR, double *minProbability);

            //! Returns a list of 'p' values for peptides with the same score.
            virtual std::vector<int> getPepsWithSameScore(const int q, const int p) const = 0;

            /*! \internal
             *
             * See \ref duplicatesPage.
             *
             * Pass a value with zero or more of the flags 
             *
             * <ul>
             * <li>ms_mascotresults::dupe_query_same</li>
             * <li>ms_mascotresults::dupe_seq_same</li>
             * <li>ms_mascotresults::dupe_mods_same</li>
             * <li>ms_mascotresults::dupe_pos_same</li>
             * </ul>
             *
             * bitwise OR'd together. 
             *
             * This function determines if this peptide should be removed from
             * the list of peptides in a protein.
             */
            bool queryRemoveThisPeptide(const unsigned short dupeFlags) const { return dupeRemoveIDs_.find(dupeFlags) != dupeRemoveIDs_.end(); }

            /*! \internal
             *
             * See \ref duplicatesPage.
             *
             * Pass a value with zero or more of the flags 
             *
             * <ul>
             * <li>ms_mascotresults::dupe_query_same</li>
             * <li>ms_mascotresults::dupe_seq_same</li>
             * <li>ms_mascotresults::dupe_mods_same</li>
             * <li>ms_mascotresults::dupe_pos_same</li>
             * </ul>
             *
             * bitwise OR'd together. 
             *
             * This function determines if this peptide should be used to add
             * to the score of peptides in a protein.
             */
            bool queryScoreThisPeptide(const unsigned short dupeFlags) const { return dupeIncludeInScoreIDs_.find(dupeFlags) != dupeIncludeInScoreIDs_.end(); }

            //! \internal
            virtual bool loadPepMatchesForProteinFromCache(ms_protein * prot) { return false; }

            //! \internal
            virtual bool isValidQandP(const int q, const int p) const = 0;

        protected:
            // Not safe to copy or assign this object.
#ifndef SWIG
            ms_mascotresults(const ms_mascotresults & rhs);
            ms_mascotresults & operator=(const ms_mascotresults & rhs);

            virtual void calculateDecoyStats(double dOneInXprobRnd) = 0;

            typedef std::vector< double > FDREntries_t;

            virtual void collectExpectValuesForFDR(FDREntries_t *entries, bool is_decoy, bool use_homology) = 0;

            virtual double getPeptideExpectationValueProtected(const double score, 
                                                               const int query,
                                                               const ms_mascotresfile::section summary_section,
                                                               const THRESHOLD_TYPE thresholdType) const;
#endif

            ms_mascotresfile &resfile_;
            int   numQueries_;
            double tolFactor_;

            proteinSet proteins_;

            // For unigene, the original proteins are not saved in proteins_
            proteinSet componentProteins_;

            // - Not documented - Get the 'corrected' ions score given multiplicity
            double getIonsScoreCorrected(const double ionsScore, 
                                         const long   multiplicity) const;

            unsigned int      flags_;
            unsigned int      flags2_;
            double            minProbability_;
            int               maxHitsToReport_;
            std::string       unigeneIndexFile_;
            ms_unigene      * unigene_;
            bool              tooOld_;
            int               minPepLenInPepSummary_;
            std::string       singleHit_;


            // The elements in the vector of peptides are accessed by
            // q + (p * num queries)
            std::vector<ms_peptide *> peptides_;

            //! Return the threshold value for this ms-ms data being a random match.
	        virtual double getPepIdentThreshProtected(const int query, 
                                                      double OneInXprobRnd,
                                                      ms_mascotresfile::section sec,
                                                      double * pQmatch = 0) const;
            virtual double getHomologyThreshProtected(const int query,
                                                      double OneInXprobRnd,
                                                      ms_mascotresfile::section sec,
                                                      const int rank=1,
                                                      const ERROR_TOLERANT_PEPTIDE etPep = ETPEP_UNKNOWN ) const;

            inline bool checkCreated(const char * funcname, unsigned int t) const {
                if (!(completedTasks_ & t)) {
                    resfile_.setError(ms_mascotresfile::ERR_RESULTS_NOT_CREATED, funcname);
                    return false;
                } else {
                    return true;
                }
            }


            msparser_internal::ms_unassigned * unassigned_;

            double top50Scores_[50];
            std::set<unsigned short>dupeRemoveIDs_;
            std::set<unsigned short>dupeIncludeInScoreIDs_;

            bool bDecoyStatsCalculated_;
            double dOneInXprobRndForDecoy_;
            long numHitsAboveIdentity_;
            long numDecoyHitsAboveIdentity_;
            long numHitsAboveHomology_;
            long numDecoyHitsAboveHomology_;
            ms_mascotresfile::section secSummary_;
            ms_mascotresfile::section secMixture_;
            ms_mascotresfile::section secPeptides_;
            ms_mascotresfile::section secProteins_;
            double subsetsScoreFraction_;
            msparser_internal::ms_proteininference * pProteinInferencer_;
            bool nucleicAcid_;
            mutable int cachedAvePepIdentThresh_;
            std::vector<int> ionsScoreHistogramTopMatch_;
            std::vector<int> ionsScoreHistogramTop10_;
            int maxRankValue_;
            bool isPercolator_;
            THRESHOLD_TYPE thresholdType_;
            bool cancelCreateSummary_;

            enum COMPLETED_TASKS {
                CT_NONE                 = 0x0000,
                CT_LOADQUERIES          = 0x0001,
                CT_SRCRANKINITIALISED   = 0x0002,
                CT_PERCOLATORRESULTS    = 0x0004,
                CT_INFERENCING          = 0x0008,
                CT_UNASSIGNEDLIST       = 0x0010,
                CT_CREATECDB            = 0x0020,
                CT_ALLDONE              = 0xFFFF
            };
            mutable unsigned int completedTasks_;

            /* These need to be protected, not private, so that 
             * ms_peptidesummary can populate them from the cache.
             */
            typedef std::map< double, double > FDRtoExpect_t;
            mutable bool cachedFDRtoExpect_[2];
            mutable FDRtoExpect_t cachedFDRtoExpectTable_[2];

        private:
            mutable bool   cachedHomology_[2];
            mutable double cachedHomologyProb_[2];
            mutable std::vector<double> cachedHomologyValues_[2];
            mutable std::vector<int> cachedQMatch_[3];
            typedef std::map<std::pair<int, std::string>, int> dbIdxPlusAccToId_t;
            mutable dbIdxPlusAccToId_t summarySectionAccs_;

            bool getProteinDescriptionAndMass(const char * accession, const int dbIdx,
                                              double & mass, std::string & desc) const;
            int getQmatch(const int query, const ms_mascotresfile::section sec) const;
            void debugCheckReloadablePeps() const;

            void calculateThresholdForFDR(FDRtoExpect_t &FDRs, bool useHomology);
            bool findThresholdForFDR(double targetFDR, const FDRtoExpect_t &values, double *closestFDR, double *minProbability);
    };
    /** @} */ // end of resfile_group
}   // matrix_science namespace

#endif // MS_MASCOTRESULTS_HPP

/*------------------------------- End of File -------------------------------*/
