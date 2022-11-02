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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#include "stdafx.h"
#include "shared_types.h"
#include "shared_defs.h"
#include "shared_funcs.h"
#include "proteinStore.h"
#include "AhoCorasickTrie.hpp"
#include <math.h>

using namespace std;
using namespace pwiz::util;


proteinStore proteins;

struct NETWorkerInfo
{
    size_t number;
    size_t start;
    size_t end;
    vector<double> NETStats;

    NETWorkerInfo(int num, int strt, int e)
    {
        number = num;
        start = strt;
        end = e;
        NETStats.resize(3);
        fill(NETStats.begin(), NETStats.end(),0);
    };

    NETWorkerInfo() {};
};

simplethread_handle_array_t NETWorkerHandles;
size_t numNETWorkers;
typedef map<simplethread_id_t, NETWorkerInfo*> NETWorkerThreadMap;
NETWorkerThreadMap NETWorkerThreads;
simplethread_mutex_t        resourceMutex;
CVID cleavageAgent;
boost::regex cleavageAgentRegex;
Digestion::Specificity specificity;
Digestion::Config digestionConfig;

simplethread_return_t ExecuteNETThread(simplethread_arg_t threadArg)
{

    // Get a sempahore on this function
    simplethread_lock_mutex( &resourceMutex );
    // Get the thread ID
    simplethread_id_t threadId = simplethread_get_id();
    NETWorkerThreadMap* threadMap = (NETWorkerThreadMap*) threadArg;
    // Find the data structure that is supposed to store the thread information.
    NETWorkerInfo* threadInfo = reinterpret_cast< NETWorkerInfo* >( threadMap->find( threadId )->second );
    // Realease the semaphore
    simplethread_unlock_mutex( &resourceMutex );

    cout << "Accumulating peptide termini stats..." << endl;
    for(size_t index=threadInfo->start; index <= threadInfo->end; ++index)
    {
        Peptide protein(proteins[index].getSequence());
        Digestion digestion( protein, cleavageAgentRegex, digestionConfig );
        for( Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr ) 
            ++threadInfo->NETStats[(*itr).specificTermini()];
    }
    return 0;
}

void initProteinStore()
{
    cout << "Reading fasta database ..." << endl;
    proteins.readFASTA("/hactar/fasta/20100208-IPI-Human-368-ECOLI-Cntms-reverse.fasta");
    Timer timer;
    timer.Begin();
    proteins.random_shuffle();

    cleavageAgent = Digestion::getCleavageAgentByName("trypsin");
    cleavageAgentRegex = boost::regex(Digestion::getCleavageAgentRegex(cleavageAgent));
    specificity = (Digestion::Specificity) 0;
    digestionConfig = Digestion::Config( 2, 8, 100, specificity );

    numNETWorkers = GetNumProcessors();
    size_t eachWorkerProteinCount = (size_t) ((proteins.size() * 0.05)/numNETWorkers);
    // Get a semaphore
    simplethread_lock_mutex( &resourceMutex );
    // Create a thread for each of the processor and
    // attach the procedure that needs to be executed
    // for each thread [i.e. the start() function].
    size_t proteinStartIndex = 0;
    for( size_t t = 0; t < numNETWorkers; ++t )
    {
        simplethread_id_t threadId;
        simplethread_handle_t threadHandle = simplethread_create_thread( &threadId, &ExecuteNETThread, &NETWorkerThreads );
        NETWorkerThreads[ threadId ] = new NETWorkerInfo( t, proteinStartIndex, (proteinStartIndex+eachWorkerProteinCount) );
        NETWorkerHandles.array.push_back( threadHandle );
        proteinStartIndex += eachWorkerProteinCount;
    }
    simplethread_unlock_mutex( &resourceMutex );

    simplethread_join_all( &NETWorkerHandles );
    
    
    cout << "Computing peptide termini probabilities..." << endl;

    vector<double> superVector;
    superVector.resize(3);
    for(NETWorkerThreadMap::const_iterator itr = NETWorkerThreads.begin(); itr != NETWorkerThreads.end(); ++itr)
        for(size_t index=0; index < (*itr).second->NETStats.size(); ++index)
            superVector[index] += (*itr).second->NETStats[index];

    double sum = accumulate(superVector.begin(), superVector.end(), 0.0);
    for(size_t index=0; index<superVector.size(); ++index) 
        if(superVector[index] > 0)
        {
            superVector[index] /= sum;
            cout << "key:" << index << " value:" << superVector[index] << " ln(prob):" << log(superVector[index]) << endl;
        }
    cout << "Finished computing NTT probabilities; " << timer.End() << " seconds elapsed." << endl;
    
}


void mapPeptidesToFasta(string fastafile, string peptidesfile)
{
    proteins = proteinStore( "rev_" );
    cout << "Reading fasta file " << fastafile << "..." << endl;
    proteins.readFASTA(fastafile);
    cout << "Finished reading fasta file." << endl;
    cout << "Reading peptides and building trie..." << endl;
    ifstream reader(peptidesfile.c_str(),ios::in);
    typedef AhoCorasickTrie<> ascii_trie;
    typedef ascii_trie::shared_keytype shared_string;

    vector<shared_string> keywords;
    string input;
    while(getlinePortable(reader,input))
    {
        bal::trim(input);
        if(input.size()>2)
            keywords.push_back(shared_string(new string(input)));
    }
    
    map<shared_string, map<string, size_t> > matches;

    ascii_trie trie(keywords.begin(), keywords.end());
    cout << "Finished building trie." << endl;
    cout << "Searching trie..." << endl;
    for(size_t i = 0; i < proteins.size(); ++i)
    {
        if(proteins[i].isDecoy())
            continue;
        BOOST_FOREACH(const ascii_trie::SearchResult& result, trie.find_all(proteins[i].getSequence()))
            matches[result.keyword()][proteins[i].getName()] = result.offset();
    }
    cout << "Finished searching. " << endl;

    for(size_t i = 0; i < keywords.size(); ++i)
    {
        map<string, size_t>& peptideInstances = matches[keywords[i]];
        if(peptideInstances.size() > 0)
        {
            stringstream ss;
            typedef pair<string, size_t> ProteinMatch;
            bool firstProt=true;
            BOOST_FOREACH(const ProteinMatch& pm, peptideInstances)
            {
                if(firstProt)
                {
                    ss << pm.first;
                    firstProt=false;
                }
                else
                    ss << "," << pm.first;
                
            }
            cout << *keywords[i] << "\t" << ss.str() << endl;
        }
        else
            cout << *keywords[i] << "\tnot-matched" << endl;
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if(argc < 3)
        {
            cout << "mapPeptidesToFasta <FASTA> <Peptides>" << endl;
            return 1;
        }

        vector< string > args;
        for( int i=0; i < argc; ++i )
            args.push_back( argv[i] );
        mapPeptidesToFasta(args[1],args[2]);

        return 0;
    }
    catch (exception& e)
    {
        cout << "Error: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception." << endl;
    }

    return 1;
}
