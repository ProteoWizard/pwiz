//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// Exporter.cpp
///

#include "Exporter.hpp"
#include <algorithm>

using namespace pwiz;
using namespace eharmony;
using namespace pwiz::proteome;

void Exporter::writePM(ostream& os)
{
  XMLWriter pm_writer(os);
  PeptideMatchContainer sqs = _pm.getMatches();

  PeptideMatchContainer::iterator pm_it = sqs.begin();
  for(; pm_it != sqs.end(); ++pm_it)
    {
      pm_it->first->write(pm_writer);
      pm_it->second->write(pm_writer);

    }

}

void Exporter::writeWigglePlot(ostream& os)
{
  const PeptideMatchContainer& sqs = _pm.getMatches();

  PeptideMatchContainer::const_iterator it = sqs.begin();
  for(; it != sqs.end(); ++it) os << it->first->retentionTimeSec << "\t" << it->second->retentionTimeSec << "\n";


}

void Exporter::writeRTCalibrationPlot(ostream& os)
{
}

void Exporter::writeFunnyPeptides(ostream& os)
{
  const PeptideMatchContainer& sqs = _pm.getMatches();

  PeptideMatchContainer::const_iterator it = sqs.begin();
  size_t index = 0;
  for(; it != sqs.end(); ++it, ++index) if (index %5 != 0 && it->second->retentionTimeSec < 4000 && it->second->retentionTimeSec >= 3000 )/*(it->second->retentionTimeSec < 1000 && (it->first->retentionTimeSec - it->second->retentionTimeSec) > 50)*/ os << it->first->searchResult.searchHit.peptide << "\n";

}

void Exporter::writeOKPeptides(ostream& os)
{
  const PeptideMatchContainer& sqs = _pm.getMatches();
  
  PeptideMatchContainer::const_iterator it = sqs.begin();
  size_t index = 0;
  for(; it != sqs.end(); ++it, ++index) if ((index % 5 == 0 && it->second->retentionTimeSec < 4000) && it->second->retentionTimeSec >= 3000 )/*(it->second->retentionTimeSec < 1000 && (it->first->retentionTimeSec - it->second->retentionTimeSec) <= 50)*/ os << it->first->searchResult.searchHit.peptide << "\n";


}

void Exporter::writeF2PM(ostream& os)
{
  XMLWriter f2pm_writer(os);
  MatchData md(_f2pm.getMatches());
  md.write(f2pm_writer);

}

void Exporter::writeROCStats(ostream& os)
{
  MatchData fp(_f2pm.getFalsePositives());
  MatchData fn(_f2pm.getFalseNegatives());
  MatchData tp(_f2pm.getTruePositives());
  MatchData tn(_f2pm.getTrueNegatives());

  os << "All matches: " << _f2pm.getMatches().size() << endl;
  os << "truePositives: " << tp.matches.size() << endl;
  os << "falsePositives: " << fp.matches.size() << endl;
  os << "trueNegatives: " << tn.matches.size() << endl;
  os << "falseNegatives: " << fn.matches.size() << endl;
  os << "unknownPositives: " << _f2pm.getUnknownPositives().size() << endl;
  os << "unknownNegatives: " << _f2pm.getUnknownNegatives().size() << endl;

}
void Exporter::writePepXML(MSMSPipelineAnalysis& mspa, ostream& os) // mspa is the original pepXML. we are just changing the spectrumQueries attribute.
{
  vector<MatchPtr> matches = _f2pm.getMatches();
  vector<SpectrumQuery> hacked_sqs;

  vector<MatchPtr>::iterator it = matches.begin();
  for( ; it!= matches.end() ; ++it)
    {
      SpectrumQuery sq = (*it)->spectrumQuery;
      sq.searchResult.searchHit.analysisResult.peptideProphetResult.probability = (*it)->score;
      // TODO need to change n term probs?
      hacked_sqs.push_back(sq);

    }

  mspa.msmsRunSummary.spectrumQueries = hacked_sqs;

  XMLWriter writer(os);
  mspa.write(writer);

}

void Exporter::writeCombinedPepXML(MSMSPipelineAnalysis& mspa, ostream& os) // original ms2s and new ms1.5s
{
  vector<MatchPtr> matches = _f2pm.getMatches();
  vector<MatchPtr>::iterator it = matches.begin();
  for( ; it!=matches.end(); ++it)
    {
      SpectrumQuery sq = (*it)->spectrumQuery;
      sq.searchResult.searchHit.analysisResult.peptideProphetResult.probability = (*it)->score;
      mspa.msmsRunSummary.spectrumQueries.push_back(sq);

    }

  XMLWriter writer(os);
  mspa.write(writer);

}

void Exporter::writeRInputFile(ostream& os)
{
  vector<MatchPtr> matches = _f2pm.getMatches();
  vector<MatchPtr>::iterator it = matches.begin();
  for(; it != matches.end(); ++it)
    {
      double mzDiff = ((*it)->feature->mz - Ion::mz((*it)->spectrumQuery.precursorNeutralMass, (*it)->spectrumQuery.assumedCharge));
      double rtDiff = ((*it)->feature->retentionTime - (*it)->spectrumQuery.retentionTimeSec);
      os << mzDiff << "\t" << rtDiff << "\n";

    }

}

void Exporter::writeTruePositives(ostream& os)
{
  vector<MatchPtr> matches = _f2pm.getTruePositives();
  vector<MatchPtr>::iterator it = matches.begin();
  for(; it != matches.end(); ++it)
    {

        double mass = (*it)->spectrumQuery.precursorNeutralMass;
        double rt = (*it)->feature->retentionTime;

        double mzDiff = ((*it)->feature->mz - Ion::mz((*it)->spectrumQuery.precursorNeutralMass, (*it)->spectrumQuery.assumedCharge));
        double rtDiff = ((*it)->feature->retentionTime - (*it)->spectrumQuery.retentionTimeSec);

        os << mass << "\t" << rt << "\t" << mzDiff << "\t" << rtDiff << "\t" << (*it)->score <<  "\n";
            
    }

}

void Exporter::writeFalsePositives(ostream& os)
{
  vector<MatchPtr> matches = _f2pm.getFalsePositives();
  vector<MatchPtr>::iterator it = matches.begin();
  for(; it != matches.end(); ++it)
    {

        double mass = (*it)->spectrumQuery.precursorNeutralMass;
        double rt = (*it)->feature->retentionTime;

        double massDiff = mass - (*it)->calculatedMass;
        double rtDiff = ((*it)->feature->retentionTime - (*it)->spectrumQuery.retentionTimeSec);

        os << mass << "\t" << rt << "\t" << massDiff << "\t" << rtDiff << "\t" << (*it)->score <<"\n";
 
    }

}

void Exporter::writeTrueNegatives(ostream& os)
{
  vector<MatchPtr> matches = _f2pm.getTrueNegatives();
  vector<MatchPtr>::iterator it = matches.begin();
  for(; it != matches.end(); ++it)
    {

        double mass = (*it)->spectrumQuery.precursorNeutralMass;
        double rt = (*it)->feature->retentionTime;

        double massDiff = mass - (*it)->calculatedMass;
        double rtDiff = ((*it)->feature->retentionTime - (*it)->spectrumQuery.retentionTimeSec);

        os << mass << "\t" << rt << "\t" << massDiff << "\t" << rtDiff << "\t" << (*it)->score << "\n" ;        

    }

}

void Exporter::writeFalseNegatives(ostream& os)
{
  vector<MatchPtr> matches = _f2pm.getFalseNegatives();
  vector<MatchPtr>::iterator it = matches.begin();
  for(; it != matches.end(); ++it)
    {
        double mass = (*it)->spectrumQuery.precursorNeutralMass;
        double rt = (*it)->feature->retentionTime;

        double massDiff = mass - (*it)->calculatedMass;
        double rtDiff = ((*it)->feature->retentionTime - (*it)->spectrumQuery.retentionTimeSec);

        os << mass << "\t" << rt << "\t" << massDiff << "\t" << rtDiff << "\t" << (*it)->score << "\n";
     
    }

}

void Exporter::writeUnknownPositives(ostream& os)
{
    vector<MatchPtr> matches = _f2pm.getUnknownPositives();
    vector<MatchPtr>::iterator it = matches.begin();
    for(; it != matches.end(); ++it)
        {
            double mass = (*it)->spectrumQuery.precursorNeutralMass;
            double rt = (*it)->feature->retentionTime;

            double massDiff = mass - (*it)->calculatedMass;
            double rtDiff = ((*it)->feature->retentionTime - (*it)->spectrumQuery.retentionTimeSec);

            os << mass << "\t" << rt << "\t" << massDiff << "\t" << rtDiff << "\t" << (*it)->score << "\n";

        }

}

void Exporter::writeUnknownNegatives(ostream& os)
{
    vector<MatchPtr> matches = _f2pm.getUnknownNegatives();
    vector<MatchPtr>::iterator it = matches.begin();
    for(; it != matches.end(); ++it)
        {
            double mass = (*it)->spectrumQuery.precursorNeutralMass;
            double rt = (*it)->feature->retentionTime;

            double massDiff = mass - (*it)->calculatedMass;
            double rtDiff = ((*it)->feature->retentionTime - (*it)->spectrumQuery.retentionTimeSec);

            os << mass << "\t" << rt << "\t" << massDiff << "\t" << rtDiff << "\t" << (*it)->score << "\n";

        }

}


void Exporter::writeRTCalibrationData(ostream& ospep, ostream& osf0, ostream& osf1)
{
    return;
}

struct RTLess
{
  RTLess(){}
    bool operator()(const pair<boost::shared_ptr<SpectrumQuery>, boost::shared_ptr<SpectrumQuery> >& a, const pair<boost::shared_ptr<SpectrumQuery>, boost::shared_ptr<SpectrumQuery> >& b) { return a.first->retentionTimeSec < b.first->retentionTimeSec;}

};

void Exporter::writeAnchors(ostream& os)
{
  PeptideMatchContainer sqs = _pm.getMatches();  
  sort(sqs.begin(), sqs.end(), RTLess());

  PeptideMatchContainer::iterator it = sqs.begin();
  size_t index = 1;
  for(; it!= sqs.end(); ++it, ++index) if (index%30 == 0) os << it->first->retentionTimeSec << "\t" << it->second->retentionTimeSec << "\n";

}
