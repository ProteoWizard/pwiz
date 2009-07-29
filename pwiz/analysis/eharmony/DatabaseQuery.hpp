///
/// DatabaseQuery.hpp
///

#ifndef _DATABASEQUERY_HPP_
#define _DATABASEQUERY_HPP_

#include "FeatureSequenced.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "SearchNeighborhoodCalculator.hpp"

namespace pwiz{
namespace eharmony{

using namespace pwiz::data::pepxml;

typedef boost::shared_ptr<SpectrumQuery> SpectrumQueryPtr;
//    Independent of retention time calibration, this struct allows querying of a feature to the database as a whole and is constructed from the database itself

struct DatabaseQuery
{
    std::vector<MatchPtr> query(FeatureSequencedPtr fs, NormalDistributionSearch nds, double threshold);
    pair<double,double> calculateSearchRegion(const double& mu1, const double& mu2, const double& sigma1, const double& sigma2, const double& threshold);

    PidfPtr _database;
    DatabaseQuery(const PidfPtr database) : _database(database){}    

};

}
}


#endif
