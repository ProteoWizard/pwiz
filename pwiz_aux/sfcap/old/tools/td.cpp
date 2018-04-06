//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
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


#include "data/TransientData.hpp"
#include <iostream>
#include <sstream>
#include <map>
#include <stdexcept>
#include <iterator>
#include <iomanip>
#include <algorithm>


using namespace std;
using namespace pwiz::data;


typedef int (*Subcommand)(const vector<string>& args);
map<string, Subcommand> subcommands_;
string usage_("Usage: td subcommand [args]\nSubcommands:\n");


int cat(const vector<string>& args)
{
    if (args.size() != 1) throw runtime_error(usage_);
    const string& filename = args[0];

    TransientData td(filename); 
    cout.precision(14);

    copy(td.data().begin(), td.data().end(), ostream_iterator<double>(cout, "\n"));

    return 0;
}


int cathead(const vector<string>& args)
{
    if (args.size() != 1) throw runtime_error(usage_);
    const string& filename = args[0];

    TransientData td(filename); 
    cout.precision(14);

    cout << "startTime: " << td.startTime() << endl;
    cout << "observationDuration: " << td.observationDuration() << endl;
    cout << "A: " << td.A() << endl;
    cout << "B: " << td.B() << endl;
    cout << "bandwidth: " << td.bandwidth() << endl;
    cout << "magneticField: " << td.magneticField() << endl;
    cout << "sampleCount: " << td.data().size() << endl;

    return 0;
}


int digitCount(int N)
{
    ostringstream oss;
    oss << N;
    return oss.str().size();
}


string partitionFilename(const string& filename, int i, int digitCount)
{
    ostringstream oss;
    oss << filename << "." << setw(digitCount) << setfill('0') << i;
    return oss.str();
}


int partition(const vector<string>& args)
{
    if (args.size() != 2) throw runtime_error(usage_);
    const string& filename = args[0];
    int partitionCount = atoi(args[1].c_str());
    if (partitionCount < 2) 
        throw runtime_error("Nothing to do.");

    TransientData td(filename); 
    if (td.data().size() % partitionCount != 0) 
        throw runtime_error("Must choose an even partition.");

    int partitionSize = td.data().size()/partitionCount;
    double partitionDuration = td.observationDuration()/partitionCount;

    for (int i=0; i<partitionCount; i++)
    {
        TransientData partition;
        partition.startTime(td.startTime() + partitionDuration*i);
        partition.observationDuration(partitionDuration); 
        partition.A(td.A());
        partition.B(td.B());
        partition.data().resize(partitionSize);
        copy(td.data().begin()+(partitionSize*i), 
             td.data().begin()+(partitionSize*(i+1)), 
             partition.data().begin());
        partition.write(partitionFilename(filename, i, digitCount(partitionCount-1)));
    }

    return 0;
}


void initializeSubcommands()
{
    subcommands_["cat"] = cat;
    usage_ += "    cat filename.dat\n";
    
    subcommands_["cathead"] = cathead;
    usage_ += "    cathead filename.dat\n";
    
    subcommands_["partition"] = partition;
    usage_ += "    partition filename.dat partitionCount\n";
}


int main(int argc, char* argv[])
{
    try
    {
        initializeSubcommands();
        Subcommand subcommand = argc>1 ? subcommands_[argv[1]] : 0;
        if (!subcommand) throw runtime_error(usage_);

        vector<string> args;
        copy(argv+2, argv+argc, back_inserter(args));
        
        return subcommand(args);
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

