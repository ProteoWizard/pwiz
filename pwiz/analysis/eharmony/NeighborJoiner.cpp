///
/// NeighborJoiner.cpp
///

#include "NeighborJoiner.hpp"
#include <iostream>

using namespace pwiz;
using namespace eharmony;

NeighborJoiner::NeighborJoiner(const vector<boost::shared_ptr<Entry> >& entries) : Matrix(entries.size(), entries.size())
{
    vector<boost::shared_ptr<Entry> >::const_iterator it = entries.begin();
    for(; it!= entries.end(); ++it)
        {
  	    _rowEntries.push_back(**it);
	    _columnEntries.push_back(**it);
	  
	}

}

void NeighborJoiner::calculateDistanceMatrix()
{
    vector<boost::shared_ptr<DistanceAttribute> >::iterator it = _attributes.begin();
    for(; it!= _attributes.end(); ++it) // just overwrites right now. later aggregate euclideanly or as desired
        {
            size_t index = 0;
            vector<Entry>::iterator i_it = _rowEntries.begin();
            for(; i_it != _rowEntries.end(); ++i_it, ++index)
                {
                    size_t jindex = 0;
                    vector<Entry>::iterator j_it = _columnEntries.begin(); // square so doesn't really matter..
                    for(; j_it != _columnEntries.end(); ++j_it, ++jindex)
                        {
			  if (index == jindex) 
			      {
			          this->insert(10000000000, index, jindex);				 
                      continue;
			      }
			  
			  double score = (*it)->score(*i_it, *j_it);			  
			  this->insert(score, index, jindex);
 
                        } 
                  
                }

        }  

}

void NeighborJoiner::joinNearest()
{
    if (_rowEntries.size() == 1) return;    
    pair<int,int> nearest = getMinValLocation();

    cout << "Joining nearest neighbors at current matrix indices: " << nearest.first << " , " << nearest.second << endl;

    AMTContainer ra = _rowEntries.at(nearest.first);
    AMTContainer rb = _rowEntries.at(nearest.second);

    cout << "to be merged: " << ra._id << " and " << rb._id << endl;
    AMTContainer ra_copy = ra;

    ra.merge(rb);
    replace(_rowEntries.begin(), _rowEntries.end(), ra_copy, ra);
    _rowEntries.erase(find(_rowEntries.begin(), _rowEntries.end(),rb));
    
    AMTContainer ca = _columnEntries.at(nearest.first);
    AMTContainer cb = _columnEntries.at(nearest.second);

    AMTContainer ca_copy = ca;
    
    ca.merge(cb);
    replace(_columnEntries.begin(), _columnEntries.end(), ca_copy, ca);
    _columnEntries.erase(find(_columnEntries.begin(), _columnEntries.end(), cb));

    _tree.push_back(nearest); // store merging 

    _data.clear(); // old distance matrix is not any good
    calculateDistanceMatrix(); // recalculate it

}

