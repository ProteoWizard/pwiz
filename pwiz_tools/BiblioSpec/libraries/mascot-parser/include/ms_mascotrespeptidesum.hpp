/*
##############################################################################
# file: ms_mascotrespeptidesum.hpp                                           #
# 'msparser' toolkit                                                         #
# Encapsulates the peptide summary report from the mascot results file       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2002 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotrespe $ #
#     $Author: davidc $ #
#       $Date: 2011-07-20 15:23:03 $ #
#   $Revision: 1.63 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASCOTRESPEPTIDESUM_HPP
#define MS_MASCOTRESPEPTIDESUM_HPP

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

#ifdef __ALPHA_UNIX__
#include <ctype.h>
#endif

// Includes from the standard template library
#include <string>
#include <list>
#include <vector>
#include <set>

namespace msparser_internal {
    class ms_peptidesumcdb;
    class ms_peptide_impl;
    class ms_peptide_impl_reloadable;
}

namespace matrix_science {
    class ms_tinycdb;
    class ms_protein;

    /** @addtogroup resfile_group
     *  
     *  @{
     */

    //! Use this class to get peptide summary results.
    /*! 
     * @copydetails matrix_science::ms_mascotresults
     *
     * This class inherits from ms_mascotresults, and all the class functions 
     * except for getPeptide() are documented in ms_mascotresults.
     */
    class MS_MASCOTRESFILE_API ms_peptidesummary : public matrix_science::ms_mascotresults
    {
        friend class msparser_internal::ms_peptide_impl;
        friend class ms_protein;
        public:
            //! Flags for getQueryList().
            /*!
             * See \ref DynLangEnums and getQueryList().
             */
            enum QL_FLAG
            { 
                QL_FIRST                    = 0x0000, 
                QL_ALL 	                    = 0x0000, //!< Returns "All" .
                QL_UNASSIGNED 	            = 0x0001, //!< Returns a comma separated list of the query numbers in the unassigned list. If createUnassignedList() has not been called, getQueryList() function will call it, possibly causing some delay.
                QL_BELOW_IDENTITY 	        = 0x0002, //!< Returns a comma separated list of the query numbers which have scores below the identity threshold calculated using the \a minProbability threshold specified in the ms_peptidesummary constructor. If \a minProbability <= 0 or >= 1, then an empty string is returned.
                QL_BELOW_HOMOLOGY 	        = 0x0003, //!< Returns a comma separated list of the query numbers which have scores below the homology threshold calculated using the \a minProbability threshold specified in the ms_peptidesummary constructor. If \a minProbability <= 0 or >= 1, then an empty string is returned.
                QL_IGNORE_IONS_SCORE_BELOW 	= 0x0004, //!< Uses the threshold specified by the \a ignoreIonsScoreBelow parameter. If \a ignoreIonsScoreBelow is zero, then an empty string is returned. 

                QL_LAST                     = 0x0004  //!< For looping through all possible values.
            };

            //! \a flags2 for ms_peptidesummary introduced in Mascot Parser 2.3.
            /*!
             * See \ref DynLangEnums and ms_peptidesummary::ms_peptidesummary.
             */
            enum MSPEPSUM
            { 
                MSPEPSUM_NONE               = 0x0000, //!< Default. 
                MSPEPSUM_PERCOLATOR         = 0x0001, //!< See \ref Percolator for details.
                MSPEPSUM_USE_CACHE          = 0x0002, //!< See \ref Caching_peptide_summary.
                MSPEPSUM_SINGLE_HIT_DBIDX   = 0x0004, //!< The singleHit parameter string must start with a database index and a colon, e.g. <code>3:CH60_SHEON</code>. See \ref SingleHit.
                MSPEPSUM_USE_HOMOLOGY_THRESH= 0x0008, //!< Expect values and cutoffs will use homology thresholds rather than identity thresholds.
                MSPEPSUM_NO_PROTEIN_GROUPING= 0x0010, //!< Used for when a ms_peptidesummary object it required, but no protein grouping is needed. ms_peptidesummary::getNumberOfHits() will return zero. The only functions that are guaranteed to work are ms_peptidesummary::getPeptide(), ms_peptidesummary::getAllProteinsWithThisPepMatch(), ms_peptidesummary::getQmatch(). Will not work with error tolerant searches.
                MSPEPSUM_DISCARD_RELOADABLE = 0x0020, //!< Specify this flag to use less memory. However, calls to getPeptide() will be slower because the data wll always be loaded again from the results file.
                MSPEPSUM_DEFERRED_CREATE    = 0x0040  //!< Useful if it is necessary to be able to cancel creation of a ms_peptidesummary() object (which can take a long time). Call createSummary() manually after creating the ms_peptidesummary object
            };

            //! Call this constructor once to create peptide summary results.
            ms_peptidesummary(ms_mascotresfile  &resfile,
                              const unsigned int flags = MSRES_GROUP_PROTEINS,
                              double             minProbability = 0.0,
                              int                maxHits = 50,
                              const char *       unigeneIndexFile = 0,
                              double             ignoreIonsScoreBelow = 0.0,
                              int                minPepLenInPepSummary = 0,
                              const char *       singleHit = 0,
                              const unsigned int flags2 = MSPEPSUM_NONE);
            virtual ~ms_peptidesummary();
            
            //! Create the summary using a separate call after the ms_peptidesummary object has been created
            virtual bool createSummary();

            //! Return the ms_protein hit - returns null if \a hit > number of hits.
            virtual ms_protein * getHit(const int hit) const;

            //! Frees any memory associated with the passed hit number
            virtual void freeHit(const int hit);

            // This is 'hard-coded' to 10 in Mascot
            enum { PEPS_PER_QUERY = 10 };

            //! Return a peptide object for the specified query / rank.
            virtual ms_peptide getPeptide(const int q, const int p) const;

            //! Returns true if this peptide match is unique to one protein or one protein family.
            virtual bool isPeptideUnique(const int q, const int p, const UNIQUE_PEP_RULES rules = UPR_DEFAULT) const;

            //! Return a peptide object for the specified query / rank.
            virtual bool getPeptide(const int q, const int p, ms_peptide * & pep) const;

            //! Return a partial list of proteins that matched the same peptide.
            virtual std::string getProteinsWithThisPepMatch(const int q, const int p, const bool quotes=false);

            //! Returns a complete list of all the accessions that contained the peptide matched by this result. 
            virtual std::vector<std::string> getAllProteinsWithThisPepMatch(const int q, const int p, 
                                                                            std::vector<int> & start, 
                                                                            std::vector<int> & end,
                                                                            std::vector<std::string> &pre,
                                                                            std::vector<std::string> &post,
                                                                            std::vector<int> & frame,
                                                                            std::vector<int> & multiplicity,
                                                                            std::vector<int> & db) const;

            //! Return a list of (top level) family proteins that have a match to the specified q and p.
            virtual int getAllFamilyMembersWithThisPepMatch(const int hit,
                                                            const int q,
                                                            const int p,
                                                            std::vector< int >& db,
                                                            std::vector< std::string >& acc,
                                                            std::vector< int >& dupe_status) const;

            //! Return the complete error tolerant mod string from \c h1_q2_et_mods or \c q1_p1_et_mods.
            virtual std::string getErrTolModString(const int q, const int p) const;

            //! Return the complete error tolerant mod master neutral loss string from \c q1_p1_et_mods_master.
            virtual std::string getErrTolModMasterString(const int q, const int p) const;

            //! Return the complete error tolerant mod slave neutral loss string from \c q1_p1_et_mods_slave.
            virtual std::string getErrTolModSlaveString(const int q, const int p) const;

            //! Return the complete error tolerant mod peptide neutral loss string from \c q1_p1_et_mods_pep.
            virtual std::string getErrTolModPepString(const int q, const int p) const;

            //! Return the complete error tolerant mod required peptide neutral loss string from \c q1_p1_et_mods_reqpep.
            virtual std::string getErrTolModReqPepString(const int q, const int p) const;

            //! Return the complete tag string from \c q1_p1_tag.
            virtual std::string getTagString(const int q, const int p) const;

            //! Return the first number from <code>q1_p2_drange=0,256</code>.
            virtual int getTagDeltaRangeStart(const int q, const int p) const;

            //! Return the second number from <code>q1_p2_drange=0,256</code>.
            virtual int getTagDeltaRangeEnd(const int q, const int p) const;

            //! Return the complete terminal residue string from \c q1_p1_terms.
            virtual std::string getTerminalResiduesString(const int q, const int p) const;

            //! Return \c q1_p2_comp string value.
            virtual std::string getComponentString(const int q, const int p) const;

            //! Return the 'protein' score value for cutting off results. Different for peptide and protein summary.
            virtual int getProteinScoreCutoff(double OneInXprobRnd) const;

            //! Returns the 'source' rank for a given peptide match.
            int getSrcRank(int q, int p) const;

            //! Returns the 'source' section for a given peptide match.
            ms_mascotresfile::section getSrcSection(int q, int p)const;

            //! Returns a list of query numbers that can be used for a repeat search.
            std::string getQueryList(QL_FLAG flag, bool outputListOfQueries = true);

            //! Returns a list of 'p' values for peptides with the same score.
            virtual std::vector<int> getPepsWithSameScore(const int q, const int p) const;

            //! Find the next hit that contains proteins with the specified attributes
            virtual int findProteins(const int startHit, 
                                     const std::string & str, 
                                     const int dbIdx,
                                     FIND_FLAGS item,
                                     FIND_COMPARE_FLAGS compareFlags,
                                     std::vector<std::string> & accessions,
                                     std::vector<int> & dbIndexes) const;

            //! Find the next hit that contains proteins with the specified accession.
            virtual int findProteinsByAccession(const int startHit, 
                                                const std::string & str, 
                                                const int dbIdx,
                                                FIND_COMPARE_FLAGS compareFlags,
                                                std::vector<std::string> & accessions,
                                                std::vector<int> & dbIndexes) const;

            //! Find the next hit that contains proteins with the specified description.
            virtual int findProteinsByDescription(const int startHit, 
                                                  const std::string & str, 
                                                  FIND_COMPARE_FLAGS compareFlags,
                                                  std::vector<std::string> & accessions,
                                                  std::vector<int> & dbIndexes) const;

            //! Find the next hit that contains peptides with the specified attribute.
            virtual int findPeptides(const int startHit, 
                                     const std::string & str, 
                                     FIND_FLAGS item,
                                     FIND_COMPARE_FLAGS compareFlags,
                                     std::vector<int> & q,
                                     std::vector<int> & p) const;

            //! Return a pointer to the protein entry given an accession.
            virtual const ms_protein * getProtein(const char * accession, const int dbIdx = 1) const;

            //! Return a pointer to the protein entry given an accession.
            virtual const ms_protein * getComponentProtein(const char * accession, const int dbIdx = 1) const;

            //! Return distances and structure suitable for a dendrogram plot.
            virtual bool getTreeClusterNodes(const int hit,
                                             std::vector<int>    &left, 
                                             std::vector<int>    &right, 
                                             std::vector<double> &distance,
                                             TREE_CLUSTER_METHOD  tcm = TCM_PAIRWISE_MAXIMUM,
                                             double           *** reserved1 = 0,
                                             unsigned int       * reserved2 = 0) const;

            //! Returns true if a cache file will be created when the ms_peptidesummary constructor is called.
            static bool willCreateCache(ms_mascotresfile  &resfile,
                                        const unsigned int flags = MSRES_GROUP_PROTEINS,
                                        double             minProbability = 0.0,
                                        int                maxHits = 50,
                                        const char *       unigeneIndexFile = 0,
                                        double             ignoreIonsScoreBelow = 0.0,
                                        int                minPepLenInPepSummary = 0,
                                        const char *       singleHit = 0,
                                        const unsigned int flags2 = MSPEPSUM_NONE);

            //! Returns the filename of the cache file.
			std::string getCacheFileName() const;

            //! \internal
            virtual bool loadPepMatchesForProteinFromCache(ms_protein * prot);

            // This is just for each rank 1 peptide
            struct percolator_t {
                double percolatorScore; 
                double qValue;
                double percolatorPEP;
                double mascotScore;
                percolator_t() { percolatorScore = 0; qValue = 0; percolatorPEP = 0; mascotScore = 0; }
            };

            //! \internal
            virtual bool isValidQandP(const int q, const int p) const;

            //! \internal
            bool dumpCDB(const std::string dumpFileName);

        protected:  
            // Not safe to copy or assign this object.
#ifndef SWIG
            ms_peptidesummary(const ms_peptidesummary & rhs);
            ms_peptidesummary & operator=(const ms_peptidesummary & rhs);
#endif
            void calculateDecoyStats(double dOneInXprobRnd);

            void collectExpectValuesForFDR(FDREntries_t *entries, bool is_decoy, bool use_homology);

        private:
            void loadQuery(int q, acc_dbidx_set_t * pAccessions = 0);

            ms_peptide * loadPepRes(const ms_mascotresfile::section sec,
                                    int q, int p, int rank,
                                    std::string::size_type & idx,
                                    bool loadAccessions,
                                    acc_dbidx_set_t * pAccessions = 0,
                                    msparser_internal::ms_peptide_impl_reloadable * * pReloadable = 0);
            void loadIntoProteins(const ms_mascotresfile::section sec,
                                  std::string str,
                                  std::string strDB,
                                  std::string::size_type idx,
                                  int q, int p, int rank,
                                  double ionsScore,
                                  acc_dbidx_set_t * pAccessions);
            double            ignoreIonsScoreBelow_;
            int               singleHitDbIdx_;

            // Some private variables and functions for error tolerant search
            bool checkErrorTolerantStatus(int q, int p, double ionsScore, 
                                          bool fromET,
                                          const std::string & pepStr);
            ms_mascotresfile  * errTolSource_;
            ms_peptidesummary * errTolPepSummary_;
            unsigned int        errTolType_;
            bool                missingErrTolParent_;
            std::vector<unsigned char> srcRank_;
            bool srcRankInitialised_;
            std::vector<short> offsetToAccession_;

            void setSrcRank(int q, int p, int srcRank, ms_mascotresfile::section peptideSec, bool rejected);
            int getSrcRank(int q, int p, ms_mascotresfile::section & peptideSec, bool * pRejected = 0) const;
            enum PEP_SECTIONS { PEP_SEC_INVALID         = 0, 
                                PEP_SEC_PEPTIDES        = 1, 
                                PEP_SEC_DECOYPEPTIDES   = 2, 
                                PEP_SEC_ERRTOLPEPTIDES  = 3, 
                                PEP_SEC_LAST            = 4};
            // For each of these, use SR_MASK_ and then, if required, >> SR_SHIFT_
            enum SRC_RANK_MASKS { SR_MASK_RANK      = 0x0F,  // bits 0..3
                                  SR_MASK_SEC       = 0x30,  // bits 4..5
                                  SR_MASK_REJECT    = 0x40,  // bit  6     If discarded because below threshold or other rule
                                  SR_MASK_UNUSED    = 0x80,  // bit  7
                                  SR_SHIFT_RANK     = 0x00,  // bits 0..3 - no need to shift
                                  SR_SHIFT_SEC      = 0x04,  // bits 4..5
                                  SR_SHIFT_REJECT   = 0x06,  // bit  6     If discarded because below threshold or other rule
                                  SR_SHIFT_UNUSED   = 0x07}; // bit  7
            unsigned char secLookupFwd_[ms_mascotresfile::SEC_NUMSECTIONS];
            int secLookupRev_[PEP_SEC_LAST];

            void getUnassignedListAsString(std::string & str);
            bool findCompareProtein(const std::string & accRequired, 
                                    const std::string & accToTest, 
                                    const int dbIdxRequired,
                                    const int dbIdxToTest,
                                    FIND_FLAGS item,
                                    FIND_COMPARE_FLAGS compareFlags,
                                    const ms_protein * prot,
                                    const std::vector<int> & q, 
                                    const std::vector<int> & p) const;
            bool findCompare(const std::string & find, 
                             const std::string & findIn, 
                             FIND_COMPARE_FLAGS compareFlags) const;

            struct distanceInfo_t {
                double ionsScore;
	            double scoreThreshold;
                std::string peptideStr;
                distanceInfo_t() { ionsScore = 0; scoreThreshold = 0; }
                distanceInfo_t(double sc, double thresh, std::string & pep) { ionsScore = sc; scoreThreshold = thresh; peptideStr = pep; }
            };
            typedef std::map<std::pair<int, int>, distanceInfo_t> distanceInfoMap_t;
            double getDistance(const distanceInfoMap_t & a, const distanceInfoMap_t & b) const;
            bool readPercolatorOutputFile(bool decoyResults,
                                          std::vector<percolator_t> * pVector = 0,
                                          long * pNumAboveIdentity = 0, 
                                          double threshold = 0.05);

            typedef std::set<std::pair<int,int> > hitAndFamily_t;
            bool getHitAndFamilyMember(const ms_protein * prot, hitAndFamily_t & hitAndFamily, const UNIQUE_PEP_RULES rules) const;

            std::vector<percolator_t> percolatorScores_;

            msparser_internal::ms_peptidesumcdb * pCacheFile_;
            ms_tinycdb  * pTmpCache_;

    };
    /** @} */ // end of resfile_group
}   // matrix_science namespace

#endif // MS_MASCOTRESPEPTIDESUM_HPP

/*------------------------------- End of File -------------------------------*/
