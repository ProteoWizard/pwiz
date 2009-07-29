///
/// NeighborJoiner.cpp
///

#include "NeighborJoiner.hpp"
#include <iostream>

using namespace pwiz;
using namespace eharmony;

NeighborJoiner::NeighborJoiner(const vector<boost::shared_ptr<Entry> >& entries, const WarpFunctionEnum& wfe) : Matrix(entries.size(), entries.size()), _wfe(wfe)
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

    cout << "Merging: " << ra._id << " and " << rb._id << endl;
    AMTContainer ra_copy = ra;

    // adjust rt and warp entries

    DataFetcherContainer dfc(ra._pidf, rb._pidf, ra._fdf, rb._fdf);

    const bool adjust_a = (ra._pidf->getRtAdjustedFlag() + 1) % 2;
    const bool adjust_b = (rb._pidf->getRtAdjustedFlag() + 1) % 2;

    dfc.adjustRT(adjust_a, adjust_b);
    dfc.warpRT(_wfe);

    // merge using the row indices
    ra.merge(rb);
    replace(_rowEntries.begin(), _rowEntries.end(), ra_copy, ra);
    _rowEntries.erase(find(_rowEntries.begin(), _rowEntries.end(),rb));
    
    // no need to merge the column indices (shared ptrs) but do need to erase
    AMTContainer cb = _columnEntries.at(nearest.second);
    _columnEntries.erase(find(_columnEntries.begin(), _columnEntries.end(), cb));
    
    // store indices of merging
    _tree.push_back(nearest); 

    // recalculate distance matrix with new entries
    _data.clear(); 
    calculateDistanceMatrix();

}

