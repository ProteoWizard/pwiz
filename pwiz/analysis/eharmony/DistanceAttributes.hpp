///
/// DistanceAttributes.hpp
///

#ifndef _DISTANCEATTRIBUTES_HPP_
#define _DISTANCEATTRIBUTES_HPP_

#include "AMTContainer.hpp"
#include "Matrix.hpp"

using namespace std;

namespace pwiz{
namespace eharmony{

typedef AMTContainer Entry;

struct DistanceAttribute
{
    DistanceAttribute(){}
    virtual double score(const Entry& a, const Entry& b){ return 0;}
    virtual double operator()(const Entry& a, const Entry& b){ return this->score(a,b);}
    virtual ~DistanceAttribute(){}

};

struct NumberOfMS2IDs : public DistanceAttribute
{
     NumberOfMS2IDs(){}
     virtual double score(const AMTContainer& a, const AMTContainer& b);

};

struct RandomDistance : public DistanceAttribute
{
    virtual double score(const Entry& a, const Entry& b);

};

struct RTDiffDistribution : public DistanceAttribute
{
    virtual double score(const Entry& a, const Entry& b);

};

struct HammingDistance : public DistanceAttribute
{
    HammingDistance(const vector<boost::shared_ptr<Entry> >& v);
    virtual double score(const Entry& a, const Entry& b);
    
    vector<string> allUniquePeptides;

};

struct WeightedHammingDistance : public DistanceAttribute
{
    WeightedHammingDistance(const vector<boost::shared_ptr<Entry> >& v);
    virtual double score(const Entry& a, const Entry& b);

    double normalizationFactor;
    vector<string> allUniquePeptides;

};

struct EditDistance : public DistanceAttribute
{
    EditDistance();
    virtual double score(const Entry& a, const Entry& b);

    double insertionCost;
    double deletionCost;
    double translocationCost;

private:

    Matrix scoreMatrix;

};

} // namespace eharmony
} // namespace pwiz


#endif
