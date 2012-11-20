//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "IsotopeCalculator.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::chemistry;


ostream* os_ = 0;


inline bool nonzero(int a)
{
    return a != 0;
}


// calculate multinomial probabilities manually
double probability(const vector<double>& p, const vector<int>& i)
{
    if (p.size() < i.size())
        throw runtime_error("p not big enough");

    // p = probability distribution
    // i = partition, sum(i) == n

    const int n = accumulate(i.begin(), i.end(), 0);
    
    // calculate p0^i0 * p1^i1 * ... * pr^ir * C(n; i0, ... , ir)

    vector<int> p_count = i, C_count = i;
    int n_count = n;

    double result = 1;
    
    for (int count=0; count<3*n; )
    {
        if (n_count && result<=1)
        {
            //cout << count << ") multiply: " << n_count << endl;
            result *= n_count--;
            count++;
        }
       
        while ((result>=1 || n_count==0) && accumulate(C_count.begin(), C_count.end(), 0))
        {
            vector<int>::iterator it = find_if(C_count.begin(), C_count.end(), nonzero);
            if (it == C_count.end()) throw runtime_error("blech");
            //cout << count << ") divide: " << *it << endl;
            result /= (*it)--;
            count++;
        }

        while ((result>=1 || n_count==0) && accumulate(p_count.begin(), p_count.end(), 0))
        {
            vector<int>::iterator it = find_if(p_count.begin(), p_count.end(), nonzero);
            if (it == p_count.end()) throw runtime_error("blech2");
            size_t index = it - p_count.begin();
            //cout << count << ") multiply: " << p[index] << endl;
            result *= p[index];
            (*it)--;
            count++;
        }
    }

    return result;
}


void testUsage(const IsotopeCalculator& calc)
{
    Formula angiotensin("C50 H71 N13 O12 ");
    Formula bombesin("C71 H110 N24 O18 S1");
    Formula substanceP("C63 H98 N18 O13 S1");
    Formula neurotensin("C78 H121 N21 O20");
    Formula alpha1_6("C45 H59 N11 O8");

    MassDistribution md = calc.distribution(neurotensin, 2);
    if (os_) *os_ << "MassDistribution:\n" << md << endl;
}


void testProbabilites(const IsotopeCalculator& calc)
{
    const MassDistribution& md = Element::Info::record(Element::C).isotopes;

    if (os_) *os_ << "C distribution: " << md << endl;

    vector<double> p;
    for (MassDistribution::const_iterator it=md.begin(); it!=md.end(); ++it)
        p.push_back(it->abundance);

    vector<int> neutron0; neutron0.push_back(100);
    vector<int> neutron1; neutron1.push_back(99); neutron1.push_back(1);
    vector<int> neutron2; neutron2.push_back(98); neutron2.push_back(2);
    vector<int> neutron3; neutron3.push_back(97); neutron3.push_back(3);
    vector<int> neutron4; neutron4.push_back(96); neutron4.push_back(4);

    vector<double> abundances;
    abundances.push_back(probability(p, neutron0));
    abundances.push_back(probability(p, neutron1));
    abundances.push_back(probability(p, neutron2));
    abundances.push_back(probability(p, neutron3));
    abundances.push_back(probability(p, neutron4));

    if (os_)
    {
        *os_ << "C100 abundances (manually calculated):\n";
        copy(abundances.begin(), abundances.end(), ostream_iterator<double>(*os_, "\n"));
        *os_ << endl;
    }

    MassDistribution md_C100 = calc.distribution(Formula("C100"));
    if (os_) *os_ << "C100 distribution (from IsotopeCalculator):\n"
                  << md_C100 << endl;

    // compare manually calculated probabilities to those returned by IsotopeCalculator
    for (unsigned int i=0; i<abundances.size(); i++)
        unit_assert_equal(abundances[i], md_C100[i].abundance, 1e-10);
}


void testNormalization(const IsotopeCalculator& calc)
{
    if (os_) *os_ << "mass normalized:\n"
                  << calc.distribution(Formula("C100"), 0, 
                                       IsotopeCalculator::NormalizeMass) << endl; 

    if (os_) *os_ << "abundance normalized:\n"
                  << calc.distribution(Formula("C100"), 0, 
                                       IsotopeCalculator::NormalizeAbundance) << endl; 
    
    MassDistribution md = calc.distribution(Formula("C100"), 0, 
                                            IsotopeCalculator::NormalizeMass |
                                            IsotopeCalculator::NormalizeAbundance);
    if (os_) *os_ << "both normalized:\n" << md << endl;

    double sumSquares = 0;
    for (MassDistribution::iterator it=md.begin(); it!=md.end(); ++it)
        sumSquares += it->abundance * it->abundance;
    if (os_) *os_ << "sumSquares: " << sumSquares << endl;

    unit_assert_equal(sumSquares, 1, 1e-12);
    unit_assert(md[0].mass == 0);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "IsotopeCalculatorTest\n" << setprecision(12);

        //IsotopeCalculator calc(1e-10, .2);
        IsotopeCalculator calc(1e-3, .2);
        testUsage(calc);
        testProbabilites(calc);
        testNormalization(calc);
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
