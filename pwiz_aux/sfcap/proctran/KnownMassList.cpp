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


#include "KnownMassList.hpp"
#include "pwiz/utility/proteome/Peptide.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "boost/lexical_cast.hpp"
#include <boost/random.hpp>
#include <iostream>
#include <iomanip>
#include <iterator>
#include <algorithm>


namespace pwiz {
namespace pdanalysis {


using namespace std;
using namespace pwiz::proteome;
using boost::lexical_cast;


class KnownMassList::Impl
{
public:
  
  void insert(const Entry& entry) {entries.push_back(entry);}
  void insert(const Formula& formula, const string& name, const IntegerSet& chargeStates);
  void insert_5pep();
  void insert_calmix();
  void insert_db(std::vector<std::string>  peptideVector);
  void insert_entryVector(vector<KnownMassList::Entry> entryVect);
  void replace_entryVector(vector<KnownMassList::Entry> entryVect);
  vector<KnownMassList::Entry> splitEntries(double percentage);
  
  MatchResult match(const Scan& scan, double epsilon) const;
  
  typedef vector<KnownMassList::Entry> Entries;
  Entries entries;
};


void KnownMassList::Impl::insert(const Formula& formula, 
                                 const string& name, 
                                 const IntegerSet& chargeStates) 
{
    double neutralMass = formula.monoisotopicMass();

    for (IntegerSet::const_iterator it=chargeStates.begin(); it!=chargeStates.end(); ++it)
    {
        Entry entry;
        entry.mz = Ion::mz(neutralMass, *it);
        entry.label = name + "(+" + lexical_cast<string>(*it) + ")";
        entries.push_back(entry);
    }
}


void KnownMassList::Impl::insert_5pep()
{
    insert(Peptide("DRVYIHPF").formula(), "AngiotensinII", IntegerSet(1,4));
    insert(Peptide("WHWLQL").formula(), "Alpha1-6", IntegerSet(1,3));
    insert(Formula("C71 H110 N24 O18 S1"), "Bombessin", IntegerSet(1,4));
    insert(Formula("C63 H98 N18 O13 S1"), "SubstanceP", IntegerSet(1,3)); 
    insert(Formula("C78 H121 N21 O20"), "Neurotensin", IntegerSet(1,5)); 
}

void KnownMassList::Impl::insert_db(std::vector<std::string> peptideVector)
{
  for(vector<string>::iterator i = peptideVector.begin();i!=peptideVector.end();++i){
    insert(Peptide(*i).formula(), *i, IntegerSet(1,6));
  }
}


Formula ultramarkFormula(int carbonCount)
{
    using namespace Chemistry::Element;

    if (carbonCount<17 || carbonCount>38)
        throw runtime_error("[KnownMassList] Invalid carbon count for ultramark.");

    Formula result("H17 O6 N3 P3");
    result[C] = carbonCount; 
    result[F] = 22 + (carbonCount-17)*2; 
    if (carbonCount%2 == 0) result[H] += 1;

    return result;
}


void KnownMassList::Impl::insert_calmix()
{
    insert(Peptide("MRFA").formula(), "MRFA", IntegerSet(1,4));
    insert(Formula("C8 H10 N4 O2"), "Caffeine", IntegerSet(1,4));

    for (int i=17; i<=38; i++)
        insert(ultramarkFormula(i), 
               "Ultramark(" + lexical_cast<string>(i) + ")",
               IntegerSet(1,4));
}

void KnownMassList::Impl::insert_entryVector(vector<KnownMassList::Entry> entryVect)
{
  
  for (vector<KnownMassList::Entry>::iterator i=entryVect.begin(); i!=entryVect.end(); ++i){
    entries.push_back(*i);
  }
}

void KnownMassList::Impl::replace_entryVector(vector<KnownMassList::Entry> entryVect)
{
  entries.clear();
  insert_entryVector(entryVect);
}

vector<KnownMassList::Entry> KnownMassList::Impl::splitEntries(double percentage){
  vector<KnownMassList::Entry> tmpEntries = entries;
  vector<KnownMassList::Entry> retEntries;

  entries.clear();
  boost::mt19937 rng;                 // merseinne twister random generator
  
  boost::uniform_int<> distribution(1,int(1.0/percentage));       // distribution that maps to 1..6
  boost::variate_generator<boost::mt19937&, boost::uniform_int<> >
    rnGen(rng, distribution);             // glues randomness with mapping
  
  for(Entries::iterator i=tmpEntries.begin();i!=tmpEntries.end();++i){
    int x = rnGen();                      // get random number
    if(x>1){
      entries.push_back(*i);
    }
    else{
      retEntries.push_back(*i);
    }
  }
  return retEntries;
}


namespace {

inline bool hasLesserLabel(const KnownMassList::Entry& a, const KnownMassList::Entry& b) 
{
    return a.label < b.label;
}

inline bool hasLesserMZMonoisotopic(const PeakFamily& a, const PeakFamily& b)
{
    return a.mzMonoisotopic < b.mzMonoisotopic;
}

inline bool areClose(double a, double b, double epsilon) {return abs((1.0e6/a) * (a-b)) <epsilon;}

} // namespace


void verifyMatch(const PeakFamily& peakFamily, 
                 double epsilon,
                 KnownMassList::Match& result)
{
    if (!result.entry)
        throw runtime_error("[KnownMassList] Match::entry not filled in.");

    // if peakFamily is close to result.entry, fill in peakFamily and calculate error

    if (!areClose(peakFamily.mzMonoisotopic, result.entry->mz, epsilon))
        return;

    //    cout<< setprecision(10) <<epsilon<<" "<<peakFamily.mzMonoisotopic<<"\t"<<result.entry->mz<<"\t"<<epsilon<<"\t"<<(1.0e6 / result.entry->mz) * ( peakFamily.mzMonoisotopic - result.entry->mz)<<endl;
    
    if (result.peakFamily)
    {
        cerr << result.peakFamily->mzMonoisotopic << " " << peakFamily.mzMonoisotopic << endl;
        throw runtime_error("[KnownMassList::verifyMatch()] Multiple peaks match single known mass.");
    }

    result.peakFamily = &peakFamily;
    result.dmz = peakFamily.mzMonoisotopic - result.entry->mz;
    result.dmz2 = result.dmz * result.dmz;
    result.dmzRel = result.dmz / result.entry->mz;
    result.dmzRel2 = result.dmzRel * result.dmzRel; 
}


void KnownMassList::MatchResult::reComputeStatistics()
{
  for (std::vector<Match>::iterator i=matches.begin(); i!=matches.end(); ++i){
    Match& match = *i;
    if (match.peakFamily){
      ++matchCount;
      dmzMean += match.dmz;
      dmz2Mean += match.dmz2;
      dmzRel2Mean += match.dmzRel2;
    }
  }
  
  if (matchCount){
    dmzMean /= matchCount;
    dmz2Mean /= matchCount;
    dmzRel2Mean /= matchCount;
  }
}

std::vector<KnownMassList::Entry> KnownMassList::MatchResult::getMatchedEntries(){
  std::vector<KnownMassList::Entry> matchedEntries;
  for (std::vector<Match>::iterator i=matches.begin(); i!=matches.end(); ++i){
    if(i->peakFamily){
      matchedEntries.push_back(*(i->entry));
    }
  }
  return matchedEntries;
}

KnownMassList::MatchResult KnownMassList::Impl::match(const Scan& scan, double epsilon) const
{
    MatchResult result;
    result.matches.resize(entries.size());

    for (unsigned int i=0; i<entries.size(); ++i)
    {
        Match& match = result.matches[i];
        match.entry = &entries[i]; 

        PeakFamily temp;
        temp.mzMonoisotopic = match.entry->mz; 

        vector<PeakFamily>::const_iterator lb = lower_bound(scan.peakFamilies.begin(),
                                                            scan.peakFamilies.end(),
                                                            temp,
                                                            hasLesserMZMonoisotopic);
        if (lb != scan.peakFamilies.begin())
            verifyMatch(*(lb-1), epsilon, match); 

        if (lb != scan.peakFamilies.end())
            verifyMatch(*lb, epsilon, match); 

        if (match.peakFamily)
        {
            ++result.matchCount;
            result.dmzMean += match.dmz;
            result.dmz2Mean += match.dmz2;
            result.dmzRel2Mean += match.dmzRel2;
        }
    }

    if (result.matchCount)
    {
        result.dmzMean /= result.matchCount;
        result.dmz2Mean /= result.matchCount;
        result.dmzRel2Mean /= result.matchCount;
    }

    return result;
}



// KnownMassList implementation

KnownMassList::KnownMassList() : impl_(new Impl) {}

KnownMassList::~KnownMassList() {} // auto destruction of impl_

void KnownMassList::insert(const KnownMassList::Entry& entry) {impl_->insert(entry);}

void KnownMassList::insert(const Formula& formula, 
                           const string& name,
                           const IntegerSet& chargeStates)
{
    impl_->insert(formula, name, chargeStates);
}

void KnownMassList::insert_5pep() {impl_->insert_5pep();}
void KnownMassList::insert_calmix() {impl_->insert_calmix();}
void KnownMassList::insert_db(std::vector<std::string> peptideVector) {impl_->insert_db(peptideVector);}

void KnownMassList::insert_entryVector(vector<KnownMassList::Entry> entryVector) {impl_->insert_entryVector(entryVector);}
void KnownMassList::replace_entryVector(vector<KnownMassList::Entry> entryVector) {impl_->replace_entryVector(entryVector);}
vector<KnownMassList::Entry> KnownMassList::splitEntries(double percentage){return impl_->splitEntries(percentage);}


KnownMassList::MatchResult KnownMassList::match(const Scan& scan, double epsilon) const
{
    return impl_->match(scan, epsilon);
}

ostream& operator<<(ostream& os, const KnownMassList::Entry& entry)
{
    os << entry.mz << "\t" << entry.label;
    return os;
}

ostream& operator<<(ostream& os, const KnownMassList::Match& match)
{
    if (!match.entry)
        throw runtime_error("[KnownMassList::operator<<] Null pointer in Match.");

    os << *match.entry << ": ";

    if (match.peakFamily)
       os << match.peakFamily->mzMonoisotopic << " "
          << "dmz=" << match.dmz << " "
          << "dmz2=" << match.dmz2;
    else
        os << "no match";

    return os;
}

ostream& operator<<(ostream& os, const KnownMassList::MatchResult& matchResult)
{
    copy(matchResult.matches.begin(), matchResult.matches.end(), 
         ostream_iterator<KnownMassList::Match>(os,"\n"));
    cout << "matchCount: " << matchResult.matchCount << endl;
    cout << "dmzMean: " << matchResult.dmzMean << endl;
    cout << "dmz2Mean: " << matchResult.dmz2Mean << endl;
    cout << "dmzRel2Mean: " << matchResult.dmzRel2Mean << endl;
    cout << "rms absolute: " << sqrt(matchResult.dmz2Mean) << endl;
    cout << "rms relative: " << sqrt(matchResult.dmzRel2Mean) << endl;
    return os;
}

ostream& operator<<(ostream& os, const KnownMassList& kml)
{
    sort(kml.impl_->entries.begin(), kml.impl_->entries.end(), hasLesserLabel);
    copy(kml.impl_->entries.begin(), kml.impl_->entries.end(), 
         ostream_iterator<KnownMassList::Entry>(os,"\n"));
    return os;
}


} // namespace pdanalysis 
} // namespace pwiz


