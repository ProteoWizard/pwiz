/*
##############################################################################
# File: ms_shapiro_wilk.hpp                                                  #
# Mascot Parser toolkit                                                      #
# Include file for Shapiro-Wilk W test (statistical algorithm)               #
##############################################################################
#    $Source: /vol/cvsroot/parser/inc/ms_shapiro_wilk.hpp,v $
#    $Author: villek $ 
#      $Date: 2010-09-06 16:18:57 $ 
#  $Revision: 1.3 $
##############################################################################
*/
/*
##############################################################################
# Applied Statistics algorithms                                              #
#                                                                            #
# Translated and adapted from routines that originally appeared in the       #
# journal Applied Statistics, copyright The Royal Statistical Society,       #
# and made available through StatLib http://lib.stat.cmu.edu/                #
#                                                                            #
# The implementation and source code for this algorithm is available as a    #
# free download from http://www.matrixscience.com/msparser.html              #
# to conform with the requirement that no fee is charged for their use       #
##############################################################################
*/


#ifndef MS_SHAPIRO_WILK_HPP
#define MS_SHAPIRO_WILK_HPP

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

// Includes from the standard template library
#include <string>
#include <deque>
#include <list>
#include <vector>
#include <set>
#include <map>

#ifdef __AIX__
#undef SCORE
#endif

namespace matrix_science {
    /** @addtogroup tools_group
     *  
     *  @{
     */


    //! The Shapiro-Wilk W test.
    /*!
     * <strong>Testing for normality</strong>
     *
     * Testing for outliers and reporting a standard deviation for the protein 
     * ratio can only be performed if the peptide ratios are consistent with a 
     * sample from a normal distribution (in log space). If the peptide 
     * ratios do not appear to be from a normal distribution, this may indicate 
     * that the values are meaningless, and something went systematically wrong 
     * with the the analysis. On the other hand, it may indicate something 
     * interesting, like the peptides have been mis-assigned and actually come 
     * from two proteins with very different ratios, so that the distribution 
     * is bimodal.
     *
     * <strong>Shapiro-Wilk W test</strong>
     *
     * In the Shapiro-Wilk W test, the null hypothesis is that the sample is 
     * taken from a normal distribution. This hypothesis is rejected if the 
     * critical value P for the test statistic W is less than 0.05. The routine 
     * used is valid for sample sizes between 3 and 2000.
     *
     * \ref ShapiroWilkSourceCode
     * 
     * References:
     * 
     * 1. Royston, J. P., An Extension of Shapiro and Wilk's W Test for 
     * Normality to Large Samples, Applied Statistics 31 115-124 (1982)
     *
     * 2. Royston, P., Remark AS R94: A Remark on Algorithm AS 181: The W-test 
     * for Normality, Applied Statistics 44 547-551 (1995)
     */
    class MS_MASCOTRESFILE_API ms_shapiro_wilk
    {
    public:
	    ms_shapiro_wilk();
	    ~ms_shapiro_wilk();

        //! The Shapiro-Wilk W test.
	    ms_shapiro_wilk(std::deque<std::pair<size_t,double> > x, long n, long n1, long n2);

        //! Calculate results using values previously added using appendSampleValue().
        void calculate(long n, long n1, long n2);

        //! Add a new sample value to the list to be tested.
        void appendSampleValue(double y);

        //! Clear current vector of X values.
        void clearSampleValues();

        //! Returns the Shapiro-Wilks W-statistic.
        double getResult() const;

        //! Returns the P-value for the Shapiro-Wilks W-statistic.
        double getPValue() const;

        //! Returns the error code for the Shapiro-Wilks W-statistic.
        double getErrorCode() const;

    private:
	    void calc(bool init, std::deque<std::pair<size_t,double> > x, long n, long n1, long n2, std::deque<double> &a);
        double poly(const double *cc, int nord, double x);
	    double ppnd7(double p,int &ifault);
	    double alnorm(double x,bool upper);
	    long sign(long x,long y);
        void swilkresult(double w, double pw, long ifault);

        bool   init_;
	    double w_;       // The Shapiro-Wilks W-statistic.
	    double pw_;      // the P-value for w
	    long   ifault_;  // error indicator
        std::deque<double> a_;
        std::deque<std::pair<size_t,double> > x_;
    };

    /** @} */ // end of tools_group
} // matrix_science


#endif // MS_SHAPIRO_WILK_HPP
/*------------------------------- End of File -------------------------------*/
