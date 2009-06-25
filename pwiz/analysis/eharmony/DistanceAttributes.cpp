
///
/// DistanceAttributes.cpp
///

#include "DistanceAttributes.hpp"
#include <time.h>

using namespace pwiz;
using namespace eharmony;

namespace{

vector<string> getUniquePeptides(const PidfPtr pidf) 
{
  vector<string> uniqueSequences;
  Bin<SpectrumQuery> bin = pidf->getBin();
  vector<boost::shared_ptr<SpectrumQuery> > sqs = bin.getAllContents();
  vector<boost::shared_ptr<SpectrumQuery> >::iterator it = sqs.begin();
  for( ; it!= sqs.end(); ++it)
      {
          if (find(uniqueSequences.begin(), uniqueSequences.end(), (*it)->searchResult.searchHit.peptide) == uniqueSequences.end()) 
              {
                  uniqueSequences.push_back((*it)->searchResult.searchHit.peptide);
		
              }

      }
  return uniqueSequences;

}

 int getHammingDistance(const Entry& a, const Entry& b, vector<string> allPeptides)
 {
     int hammingDistance = 0;

     vector<string> asqs = getUniquePeptides(a._pidf);
     vector<string> bsqs = getUniquePeptides(b._pidf);

     vector<int> aint;
     vector<int> bint;

     vector<string>::iterator it = allPeptides.begin();
     for(; it != allPeptides.end(); ++it)
         {
             if (find(asqs.begin(), asqs.end(), *it) != asqs.end()) aint.push_back(1);
             else aint.push_back(0);

             if (find(bsqs.begin(), bsqs.end(), *it) != bsqs.end()) bint.push_back(1);
             else bint.push_back(0);
                         
             hammingDistance += fabs(aint.back() - bint.back());

         }

     return hammingDistance;

 }

} // anonymous namespace

typedef AMTContainer Entry;

double NumberOfMS2IDs::score(const Entry& a, const Entry& b)
{
    int a_count = a._pidf->getAllContents().size();
    int b_count = b._pidf->getAllContents().size();
    return sqrt((a_count-b_count)*(a_count-b_count));
                                                                                                           
}


double RandomDistance::score(const Entry& a, const Entry& b) 
{
    int val = rand();
    val = val % 100;
    cout << val << endl;
    return val;

}

double RTDiffDistribution::score(const Entry& a, const Entry& b)
{
    PeptideMatcher pm(a._pidf, b._pidf);
    pm.calculateDeltaRTDistribution();
    pair<double,double> params = pm.getDeltaRTParams();
    
    return sqrt(params.first*params.first + params.second*params.second); // weight mean and stdev equally

}

WeightedHammingDistance::WeightedHammingDistance(const vector<boost::shared_ptr<Entry> >& v)
{
  // get  normalizationFactor
    normalizationFactor = 0;
    vector<boost::shared_ptr<Entry> >::const_iterator it = v.begin();
    for(; it != v.end(); ++it)
        {           
            vector<boost::shared_ptr<Entry> >::const_iterator jt = v.begin();
            while (jt < it)
                {           
                    vector<string> a = getUniquePeptides((*it)->_pidf);
                    vector<string> b = getUniquePeptides((*jt)->_pidf);
                    normalizationFactor += (a.size() + b.size());
                    ++jt;
                    
                    vector<string>::iterator a_it = a.begin();
                    for(; a_it != a.end(); ++a_it)
                        {
                            const string& currentPeptide = *a_it;                  
                            if (find(allUniquePeptides.begin(),allUniquePeptides.end(),currentPeptide) == allUniquePeptides.end()) allUniquePeptides.push_back(currentPeptide);
                        }
                    
                    vector<string>::iterator b_it = b.begin();
                    for(; b_it != b.end(); ++b_it)
                        {
                            const string& currentPeptide = *b_it;                           
                            if (find(allUniquePeptides.begin(),allUniquePeptides.end(),currentPeptide) == allUniquePeptides.end()) allUniquePeptides.push_back(currentPeptide);
                        }

                }
            
        }
    
    sort(allUniquePeptides.begin(), allUniquePeptides.end()); // store allUniquePeptides as sorted list of sequences
   
}

double WeightedHammingDistance::score(const Entry& a, const Entry& b) 
{
    vector<string> asqs = getUniquePeptides(a._pidf);
    vector<string> bsqs = getUniquePeptides(b._pidf);

    // get hamming distance(a,b)
    double hammingDistance = getHammingDistance(a,b,allUniquePeptides);
    hammingDistance *= boost::lexical_cast<double>((asqs.size() + bsqs.size()))/normalizationFactor;
    
    return hammingDistance;

}


EditDistance::EditDistance() : insertionCost(1), deletionCost(1), translocationCost(100){}

double EditDistance::score(const Entry& a, const Entry& b) 
{
    return 0;

}   
