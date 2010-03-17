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
/// DistanceAttributesTest.cpp
///

#include "DistanceAttributes.hpp"
#include "NeighborJoiner.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz;
using namespace eharmony;
using namespace pwiz::util;

boost::shared_ptr<SpectrumQuery> generateSpectrumQuery(const string& sequence, const double& mass, const int& charge, const double& rt) 
{
    boost::shared_ptr<SpectrumQuery> result(new SpectrumQuery());
    result->precursorNeutralMass = mass;
    result->assumedCharge = charge;
    result->retentionTimeSec = rt;
    result->searchResult.searchHit.peptide = sequence;
    result->searchResult.searchHit.analysisResult.peptideProphetResult.probability = 1.0;

    return result;
}

vector<boost::shared_ptr<AMTContainer> > generateAMTContainers()
{
    // Only deals with peptides right now as that is the only variable to existing distance attrs
    // If distance attrs need actual feature data, that will need to be initialized as well

    boost::shared_ptr<SpectrumQuery> a = generateSpectrumQuery("ROBERTBURKE",354,5,415);
    boost::shared_ptr<SpectrumQuery> b = generateSpectrumQuery("DARRENKESSNER", 170, 4, 420);
    boost::shared_ptr<SpectrumQuery> c = generateSpectrumQuery("ROBERTRICE", 165, 3, 1200);
    boost::shared_ptr<SpectrumQuery> d = generateSpectrumQuery("KATHERINEHOFF", 130, 2, 515);
    boost::shared_ptr<SpectrumQuery> e = generateSpectrumQuery("PARAGMALLICK", 165, 3, 345);

    vector<boost::shared_ptr<SpectrumQuery> > first;
    first.push_back(a);
    first.push_back(b);
    first.push_back(c);
    first.push_back(d);
    first.push_back(e);

    boost::shared_ptr<SpectrumQuery> a2 = generateSpectrumQuery("ROBERTBURKE", 355,5,416);
    boost::shared_ptr<SpectrumQuery> b2 = generateSpectrumQuery("DARRENKESSNER", 175, 4, 666);
    boost::shared_ptr<SpectrumQuery> c2 = generateSpectrumQuery("DAMIENWOOD", 200, 3, 815);
    
    vector<boost::shared_ptr<SpectrumQuery> > second;
    second.push_back(a2);
    second.push_back(b2);
    second.push_back(c2);

    boost::shared_ptr<SpectrumQuery> a3 = generateSpectrumQuery("ROBERTBURKE", 356,5,417);

    vector<boost::shared_ptr<SpectrumQuery> > third;
    third.push_back(a3);

    boost::shared_ptr<SpectrumQuery> a4 = generateSpectrumQuery("ROBERTBURKE", 357, 5, 418);

    vector<boost::shared_ptr<SpectrumQuery> > fourth;
    fourth.push_back(a4);

    PidfPtr pidf1(new PeptideID_dataFetcher(first));
    PidfPtr pidf2(new PeptideID_dataFetcher(second));
    PidfPtr pidf3(new PeptideID_dataFetcher(third));
    PidfPtr pidf4(new PeptideID_dataFetcher(fourth));
    
    boost::shared_ptr<AMTContainer> amt1(new AMTContainer());
    amt1->_pidf = pidf1;

    boost::shared_ptr<AMTContainer> amt2(new AMTContainer());
    amt2->_pidf = pidf2;

    boost::shared_ptr<AMTContainer> amt3(new AMTContainer());
    amt3->_pidf = pidf3;

    boost::shared_ptr<AMTContainer> amt4(new AMTContainer());
    amt4->_pidf = pidf4;

    vector<boost::shared_ptr<AMTContainer> > result;
    result.push_back(amt1);
    result.push_back(amt2);
    result.push_back(amt3);
    result.push_back(amt4);

    return result;

}

void test()
{
    
    vector<boost::shared_ptr<AMTContainer> > entries = generateAMTContainers();

    // test RTDiffDistribution - just a get of params from PM
    
    // test WeightedHammingDistance 
    // NeighborJoiner

    boost::shared_ptr<WeightedHammingDistance> whd(new WeightedHammingDistance(entries));
    NeighborJoiner nj(entries);
    nj._attributes.push_back(whd);
    nj.calculateDistanceMatrix();

    // unit_assert on rows and columns
    const double epsilon = 2*numeric_limits<double>::epsilon();
    unit_assert_equal(nj._rows.at(1).at(0), 1.0666666666666666, epsilon);
    unit_assert_equal(nj._rows.at(2).at(0), 0.80, epsilon);
    unit_assert_equal(nj._rows.at(3).at(0), 0.80, epsilon);
    unit_assert_equal(nj._rows.at(2).at(1), 0.2666666666666666, epsilon);
    unit_assert_equal(nj._rows.at(3).at(1), 0.2666666666666666, epsilon);
    unit_assert_equal(nj._rows.at(3).at(2), 0, epsilon);

}

int main(int argc, char* argv[])
{
    test();
    return 0;
}
