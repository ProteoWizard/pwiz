//
// $Id$
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

#include "Peptide.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>


using namespace pwiz::proteome;


struct MassAbundance
{
    double mass;
    double abundance;

    MassAbundance(double _mass, double _abundance)
    :   mass(_mass),
        abundance(_abundance)
    {}
};


ostream& operator<<(ostream& os, const MassAbundance& ma)
{
    os << ma.mass << " " << ma.abundance;
    return os;
}


typedef vector<MassAbundance> MassDistribution;
vector<MassDistribution> isotopes(Peptide::ElementCount);


void initializeIsotopes()
{
    using namespace Peptide;

    isotopes[C].push_back(MassAbundance(12, 0.9893));
    isotopes[C].push_back(MassAbundance(13.0033548378, 0.0107));

    isotopes[H].push_back(MassAbundance(1.00782503207, 0.999885));
    isotopes[H].push_back(MassAbundance(2.0141017778, 0.000115));

    isotopes[N].push_back(MassAbundance(14.0030740048, 0.99636));
    isotopes[N].push_back(MassAbundance(15.0001088982, 0.00364));

    isotopes[O].push_back(MassAbundance(15.99491461956, 0.99757));
    isotopes[O].push_back(MassAbundance(16.99913170, 0.00038));
    isotopes[O].push_back(MassAbundance(17.9991610, 0.00205));

    isotopes[S].push_back(MassAbundance(31.97207100, 0.9499));
    isotopes[S].push_back(MassAbundance(32.97145876, 0.0075));
    isotopes[S].push_back(MassAbundance(33.96786690, 0.0425));
    isotopes[S].push_back(MassAbundance(35.96708076, 0.0001));
}


MassDistribution calculateDistributionFor2Isotopes(const MassDistribution& isotopeDistribution, int atomCount)
{
    MassDistribution result;

    if (isotopeDistribution.size() != 2)
        throw runtime_error("only implemented for 2 isotopes");

    double m0 = isotopeDistribution[0].mass;
    double p0 = isotopeDistribution[0].abundance;
    double m1 = isotopeDistribution[1].mass;
    double p1 = isotopeDistribution[1].abundance;

    double mass = m0 * atomCount;
    double abundance = pow(p0, atomCount);

    //for (int i=0; i<=atomCount; i++)
    for (int i=0; i<=4; i++)
    {
        result.push_back(MassAbundance(mass, abundance));
        mass += (m1-m0);
        abundance *= p1/p0*(atomCount-i)/(i+1);
    }

    return result;
}


void test()
{
    using namespace Peptide;
    cout.precision(12);

    initializeIsotopes();

    for (Element e=ElementBegin; e!=ElementEnd; ++e)
    {
        cout << e << ":\n";
        copy(isotopes[e].begin(), isotopes[e].end(), ostream_iterator<MassAbundance>(cout, "\n")); 
    }
    cout << endl;


    for (Element e=ElementBegin; e!=ElementEnd; ++e)
    {
        if (isotopes[e].size() != 2) continue;
        MassDistribution test = calculateDistributionFor2Isotopes(isotopes[e], 100); 
        cout << e << ":\n"; 
        copy(test.begin(), test.end(), ostream_iterator<MassAbundance>(cout, "\n"));
        cout << endl;
    }
}

    

typedef pair<string, Peptide::Formula> PepDatum;
typedef vector<PepDatum> PepData;


ostream& operator<<(ostream& os, const PepDatum& datum)
{
    using namespace pwiz::calibration; // for Ion

    const string& name = datum.first;
    const Peptide::Formula& f = datum.second;

    cout << name << " " << f << " " << f.monoisotopicMass() << endl;
    for (int i=1; i<6; i++)
        cout << Ion::mz(f.monoisotopicMass(), i) << " ";
    cout << "\n"; 

    return os;
}


PepData create5PeptideData()
{
    using Peptide::Formula;
    PepData peptides;
    peptides.push_back(make_pair("Angiotensin II", Formula(50, 71, 13, 12)));
    peptides.push_back(make_pair("Bombesin", Formula(71, 110, 24, 18, 1)));
    peptides.push_back(make_pair("Substance P", Formula(63, 98, 18, 13, 1)));
    peptides.push_back(make_pair("Neurotensin", Formula(78, 121, 21, 20)));
    peptides.push_back(make_pair("Alpha1-6", Formula(45, 59, 11, 8)));
    return peptides; 
}


MassDistribution multiply(const MassDistribution& m, const MassDistribution& n)
{
    MassDistribution result;

    for (MassDistribution::const_iterator i=m.begin(); i!=m.end(); ++i)
    for (MassDistribution::const_iterator j=n.begin(); j!=n.end(); ++j)
        result.push_back(MassAbundance(i->mass + j->mass, i->abundance * j->abundance));

    return result;
}


bool isMoreAbundant(const MassAbundance& a, const MassAbundance& b)
{
    return a.abundance > b.abundance;
}


void printEnvelope(const MassDistribution& md, int charge)
{
    using namespace pwiz::calibration; // for Ion

    for (MassDistribution::const_iterator it=md.begin(); it!=md.end(); ++it)
    {
        if (it->abundance < .0001) continue;
        cout << Ion::mz(it->mass, charge) << " " << it->abundance << endl;
    }
}


void calculateIsotopeEnvelope(const PepDatum& peptide)
{
    using namespace Peptide;
    const string& name = peptide.first;
    const Peptide::Formula f = peptide.second;

    cout << name << endl << endl;

    MassDistribution c = calculateDistributionFor2Isotopes(isotopes[C], f[C]); 
    cout << "C envelope:\n";
    copy(c.begin(), c.end(), ostream_iterator<MassAbundance>(cout, "\n"));
    cout << endl;

    MassDistribution n = calculateDistributionFor2Isotopes(isotopes[N], f[N]); 
    cout << "N envelope:\n";
    copy(n.begin(), n.end(), ostream_iterator<MassAbundance>(cout, "\n"));
    cout << endl;

    // calculate product of C and N envelopes
    MassDistribution cn = multiply(c,n);
    sort(cn.begin(), cn.end(), isMoreAbundant);
    cout << "C*N envelope:\n";
    copy(cn.begin(), cn.begin()+5, ostream_iterator<MassAbundance>(cout, "\n"));
    cout << endl;

    // hack to add back in H, O, S 
    Peptide::Formula hack(0, f[H], 0, f[O], f[S]);
    double hackMass = hack.monoisotopicMass();
    for (MassDistribution::iterator it=cn.begin(); it!=cn.end(); ++it)
        it->mass += hackMass;

    // print envelopes by charge state
    for (int i=1; i<5; i++)
    {
        cout << "charge state " << i << endl;
        printEnvelope(cn, i);
        cout << endl;
    }
}


void test5pep()
{
    cout.precision(12);
    
    PepData peptides = create5PeptideData();

    for (PepData::iterator it=peptides.begin(); it!=peptides.end(); ++it)
        cout << *it << endl; // "copy" doesn't compile (!?)

    initializeIsotopes();

    for (int i=0; i<5; i++)
        calculateIsotopeEnvelope(peptides[i]);
}

#include "boost/multi_array.hpp"

void testMultiArray()
{
    cout << "boo\n";

      // Create a 3D array that is 3 x 4 x 2
      typedef boost::multi_array<double, 3> array_type;
      typedef array_type::index index;
      array_type A(boost::extents[3][4][2]);

      // Assign values to the elements
      int values = 0;
      for(index i = 0; i != 3; ++i) 
        for(index j = 0; j != 4; ++j)
          for(index k = 0; k != 2; ++k)
            A[i][j][k] = values++;

      // Verify values
      int verify = 0;
      for(index i = 0; i != 3; ++i) 
      {  
        for(index j = 0; j != 4; ++j)
        {
          for(index k = 0; k != 2; ++k)
          {
            cout << A[i][j][k] << " ";
            assert(A[i][j][k] == verify++);
          }
          cout << endl;
        }
        cout << endl;
      }


    cout << "ok\n";
}



int main()
{
    try
    {
        //test();
        //test5pep();
        testMultiArray();
        return 0; 
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}


