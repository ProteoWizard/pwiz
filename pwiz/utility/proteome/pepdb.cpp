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


#include "PeptideDatabase.hpp"
#include "proteome/Peptide.hpp"
#include "proteome/Ion.hpp"
#include "proteome/Chemistry.hpp"

#include "peptideSieve/fasta.h" // interface to fasta file handling
using bioinfo::fasta;

#include "peptideSieve/digest.hpp"// interface for doing trivial tryptic digests


#include "pwiz/utility/misc/Std.hpp"
#include <limits>


using namespace pwiz::proteome;


void header(const vector<string>& args)
{
    if (args.size() != 1)
        throw runtime_error("[header] Wrong number of arguments.");

    const string& filename = args[0];

    auto_ptr<const PeptideDatabase> pdb = PeptideDatabase::create(filename);

    cout << "PeptideDatabase " << filename << endl;
    cout << pdb->size() << " records.\n"; 
}


bool parseRange(const string& range, double& low, double& high)
{
    string::size_type dash = range.find('-');
    if (dash == string::npos)
        return false;

    if (dash > 0)
        low = atof(range.substr(0,dash).c_str());

    if (dash < range.size()-1)
        high = atof(range.substr(dash+1).c_str());

    return true;
}


void cat(const vector<string>& args)
{
    if (args.size() < 1)
        throw runtime_error("[cat] Wrong number of arguments.");

    const string& filename = args[0];

    double massLow = 0;
    double massHigh = numeric_limits<double>::max();
    
    enum Format {Default, MassSequence, WeightMassSequence};
    Format format = Default;

    for (unsigned int i=1; i<args.size(); i++)
    {
        if (args[i] == "2col")
            format = MassSequence;
        else if (args[i] == "3col")
            format = WeightMassSequence;
        else if (!parseRange(args[i], massLow, massHigh))
            cout << "[cat] Ignoring unknown option " << args[i] << endl; 
    }

    auto_ptr<const PeptideDatabase> pdb = PeptideDatabase::create(filename);

    PeptideDatabase::iterator begin = pdb->mass_lower_bound(massLow);
    PeptideDatabase::iterator end = pdb->mass_upper_bound(massHigh);

    cout.precision(10);
    switch (format)
    {
        case MassSequence: 
            for (PeptideDatabase::iterator it=begin; it!=end; ++it)
                cout << it->mass << " " << pdb->sequence(*it) << endl;
            break;
        case WeightMassSequence: 
            for (PeptideDatabase::iterator it=begin; it!=end; ++it)
                cout << setw(7) << it->abundance << " " << it->mass << " " << pdb->sequence(*it) << endl;
            break;
        case Default:
        default:
            for (PeptideDatabase::iterator it=begin; it!=end; ++it)
                cout << *it << " " << pdb->sequence(*it) << endl;
            break;
    }
}


void hist(const vector<string>& args)
{
    if (args.size() != 4)
        throw runtime_error("[hist] Wrong number of arguments.");

    const string& filename = args[0];
    double massLow = atof(args[1].c_str());
    double massHigh = atof(args[2].c_str());
    double binSize = atof(args[3].c_str());

    cout.precision(10);

    auto_ptr<const PeptideDatabase> pdb = PeptideDatabase::create(filename);
    
    int binCount = int((massHigh-massLow)/binSize)+1;
    cerr << "binCount: " << binCount << endl;
    vector<double> bins(binCount);
    
    PeptideDatabase::iterator begin = pdb->mass_lower_bound(massLow);
    PeptideDatabase::iterator end = pdb->mass_upper_bound(massHigh);
    
    for (PeptideDatabase::iterator it=begin; it!=end; ++it)
    {
        int bin = int((it->mass - massLow)/binSize);
        bins[bin] += it->abundance;
    }

    for (int i=0; i<binCount; i++)
        cout << massLow+binSize*i << " " << bins[i] << endl;
}


void convert_2(const vector<string>& args)
{
    if (args.size() != 2)
        throw runtime_error("[convert_2] Wrong number of arguments.");

    const string& in = args[0];
    const string& out = args[1];

    ifstream is(in.c_str());
    if (!is)
        throw runtime_error("[convert_2] Unable to open input file " + in);

    auto_ptr<PeptideDatabase> pdb = PeptideDatabase::create();
   
    while (is)
    {
        PeptideDatabaseRecord record;
        string sequence;
        is >> record.mass >> sequence;
        if (!is) break;
        pdb->append(record, sequence); 
    }

    pdb->write(out);
}


void convert_3(const vector<string>& args)
{
    if (args.size() != 2)
        throw runtime_error("[convert_3] Wrong number of arguments.");

    const string& in = args[0];
    const string& out = args[1];

    ifstream is(in.c_str());
    if (!is)
        throw runtime_error("[convert_3] Unable to open input file " + in);

    auto_ptr<PeptideDatabase> pdb = PeptideDatabase::create();
   
    while (is)
    {
        PeptideDatabaseRecord record;
        string sequence;
        is >> record.abundance >> record.mass >> sequence;
        if (!is) break;
        pdb->append(record, sequence); 
    }

    pdb->write(out);
}


void convert_ipi(const vector<string>& args)
{
    cout << "convert_ipi not implemented yet.\n";
}

inline int cmp_record_by_mass (const PeptideDatabaseRecord & a, const PeptideDatabaseRecord & b)
{
  return (a.mass < b.mass);
}


void convert_fasta(const vector<string>& args)
{
  if (args.size() != 2)
    throw runtime_error("[convert_fasta] Wrong number of arguments.");
  
  const string& in = args[0];
  const string& out = args[1];
  auto_ptr<PeptideDatabase> pdb = PeptideDatabase::create();
  
  fasta<string> mf(in);

  vector<PeptideDatabaseRecord> pdb_tmp;
  vector<string> sequences;
  int stringId = 0;
  for(fasta<string>::const_iterator seqIter = mf.begin();seqIter != mf.end();seqIter++){
    const fasta_seq<string>* fseq = *seqIter;
    const string& str = fseq->get_seq();
    //	  std::cout<<str<<endl<<endl;
    Digest peptides(str);
    for(size_t pepNdx=0;pepNdx<peptides.numPeptides();pepNdx++){
      PeptideDatabaseRecord record;
      string peptide = peptides.currentPeptide();
      peptide.erase(0,1); //drop the first character
      peptide.erase(peptide.length() - 1,1); //drop the last character
      Peptide p(peptide);
      record.mass = p.formula().monoisotopicMass();
      pwiz::chemistry::Formula f(p.formula());
      f = p.formula(); 
      //should write an assignment operator
      record.formula.C = f[pwiz::chemistry::Element::C];
      record.formula.H = f[pwiz::chemistry::Element::H];
      record.formula.N = f[pwiz::chemistry::Element::N];
      record.formula.O = f[pwiz::chemistry::Element::O];
      record.formula.S = f[pwiz::chemistry::Element::S];
      record.sequenceKey = stringId;
      stringId++;
      pdb_tmp.push_back(record);
      sequences.push_back(peptide);

      //      pdb->append(record, peptide); 
      peptides.next();	    
    }
  }
  sort(pdb_tmp.begin(),pdb_tmp.end(),cmp_record_by_mass);
  for(vector<PeptideDatabaseRecord>::iterator i=pdb_tmp.begin();i!=pdb_tmp.end();i++){
    string seq = sequences[i->sequenceKey];
    i->sequenceKey = 0;
    pdb->append(*i, seq); 
  }
  pdb->write(out);
}


void test(const vector<string>& args)
{
    auto_ptr<PeptideDatabase> pdb = PeptideDatabase::create();

    for (int i=0; i<20; i++)
    {
        PeptideDatabaseRecord record;
        record.abundance = record.mass = i;
        record.formula = PeptideDatabaseFormula(i, i, i, i, i);
        pdb->append(record, "test");
    }

    pdb->write("test.pdb");
}


typedef void (*command_type)(const vector<string>& args);

struct CommandInfo
{
    const char* name;
    command_type function;
    const char* usage;    
};

CommandInfo commandInfo_[] = 
{
    // register commands here
    {"header", header, "header filename.pdb (output header info)"},
    {"cat", cat, "cat filename.pdb [2col|3col] [[low]-[high]] (output records)"},
    {"hist", hist, "hist filename.pdb mzLow mzHigh binSize (output histogram)"}, 
    {"convert_2", convert_2, "convert_2 filename.txt filename.pdb (convert mass-sequence text database)"}, 
    {"convert_3", convert_3, "convert_3 filename.txt filename.pdb (convert weight-mass-sequence text database)"}, 
    {"convert_ipi", convert_ipi, "convert_ipi filename.fasta filename.pdb (convert ipi fasta database)"}, 
    {"convert_fasta", convert_fasta, "convert_fasta filename.fasta filename.pdb (convert fasta database)"}, 
    {"test", test, "test (creates test database test.pdb)"},
};

int commandCount_ = sizeof(commandInfo_)/sizeof(CommandInfo);

map<string, command_type> commandTable_; 

void execute(const string& command, const vector<string>& args)
{
    command_type f = commandTable_[command];
    if (!f)
        throw runtime_error("Invalid command: " + command);
    f(args);
}

void registerCommand(const CommandInfo& info)
{
    commandTable_[info.name] = info.function;
}

void initializeCommandTable()
{
    for_each(commandInfo_, commandInfo_+commandCount_, registerCommand);
}

int main(int argc, char* argv[])
{
    if (argc < 2)
    {
        cout << "Usage: pepdb command [args]" << endl;
        cout << "Commands:\n";
        for (int i=0; i<commandCount_; i++)
            cout << "    " << commandInfo_[i].usage << endl;
        return 0;
    }

    string command = argv[1];
    vector<string> args;
    copy(argv+2, argv+argc, back_inserter(args));
    initializeCommandTable();

    try
    {
        execute(command, args);
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }    
}

