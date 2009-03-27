///
/// SearchNeighborhoodCalculator.hpp
///

#ifndef _SEARCHNEIGHBORHOODCALCULATOR_HPP_
#define _SEARCHNEIGHBORHOODCALCULATOR_HPP_

#include "DataFetcherContainer.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"

using namespace pwiz::data::pepxml;

namespace pwiz{
namespace eharmony{

struct SearchNeighborhoodCalculator
{
    SearchNeighborhoodCalculator() : _id("default"), _mzTol(.005), _rtTol(60) {} // consider moving to calculateTolerances.
    SearchNeighborhoodCalculator(double mzTol, double rtTol) : _mzTol(mzTol), _rtTol(rtTol) { _id = ("naive[" + boost::lexical_cast<string>(_mzTol) + "," + boost::lexical_cast<string>(_rtTol) + "]").c_str();}

    virtual void calculateTolerances(const DataFetcherContainer& dfc) {}
    virtual bool close(const SpectrumQuery& a, const Feature& b) const;
    virtual double score(const SpectrumQuery& a, const Feature& b) const;

    string _id;
    double _mzTol;
    double _rtTol;

    virtual ~SearchNeighborhoodCalculator(){}

};


struct NormalDistributionSearch : public SearchNeighborhoodCalculator
{
    NormalDistributionSearch(double Z = 3) :  _Z(Z) { _id = ("normalDistribution[" + boost::lexical_cast<string>(_Z) + "]").c_str(); }
   
    void calculateTolerances(const DataFetcherContainer& dfc);
    virtual double score(const SpectrumQuery& a, const Feature& b) const;
    // bool close(const SpectrumQuery& a, const Feature& b) const;

    string _id;
    double _mzTol;
    double _rtTol;

    double _mu;
    double _sigma;

    double _Z;
        
};


} // eharmony
} // pwiz

#endif

