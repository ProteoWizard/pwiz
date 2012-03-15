/*
##############################################################################
# file: ms_inputquery.hpp                                                    #
# 'msparser' toolkit                                                         #
# Encapsulates a query from the mascot results file                          #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2002 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_inputquery. $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.12 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_INPUTQUERY_HPP
#define MS_INPUTQUERY_HPP

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

#include <string>

namespace matrix_science {

    /** @addtogroup resfile_group
     *  
     *  @{
     */

    //! This class encapsulates the input data in the mascot results file.
    /*!
    * Although all these parameters could be obtained using the
    * lower level functions such as ms_mascotresfile::getQuerySectionValue 
    * it is generally more convenient to use this object.
    */
    class MS_MASCOTRESFILE_API ms_inputquery  
    {
    public:

        //! Use this contructor to create an object to get the input data.
        ms_inputquery(const ms_mascotresfile &resfile, const int q);
        ~ms_inputquery();

        //! Returns the title for the ms-ms data (if any).
        std::string getStringTitle(bool unescaped) const;

        //! Returns a string that represents the supplied charge for the query.
        std::string getCharge() const;

        //! Returns a string that represents one of the sequence queries..
        std::string getSeq(int seq_no)   const;

        //! Returns a string that represents one of the sequence queries.
        std::string getComp(int comp_no) const;

        //! Returns a string that represents one of the sequence tag queries.
        std::string getTag(int tag_no) const;

        //! Returns the minimum mass of any ion.
        double getMassMin() const;

        //! Returns the maximum mass of any ion.
        double getMassMax() const;

        //! Returns the number of ions.
        int getNumVals()    const;

        //! Returns the number of ions used for matching - value no longer available.
        int getNumUsed()    const;

        //! Returns the peak list for the ions as a string.
        std::string getStringIons1();
       
        //! Returns the peak list for the ions as a string.
        std::string getStringIons2();

        //! Returns the peak list for the ions as a string.
        std::string getStringIons3();

        //! Returns the maximum intensity of any ion.
        double getIntMax() const;

        //! Returns the minimum intensity of any ion.
        double getIntMin() const;

        //! Returns a list of ions peaks for a query.
	    std::vector<std::pair<double,double> > getPeakList(const int ions);

        //! Returns the mass of a particular ions peak.
        double getPeakMass(const int ions, const int peakNo);

        //! Returns the intensity of a particular ions peak.
        double getPeakIntensity(const int ions, const int peakNo);

        //! Returns the number of ions peaks in an ms-ms spectrum.
        int    getNumberOfPeaks(const int ions);

        //! Returns the sum of all the ions intensities.
        double getTotalIonsIntensity();

        //! Returns the peptide tolerance for this query.
        double getPepTol() const;

        //! Returns the peptide tolerance units for this query.
        std::string getPepTolUnits() const;

        //!  Returns the peptol string .
        std::string getPepTolString() const;

        //! Returns an \c INSTRUMENT string if this has been specified at the query level.
        std::string getINSTRUMENT(const bool unescaped = true) const;

        //! Returns the instrument rules if this has been specified at the query level.
        std::string getRULES() const;

        //! Returns the minimum mass to be considered for internal fragemnts if an \c INSTRUMENT has been specified at the query level.
        double getMinInternalMass() const;

        //! Returns the maximum mass to be considered for internal fragments if an \c INSTRUMENT has been specified at the query level.
        double getMaxInternalMass() const;

        //! Returns the variable modifications if this has been specified at the query level.
        std::string getIT_MODS(const bool unescaped = true) const;

        //! Returns the scan number(s) that were used to generate this peak list.
        std::string getScanNumbers(const int rawFileIdx = -1) const;

        //! Returns the rawscan number(s) that were used to generate this peak list.
        std::string getRawScans(const int rawFileIdx = -1) const;

        //! Returns the retention time(s) in seconds of the scans that were used to generate this peak list.
        std::string getRetentionTimes(const int rawFileIdx = -1) const;

        //! Returns the zero based index of the peak list in an MGF, PKL or DTA file.
        int getIndex() const;

    private:
        enum { max_seqs_  = 20, max_comps_ = 20, max_tags_ = 20 };
        const ms_mascotresfile &resfile_;
        const int q_;
        std::string title_;
        std::string unescapedTitle_;
        std::string charge_;
        std::string seq_[max_seqs_   +1]; 
        std::string comp_[max_comps_ +1]; 
        std::string tag_[max_tags_ + 1];
        double mass_min_;
        double mass_max_;
        int num_vals_;
        int num_used_;
        bool loadedIons_;
        std::string ions_[3];
        std::vector<std::pair<double,double> > peakList_[3];  // for ions1, 2 and 3
        double int_max_;
        double int_min_;
        double tol_;
        std::string tol_units_;
        std::string instrument_;
        std::string unescaped_instrument_;
        std::string rules_;
        double minInternalMass_;
        double maxInternalMass_;
        std::string it_mods_;
        std::string unescaped_it_mods_;
        std::string rt_;
        std::string scans_;
        std::string rawscans_;
        int index_;

        void fillPeakList(const int ions);
        void loadIons();
    };
    /** @} */ // end of resfile_group
}   // matrix_science namespace

#endif // !defined(MS_INPUTQUERY_HPP)
