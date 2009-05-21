///
/// NeighborJoiner.hpp
///

#ifndef _NEIGHBORJOINER_HPP
#define _NEIGHBORJOINER_HPP

#include "Matrix.hpp"
#include "AMTContainer.hpp"

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

struct NeighborJoiner : public Matrix
{     
    NeighborJoiner(const vector<boost::shared_ptr<Entry> >& entries);

    void addDistanceAttribute(boost::shared_ptr<DistanceAttribute> attr) { _attributes.push_back(attr); }
    void calculateDistanceMatrix();
    void joinNearest();
    void joinAll() { while (_rowEntries.size() > 1) joinNearest(); }

    vector<Entry > _rowEntries;
    vector<Entry > _columnEntries; 

    vector<boost::shared_ptr<DistanceAttribute> > _attributes;
    vector<pair<int, int> > _tree; // stores the row/column indices of the merge at each step

};

}
}


#endif // _NEIGHBORJOINER_HPP_
