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

#include "pwiz/utility/misc/Std.hpp"


struct TextRecord
{
    int atomicNumber;
    string symbol;
    double relativeAtomicMass;
    double isotopicComposition;
    double standardAtomicWeight;
};


istream& operator>>(istream& is, TextRecord& tr)
{
    vector<string> lines(7);

    while (is && lines[0].find("Atomic Number") != 0)
        getline(is, lines[0]);
        
    if (!is) return is;

    for (int i=1; i<7; i++)
        getline(is, lines[i]);

    for (int i=0; i<7; i++)
    {
        istringstream iss(lines[i]);
        vector<string> tokens;
        copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));
        if (tokens.empty()) throw runtime_error("blech");

        const string& value = tokens[tokens.size()-1];

        switch (i)
        {
            case 0:
                tr.atomicNumber = atoi(value.c_str());
                break;
            case 1:
                tr.symbol = value;
                break;
            case 3:
                tr.relativeAtomicMass = atof(value.c_str()); 
                break;
            case 4:
                tr.isotopicComposition = atof(value.c_str()); 
                break;
            case 5:
                tr.standardAtomicWeight = atof(value.c_str()); 
                break;
        }
    }

    return is;
}


ostream& operator<<(ostream& os, const TextRecord& tr)
{
    os << 
        tr.atomicNumber << " " <<
        tr.symbol << " " <<
        tr.relativeAtomicMass << " " <<
        tr.isotopicComposition << " " <<
        tr.standardAtomicWeight;

    return os;
}


struct Isotope 
{
    double mass;
    double abundance;
};


ostream& operator<<(ostream& os, const Isotope& isotope)
{
    os << "{" << isotope.mass << ", " << isotope.abundance << "}";
    return os;
}


struct Info
{
    int atomicNumber;
    string symbol;
    double standardAtomicWeight;
    vector<Isotope> isotopes;
};


ostream& operator<<(ostream& os, const Info& info)
{
    os << 
        info.atomicNumber << " " <<
        info.symbol << " " <<
        info.standardAtomicWeight << " ";

    copy(info.isotopes.begin(), info.isotopes.end(), ostream_iterator<Isotope>(os, " "));

    return os;
}


void textRecordsToInfo(const vector<TextRecord>& textRecords, map<int, Info>& infos)
{
    for (vector<TextRecord>::const_iterator it=textRecords.begin(); it!=textRecords.end(); ++it)
    {
        if (it->atomicNumber == 0) continue;
        
        if (infos.count(it->atomicNumber) == 0)
        {
            // add new info
            Info info;
            info.atomicNumber = it->atomicNumber;
            info.symbol = it->symbol;
            info.standardAtomicWeight = it->standardAtomicWeight;
            infos[it->atomicNumber] = info;
        }

        // add isotope
        Isotope isotope;
        isotope.mass = it->relativeAtomicMass;
        isotope.abundance = it->isotopicComposition / 100;
        infos[it->atomicNumber].isotopes.push_back(isotope);
    }
}


void printCode(const vector<TextRecord>& textRecords)
{
    map<int, Info> infos;
    textRecordsToInfo(textRecords, infos);

    // enumeration of symbols
    cout << "enum Type\n{";
    for (map<int,Info>::iterator it=infos.begin(); it!=infos.end(); ++it)
    {
        static int count = 0;
        const Info& info = it->second;
        if (count++%10 == 0) cout << "\n    ";
        cout << info.symbol;
        if (count!=(int)infos.size()) cout << ", "; 
    }
    cout << "\n};\n\n";

    // isotope data
    for (map<int,Info>::iterator it=infos.begin(); it!=infos.end(); ++it)
    {
        const Info& info = it->second;

        cout << "Isotope isotopes_" << info.symbol << "[] = { ";
        copy(info.isotopes.begin(), info.isotopes.end(), ostream_iterator<Isotope>(cout, ", "));
        cout << "};\n";

        cout << "const int isotopes_" << info.symbol << "_size = sizeof(isotopes_" << info.symbol <<
            ")/sizeof(Isotope);\n\n";
    }
    cout << endl; 
    
    // element data
    cout << "Element elements[] =\n{\n";
    for (map<int,Info>::iterator it=infos.begin(); it!=infos.end(); ++it)
    {
        const Info& info = it->second;
        
        cout << "    { " << 
            info.symbol << ", " <<
            "\"" << info.symbol << "\", " <<
            info.atomicNumber << ", " << 
            info.standardAtomicWeight << ", " <<
            "isotopes_" << info.symbol << ", " << 
            "isotopes_" << info.symbol << "_size },\n";
    }
    cout << "};\n";

    cout << "const int elementsSize = sizeof(elements)/sizeof(Element);\n";
}


void parse_isotopes(const string& filename)
{
    ifstream is(filename.c_str());
    if (!is)
        throw runtime_error(("Unable to open file: " + filename).c_str());

    // read in the records
    vector<TextRecord> textRecords;
    copy(istream_iterator<TextRecord>(is), istream_iterator<TextRecord>(), back_inserter(textRecords));

    // print out the records
    cout.precision(12);
    copy(textRecords.begin(), textRecords.end(), ostream_iterator<TextRecord>(cout, "\n"));
    cout << endl;

    // print out some code
    printCode(textRecords);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc < 2)
            throw runtime_error("Usage: parse_isotopes filename");
        
        const string& filename = argv[1];
        parse_isotopes(filename);
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

