//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//


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

HammingDistance::HammingDistance(const vector<boost::shared_ptr<Entry> >& v)
{
    vector<boost::shared_ptr<Entry> >::const_iterator it = v.begin();
    for(; it != v.end(); ++it)
        {
            vector<boost::shared_ptr<Entry> >::const_iterator jt = v.begin();
            while (jt < it)
                {
                    vector<string> a = getUniquePeptides((*it)->_pidf);
                    vector<string> b = getUniquePeptides((*jt)->_pidf);
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

    sort(allUniquePeptides.begin(), allUniquePeptides.end());

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

double HammingDistance::score(const Entry& a, const Entry& b)
{
    vector<string> asqs = getUniquePeptides(a._pidf);
    vector<string> bsqs = getUniquePeptides(b._pidf);

    // get hamming distance(a,b)
    double hammingDistance = getHammingDistance(a,b,allUniquePeptides);

    return hammingDistance;

}



EditDistance::EditDistance() : insertionCost(1), deletionCost(1), translocationCost(100){}

double EditDistance::score(const Entry& a, const Entry& b) 
{
    return 0;

}   
