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

typedef boost::shared_ptr<DataFetcherContainer> DfcPtr;

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

    virtual bool operator==(const SearchNeighborhoodCalculator& that){ return _id == that._id && _mzTol == that._mzTol && _rtTol == that._rtTol;}
    virtual bool operator!=(const SearchNeighborhoodCalculator& that){ return !(*this == that); }

};


struct NormalDistributionSearch : public SearchNeighborhoodCalculator
{
    NormalDistributionSearch(double Z = 3) :  _Z(Z) { _id = ("normalDistribution[" + boost::lexical_cast<string>(_Z) + "]").c_str(); }
   
    virtual void calculateTolerances(const DfcPtr dfc);
    virtual  double score(const SpectrumQuery& a, const Feature& b) const;
    virtual bool close(const SpectrumQuery& a, const Feature& b) const;

    double _mu_mz;
    double _sigma_mz;
  
    double _mu_rt;
    double _sigma_rt;

    double _Z;
        
};


} // eharmony
} // pwiz

#endif

