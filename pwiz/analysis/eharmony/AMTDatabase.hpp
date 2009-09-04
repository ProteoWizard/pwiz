///
/// AMTDatabase.hpp
///

#ifndef _AMTDATABASE_HPP_
#define _AMTDATABASE_HPP_

#include "AMTContainer.hpp"

namespace pwiz{
namespace eharmony{

using namespace pwiz::data::peakdata;


struct AMTDatabase
{
    AMTDatabase(const AMTContainer& amtContainer);

    virtual std::vector<SpectrumQuery> query(DfcPtr dfc, const WarpFunctionEnum& wfe, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in, string outputDir="./amtdb_query",const int& roc=0, const double& threshold=0.75);
    
    PidfPtr _peptides;
    
};

struct IslandizedDatabase : public AMTDatabase
{
    IslandizedDatabase(boost::shared_ptr<AMTContainer> amtContainer);

    virtual std::vector<SpectrumQuery> query(DfcPtr dfc, const WarpFunctionEnum& wfe, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in, string outputDir="./amtdb_query");   

    struct Gaussian 
    {
        Gaussian() : mu(make_pair(0,0)), sigma(make_pair(0,0)){}
        Gaussian(const pair<pair<double,double>, pair<double,double> >& params) : mu(params.first), sigma(params.second){}
        pair<double,double> mu;
        pair<double,double> sigma;      

        bool operator==(const Gaussian& that) const { return mu == that.mu && sigma == that.sigma;}
        bool operator!=(const Gaussian& that) const { return !(*this == that);}

    };

    struct Island
    {

        Island() : massMin(0), massMax(0), rtMin(0), rtMax(0) {}
        double calculatePVal(const double& mz, const double& rt) const;

        double massMin;
        double massMax;
        double massMedian;
        double massMean;

        double rtMin;
        double rtMax;
        double rtMedian;
        double rtMean;

        double relativeArea;

        vector<boost::shared_ptr<SpectrumQuery> > spectrumQueries;
        vector<Gaussian> gaussians;
        
        string id;

    };

    vector<Island> islands;
    vector<string> uniquePeptides;

};

} // namespace eharmony
} // namespace pwiz

#endif // _AMTDATABASE_HPP_
