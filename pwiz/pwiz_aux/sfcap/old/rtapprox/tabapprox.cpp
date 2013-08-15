//
// $Id$
//
//
// Robert Burke <robert.burke@cshs.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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


#include <boost/program_options.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/matrix_proxy.hpp>

#include <iostream>
#include <fstream>
#include <iomanip>
#include <vector>
#include <string>

#include "FitFromPepXML.hpp"
#include "util/auto_vector.h"
#include "pepxml/Peptide.hpp"

#include "utils.hpp"

namespace ublas = boost::numeric::ublas;

using namespace std;
using namespace pwiz::pepxml;
using namespace pwiz::rtapprox;

int hasJunk = 0;
int hasProb = 1;

// Hack of a program used to read in a tab delimited file of peptides,
// retention times, and - perhaps - confidence values (aka p values).
int main(int argc, char** argv)
{
    *argv++;
    argc--;
    
    // Read in the input file
    if (argc < 1)
    {
        cout << "test <input file>" << endl;
        return 1;
    }

    // Create a list of Peptide objects from the input tab file.
    vector<Peptide> peptides;

    ifstream in(*argv);

    string line;
    while(getline(in, line))
    {
        istringstream ss(line);
        string junk;
        string peptide;
        string rt;
        string prob;

        if (hasJunk)
            ss >> junk;
        ss >> peptide;
        ss >> rt;
        if (hasProb)
            ss >> prob;

        Peptide p;
        p.setPeptide(peptide);
        p.setRetentionTime(rt);
        if (hasProb)
            p.setPValue(prob);
        peptides.push_back(p);
    }

    // Create dummy fit
    auto_ptr<FitFromPepXML> fit = FitFromPepXML::create();

    // Fit the read in peptides to some system.
    fit->createSystem(peptides);

    ublas::vector<double> result = fit->fit();

    for (int i=0; i<FitFromPepXML::NUMENTRIES; i++)
    {
        cout << FitFromPepXML::AMINOACIDS[i] << "\t";
    }
    
    cout << "\nresult size: " << result.size() << "\n";
    for (ublas::vector<double>::iterator i=result.begin(); i!=result.end(); i++)
    {
        cout << *i << "\t";
    }
    
    cout << "\npeptide\testimated\terror\n";

    // Check results
    double rms = 0;
    for (vector<Peptide>::iterator i=peptides.begin();i!=peptides.end(); i++)
    {
        double actualRt = atof((*i).getRetentionTime());
        ublas::vector<double> v = fit->countPeptide((*i).getPeptide());

        // This can be replaced with boost's inner_prod, but keeping
        // it manual for now.
        double rt = 0;
        for (ublas::vector<double>::size_type j=0; j<v.size()-1; j++)
            rt += v(j) * result(j);

        double error = (actualRt - rt);
        rms += error * error;
        cout << (*i).getPeptide() << "\t" << rt << "\t" << error << "\n";
    }

    rms = sqrt(rms / peptides.size() );
    cout << "\n\nrms = " << rms <<endl;
    
    return 0;
}
