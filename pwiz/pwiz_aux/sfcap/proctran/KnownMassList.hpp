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


#ifndef _KNOWNMASSLIST_HPP_
#define _KNOWNMASSLIST_HPP_


#include "pwiz/data/misc/PeakData.hpp"
#include "pwiz/utility/proteome/Peptide.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include <string>
#include <vector>
#include <memory>
#include <iosfwd>


namespace pwiz {
namespace pdanalysis {


using namespace pwiz::data::peakdata; // import names from peakdata into this namespace 
using pwiz::chemistry::Formula;
using pwiz::util::IntegerSet;


class KnownMassList
{
    public:

    KnownMassList();
    ~KnownMassList();

    struct Entry
    {
        double mz;
        std::string label;
        
        Entry(double _mz = 0, std::string _label = "") 
        : mz(_mz), label(_label) {}
    };

  void insert(const Entry& entry);
  void insert(const Formula& formula, const std::string& name, const IntegerSet& chargeStates);
  void insert_5pep(); // adds entries for the 5 peptide mix
  void insert_calmix(); // adds entries for the calmix
  void insert_db(std::vector<std::string>  peptideVector); // adds entries from a vector of peptides
  void insert_entryVector(std::vector<Entry> entryVect); // adds entries from vector
  void replace_entryVector(std::vector<Entry> entryVect); // replace entries with vector
  std::vector<Entry> splitEntries(double percentage);

    struct Match
    {
        const Entry* entry;
        const PeakFamily* peakFamily;

        double dmz;
        double dmz2;
        double dmzRel;
        double dmzRel2;
      
      
        Match()
        :   entry(0), peakFamily(0), dmz(0), dmz2(0), dmzRel(0), dmzRel2(0)
        {}
    };

    struct MatchResult
    {
      std::vector<Match> matches;
      
      int matchCount;
      double dmzMean;
      double dmz2Mean;
      double dmzRel2Mean;
      
      MatchResult()
        :   matchCount(0), dmzMean(0), dmz2Mean(0), dmzRel2Mean(0)
      {}
      void reComputeStatistics();
      std::vector<KnownMassList::Entry> getMatchedEntries();

    };


    // currently: epsilon == ppm
    MatchResult match(const Scan& scan, double epsilon) const;


    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
    KnownMassList(const KnownMassList& that);
    KnownMassList& operator=(const KnownMassList& that);
    friend std::ostream& operator<<(std::ostream& os, const KnownMassList::Entry& entry);
    friend std::ostream& operator<<(std::ostream& os, const KnownMassList::Match& match);
    friend std::ostream& operator<<(std::ostream& os, const KnownMassList::MatchResult& matchResult);
    friend std::ostream& operator<<(std::ostream& os, const KnownMassList& kml);
};


} // namespace pdanalysis 
} // namespace pwiz


#endif //  _KNOWNMASSLIST_HPP_

