/*
##############################################################################
# file: ms_mascotrespeptide.hpp                                              #
# 'msparser' toolkit                                                         #
# Encapsulates a peptide from the summary section or peptides section of the #
# mascot results file                                                        #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2002 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotrespe $ #
#     $Author: davidc $ #
#       $Date: 2011-01-25 12:08:08 $ #
#   $Revision: 1.29 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASCOTRESPEPTIDE_HPP
#define MS_MASCOTRESPEPTIDE_HPP

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
#include <vector>

namespace msparser_internal {
    class ms_peptide_impl;
}

namespace matrix_science {
    class ms_protein;
    class ms_mascotresults;

    /** @addtogroup resfile_group
     *  
     *  @{
     */

    //! This class encapsulates a peptide from the mascot results file.
    /*!
     * This class is used for protein summary and peptide summary results. 
     * There is generally no need to create an object of this class.
     * Simply open the results file as ms_proteinsummary or ms_peptidesummary
     * and call ms_proteinsummary::getPeptide() 
     * or ms_peptidesummary::getPeptide().
     *
     * To create an ms_peptide object that is not in a Mascot results
     * file, use ms_aahelper::createPeptide().
     *
     */
    class MS_MASCOTRESFILE_API ms_peptide
    {
        public:
            //! Default constructor
            ms_peptide();

            //! Constructor to initialise most commonly used values.
            ms_peptide( int         query,
                        int         rank,
                        int         missedCleavages,
                        double      mrCalc,
                        double      delta,
                        double      observed,
                        int         charge,
                        int         numIonsMatched,
                        std::string peptideStr,
                        int         peaksUsedFromIons1,
                        std::string varModsStr,
                        double      ionsScore,
                        std::string seriesUsedStr,
                        int         peaksUsedFromIons2,
                        int         peaksUsedFromIons3,
                        const ms_mascotresults * pResults = 0,
                        bool        storeReloadableInfo = true);

            //! Constructor to initialise all values.
            ms_peptide( const int                query,
                        const int                rank,
                        const int                missedCleavages,
                        const double             mrCalc,
                        const double             delta,
                        const double             observed,
                        const int                charge,
                        const int                numIonsMatched,
                        const std::string      & peptideStr,
                        const int                peaksUsedFromIons1,
                        const std::string      & varModsStr,
                        const double             ionsScore,
                        const std::string      & seriesUsedStr,
                        const int                peaksUsedFromIons2,
                        const int                peaksUsedFromIons3,
                        const std::string      & primaryNlStr,
                        const std::string      & substStr,
                        const std::string      & componentStr,
                        const ms_mascotresults * pResults = 0,
                        const bool               storeReloadableInfo = true);

            //! Copying constructor.
            ms_peptide(const ms_peptide& src);

            //! Destructor.
            ~ms_peptide();

#ifndef SWIG
            //! C++ assignment operator.
            ms_peptide& operator=(const ms_peptide& right);
#endif
            //! Copies all content from another instance of the class.
            void copyFrom(const ms_peptide* src);

            //! To save on memory, may need to call this function.
            bool clearReloadableInfo();

            //! Each peptide is associate with a query. 
            int         getQuery()              const;

            //! Return the 'rank' of the peptide match.
            int         getRank()               const;
            void        setRank(int rank);
            
            //! Similar to getRank() except that equivalent scores get the same rank.
            int         getPrettyRank()         const;
            void        setPrettyRank(int rank);

            //! Returns true if there was a peptide match to this spectrum. 
            bool        getAnyMatch()           const;

            //! Returns the number of missed cleavages. 
            int         getMissedCleavages()    const;

            //! Returns the calculated relative mass for this peptide .
            double      getMrCalc()             const;

            //! Returns the difference between the calculated and experimental relative masses.
            double      getDelta()              const;

            //! Returns the observed mass / charge value. 
            double      getObserved()           const;

            //! Returns the observed mz value as a relative mass. 
            double      getMrExperimental()     const;

            //!Returns the charge state for the parent mass. 
            int         getCharge()             const;

            //! Returns the number of ions matched.
            int         getNumIonsMatched()     const;

            //! Returns the sequence found for the peptide.
            std::string getPeptideStr(bool substituteAmbiguous = true) const;

            //! Returns the length in residues of the sequence found for the peptide.
            int         getPeptideLength()      const;

            //! Returns number of peaks used from \c ions1.
            int         getPeaksUsedFromIons1() const;

            //! Returns number of peaks used from \c ions2.
            int         getPeaksUsedFromIons2() const;

            //! Returns number of peaks used from \c ions3.
            int         getPeaksUsedFromIons3() const;

            //! Variable modifications as a string of digits.
            std::string getVarModsStr()         const;

            //! Variable modifications as a string of digits.
            void        setVarModsStr(const std::string str);

            //! Returns the ions score.
            double      getIonsScore()          const;

            //! Returns the series used as a string.
            std::string getSeriesUsedStr()      const;

            //! Returns true if the two peptides are identical.
            bool        isSamePeptideStr(ms_peptide * peptide, 
                                         bool substituteAmbiguous = true) const;

            //! Returns true if the two variable modifications are identical.
            bool        isSameVarModsStr(ms_peptide * peptide) const;

            //! Returns the hit numberof the first protein that contains this peptide.
            int         getFirstProtAppearedIn() const;

            //! \internal
            //! This function used internally in the library.
            void        setFirstProtAppearedIn(int n);

            //! Returns the total intensity of all of the ions in the spectrum.
            double      getIonsIntensity()      const;
            void        setIonsIntensity(double n);

            //! Returns a pointer to a protein that contains this peptide.
            const ms_protein * getProtein(int num) const;

            //! Return a list of hit numbers for the proteins that contain this peptide.
            const std::vector<int> getProteins()const;

            //! Returns the number of proteins that contains this peptide.
            int         getNumProteins()        const;
            void addProtein(const ms_protein * protein);
            void addProtein(const int hitNumber);

            //! Used for X, B and Z residues in source databases where Mascot then substitutes for a residue.
            std::string getAmbiguityString()    const;
            bool setAmbiguityString(const std::string val);

            //! Returns neutral loss information associated with any modification for the peptide.
            std::string getPrimaryNlStr()       const;
            void        setPrimaryNlStr(const std::string value);

            //! Returns true if this peptide came from the error tolerant search.
            bool getIsFromErrorTolerant()       const;
            void setIsFromErrorTolerant(const bool isFromErrorTolerant);

            //! Returns the quantitation method component name used for the peptide match.
            std::string getComponentStr()       const;
            void        setComponentStr(const std::string value);

            //! Returns true if the length of the peptide sequence is less than the minimum used for grouping.
            bool        getLessThanMinPepLen()  const;

            //! Returns the number of 13C peaks offset required to get a match with the supplied tolerance.
            int getNum13C(const double tol, const std::string tolu, const std::string mass_type) const;

            //! Called within Mascot Parser to replace Mascot scores with Percolator scores.
            void setPercolatorScores(double posteriorErrorProbability, 
                                     double qValue,
                                     double internalPercolatorScore);

            //! Return true if there are percolator results for this peptide.
            bool anyPercolatorResults(void)     const;

            //! Returns the percolator scores and original Mascot ions score.
            void getPercolatorScores(double * posteriorErrorProbability, 
                                     double * qValue, 
                                     double * internalPercolatorScore, 
                                     double * mascotIonsScore) const;

        private:
            msparser_internal::ms_peptide_impl * pImpl_;
    };
    /** @} */ // end of resfile_group
}   // matrix_science namespace

#endif // MS_MASCOTRESPEPTIDE_HPP

/*------------------------------- End of File -------------------------------*/
