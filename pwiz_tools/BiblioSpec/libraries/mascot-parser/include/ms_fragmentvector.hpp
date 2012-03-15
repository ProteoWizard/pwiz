/*
##############################################################################
# File: ms_fragmentvector.hpp                                                #
# Mascot Parser toolkit                                                      #
# Encapsulates a list (vector) of single fragment ions                       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Source: /vol/cvsroot/parser/inc/ms_fragmentvector.hpp,v $
#    $Author: villek $ 
#      $Date: 2010-09-06 16:18:57 $ 
#  $Revision: 1.5 $
##############################################################################
*/

#if !defined(ms_fragmentvector_INCLUDED_)
#define ms_fragmentvector_INCLUDED_

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
#include <set>
#include <vector>
#include <map>


namespace matrix_science {
     class ms_mascotresfile;

    /** @addtogroup tools_group
     *  
     *  @{
     */

    //! Class for holding a list of ms_fragment objects.
    /*!
     * This class is necessary when using Mascot Parser from programming
     * language other than C++. It is used when calling
     * ms_aahelper::calcFragments().
     *
     * C++ users will generally just want to use
     * a std::vector&lt;ms_fragment&gt; as it is easier. 
     */
    class MS_MASCOTRESFILE_API ms_fragmentvector
    {
    public:

        //! Flags for matching experimental peaks to calculated peaks.
        /*!
         * Used as flags for addExperimentalData() when passing a
         * ms_mascotresfile object.
         *
         * See \ref DynLangEnums.
         *
         */
        enum MATCH_PEAKS 
        { 

            MATCH_MOST_INTENSE_PEAK  = 0x0000, //!< Match the most intense peak in the region.
            MATCH_CLOSEST_PEAK       = 0x0001  //!< Match the peak with the closest m/z value (not currently supported).
        };


        //! Default constructor.
        ms_fragmentvector();

        //! Copying constructor.
        ms_fragmentvector(const ms_fragmentvector& src);

        //! Destructor.
        virtual ~ms_fragmentvector();

        //! Copies all content from another instance.
        void copyFrom(const ms_fragmentvector* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_fragmentvector& operator=(const ms_fragmentvector& right);
#endif
        //! Returns a number of fragments currently held in the vector.
        int getNumberOfFragments() const;

        //! Deletes all fragments from the vector.
        void clearFragments();

        //! Adds a new fragment to the end of the vector.
        void appendFragment(const ms_fragment * item);

        //! Return a fragment object by its number.
        const ms_fragment * getFragmentByNumber(const unsigned int numFrag) const;

        typedef std::vector<ms_fragment> frag_vector;

        //! Return a pointer to the STL vector of ms_fragment objects.
        frag_vector * getVector();

        //! Find matches to peak list.
        bool addExperimentalData(const std::string & peakList, 
                                 const int           numPeaks, 
                                 const double        tolerance, 
                                 const std::string & toleranceUnits,
                                 const bool          updateMatchList = true);

        //! Find matches to peaks lists from a Mascot results file.
        bool addExperimentalData(const ms_mascotresfile * resfile,
                                 const int query,
                                 const int flags = MATCH_MOST_INTENSE_PEAK,
                                 const int peaksUsedFromIons1 = -1,
                                 const int peaksUsedFromIons2 = -1,
                                 const int peaksUsedFromIons3 = -1);

        typedef std::map<double, double> peaklist_t;  // Mass, intensity

        //! Find matches to peak list.
        bool addExperimentalData(const peaklist_t &  peakList, 
                                 const double        tolerance, 
                                 const std::string & toleranceUnits,
                                 const bool          updateMatchList = true);

    private:
        frag_vector  entries_;
        peaklist_t experimentalData_;
        void matchFragments();
        double   matchingTol_; 
        std::string matchingTolUnits_;
    }; // class ms_fragmentvector
    /** @} */ // end of tools_group
} // matrix_science

#endif // !defined(ms_fragmentvector_INCLUDED_)
