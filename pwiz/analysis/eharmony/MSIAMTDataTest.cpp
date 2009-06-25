///
/// MSIAMTDataTest.cpp 
///

#include "MSIAMTData.hpp"
#include <iostream>
#include <fstream>
#include <string>

using namespace std;
using namespace pwiz;
using namespace eharmony;

int main()
{
    ifstream ifs("msi_aligned_database.xml");
    MSIAMTData data;
    data.read(ifs);

    /*
    ostringstream oss;
    XMLWriter writer(oss);
    data.write(writer);
    cout << oss.str() << endl;
    */
    
    ofstream ofs("msi_aligned_database.tsv");
    ofstream r("r_aligned.tsv");
    vector<PeptideEntry>::iterator it = data.peptideEntries.begin();
    for(; it != data.peptideEntries.end(); ++it)
        {
            vector<ModificationStateEntry>::iterator jt = it->modificationStateEntries.begin();
            for(; jt != it->modificationStateEntries.end(); ++jt)
                {
                    vector<Observation>::iterator kt = jt->observations.begin();
                    for(; kt != jt->observations.end(); ++kt)
                        {
                            ofs << jt->modifiedMass << "\t" << kt->observedHydrophobicity << "\t" << it->peptideSequence << "\n";
                            r << jt->modifiedMass << "\t" << kt->observedHydrophobicity << "\n";
                        }

                }

        }

    return 0;

}
