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

enum DistanceAttributeEnum { _Hamming, _NumberOfMS2IDs, _Random, _RTDiff, _WeightedHamming};

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
