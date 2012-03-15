/*
##############################################################################
# file: ms_mascotresproteinsum.cpp                                           #
# 'msparser' toolkit                                                         #
# Encapsulates the protein summary report from the mascot results file       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2002 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotrespr $ #
#     $Author: davidc $ #
#       $Date: 2011-07-20 15:23:03 $ #
#   $Revision: 1.34 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASCOTRESPROTEINSUM_HPP
#define MS_MASCOTRESPROTEINSUM_HPP

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


namespace matrix_science {
    /** @addtogroup resfile_group
     *  
     *  @{
     */
    /*! Use this class to get protein summary results.
     *
     * \copydetails matrix_science::ms_mascotresults
     *
     * This class inherits from ms_mascotresults, and all the class functions 
     * except for getPeptide() are documented in ms_mascotresults.
     */
    class MS_MASCOTRESFILE_API ms_proteinsummary : public ms_mascotresults
    {
        public:
            //! Call this constructor once to create protein summary results.
            ms_proteinsummary(ms_mascotresfile  &resfile,
                const unsigned int flags = ms_mascotresults::MSRES_GROUP_PROTEINS 
                                         | ms_mascotresults::MSRES_SHOW_SUBSETS,
                              double             minProbability = 0.0,
                              int                maxHitsToReport = 50,
                              const char *       unigeneIndexFile = 0,
                              const char *       singleHit = 0);
            virtual ~ms_proteinsummary();

            //! Return a peptide object for the specified query / hit.
            virtual ms_peptide getPeptide(const int q, const int p) const;

            //! Returns true if this peptide match is unique to one protein or one protein family.
            virtual bool isPeptideUnique(const int q, const int p, const UNIQUE_PEP_RULES rules = UPR_DEFAULT) const;

            //! Return a peptide object for the specified query / hit.
            virtual bool getPeptide(const int q, const int p, ms_peptide * & pep) const;

            //! Return a partial list of proteins that matched the same peptide.
            virtual std::string getProteinsWithThisPepMatch(const int q, const int p, const bool quotes=false);

            //! Return a complete list of proteins that contain this same peptide match.
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

            //! Return the complete error tolerant mod primary neutral loss string from \c h1_q1_et_mods_primary.
            virtual std::string getErrTolModMasterString(const int q, const int p) const;

            //! Return the complete error tolerant mod slave neutral loss string from \c h1_q1_et_mods_slave.
            virtual std::string getErrTolModSlaveString(const int q, const int p) const;

            //! Return the complete error tolerant mod peptide neutral loss string from \c h1_q1_et_mods_pep.
            virtual std::string getErrTolModPepString(const int q, const int p) const;

            //! Return the complete error tolerant mod required peptide neutral loss string from \c h1_q1_et_mods_reqpep.
            virtual std::string getErrTolModReqPepString(const int q, const int p) const;

            //! Return the complete tag string from \c h1_q2_tag or \c q1_p1_tag.
            virtual std::string getTagString(const int q, const int p) const;

            //! Return the first number from <code>h1_q2_drange=0,256</code>.
            virtual int getTagDeltaRangeStart(const int q, const int p) const;

            //! Return the second number from <code>h1_q2_drange=0,256</code>.
            virtual int getTagDeltaRangeEnd(const int q, const int p) const;

            //! Return the complete terminal residue string from \c h1_q1_terms.
            virtual std::string getTerminalResiduesString(const int q, const int p) const;

            //! Return an empty string.
            virtual std::string getComponentString(const int q, const int p) const;

            //! Return the 'protein' score value for cutting off results. Different for peptide and protein summary.
            virtual int getProteinScoreCutoff(double OneInXprobRnd) const;

            //! Returns number of queries (masses) used when calculating PMF protein scores.
            int    getNumPmfQueriesUsed() const;

            //! Indicates whether a mass value was used when calculating PMF protein scores.
            bool   isPmfQueryUsed(const int queryIdx) const;

            //! Returns a list of 'p' values for peptides with the same score.
            virtual std::vector<int> getPepsWithSameScore(const int q, const int p) const;
            
            //! \internal
            virtual bool isValidQandP(const int q, const int h) const;

        protected:  
            // Not safe to copy or assign this object.
            ms_proteinsummary(const ms_proteinsummary & rhs);
#ifndef SWIG
            ms_proteinsummary & operator=(const ms_proteinsummary & rhs);
#endif
            void calculateDecoyStats( double dOneInXprobRnd);

            void collectExpectValuesForFDR(FDREntries_t *expect_values, bool is_decoy, bool use_homology);

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

        private:
            bool parseProtein(const int hit);
            void loadPepRes(const int hit,
                            const int dbIdx,
                            std::string & accession, 
                            const int frame);
            void addMixtureHits();
            int singleHitAsInt_;
            ms_peptide emptyPeptide_;

            bool         isPmfRanks_;
            int          numPmfQueriesUsed_;
            std::vector<int> pmfQueriesUsed_;
            int          numPmfHitsPreserved_;

    };
    /** @} */ // end of resfile_group
}   // matrix_science namespace

#endif // MS_MASCOTRESPROTEINSUM_HPP

/*------------------------------- End of File -------------------------------*/
