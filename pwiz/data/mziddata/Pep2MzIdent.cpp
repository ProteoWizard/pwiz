//
// $Id$
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#define PWIZ_SOURCE

#include "Pep2MzIdent.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "boost/lexical_cast.hpp"

using namespace pwiz;
using namespace pwiz::msdata;
using namespace pwiz::mziddata;
using namespace pwiz::data::pepxml;
using namespace pwiz::proteome;

using namespace boost;
using namespace std;

Pep2MzIdent::Pep2MzIdent(const MSMSPipelineAnalysis& mspa, MzIdentMLPtr result)
  : _mspa(mspa), _result(result),
    precursorMonoisotopic(false), fragmentMonoisotopic(false)
{
    translateRoot();
}

/// Translates pepXML data needed for the mzIdentML tag.
void Pep2MzIdent::translateRoot()
{
    _result->creationDate = _mspa.date;
    translateEnzyme(_mspa.msmsRunSummary.sampleEnzyme, _result);
    for (vector<SearchSummaryPtr>::const_iterator ss=_mspa.msmsRunSummary.searchSummary.begin();
         ss != _mspa.msmsRunSummary.searchSummary.end(); ss++)
    {
        translateSearch(*ss, _result);
    }

    for (vector<SpectrumQueryPtr>::const_iterator it=_mspa.msmsRunSummary.spectrumQueries.begin(); it!=_mspa.msmsRunSummary.spectrumQueries.end(); it++)
    {
        translateQueries(*it, _result);
    }
}

// Copies the data in the enzyme tag into the mzIdentML tree.
void Pep2MzIdent::translateEnzyme(const SampleEnzyme& sampleEnzyme, MzIdentMLPtr result)
{
    SpectrumIdentificationProtocolPtr sip(new SpectrumIdentificationProtocol());

    EnzymePtr enzyme(new Enzyme());

    // Cross fingers and pray that the name enzyme matches a cv name.
    // TODO create a more flexable conversion.
    enzyme->enzymeName.set(translator.translate(sampleEnzyme.name));
    enzyme->enzymeName.userParams.push_back(UserParam("description",
                                                sampleEnzyme.description));

    if (sampleEnzyme.fidelity == "Semispecific")
        enzyme->semiSpecific = true;
    else if (sampleEnzyme.fidelity == "Nonspecific")
        enzyme->semiSpecific = false;

    enzyme->minDistance = sampleEnzyme.specificity.minSpace;

    // TODO handle sense fields
    // first attempt at regex
    enzyme->siteRegexp = "[^"+sampleEnzyme.specificity.noCut+
        "]["+sampleEnzyme.specificity.cut+"]";
    
    sip->enzymes.enzymes.push_back(enzyme);

    result->analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sip);
}

void Pep2MzIdent::translateSearch(const SearchSummaryPtr summary, MzIdentMLPtr result)
{
    // push SourceFilePtr onto sourceFile
    // in Inputs in DataCollection
    SourceFilePtr sourceFile(new SourceFile());
    sourceFile->id = "SF_1";
    sourceFile->location = summary->baseName;
    sourceFile->fileFormat.set(MS_ISB_mzXML_file);

    result->dataCollection.inputs.sourceFile.push_back(sourceFile);

    AnalysisSoftwarePtr as(new AnalysisSoftware());
    result->analysisSoftwareList.push_back(as);
    result->analysisSoftwareList.back()->softwareName.set(
        translator.translate(summary->searchEngine));

    // handle precursorMassType/fragmentMassType
    precursorMonoisotopic = summary->precursorMassType == "monoisotopic";
    fragmentMonoisotopic = summary->fragmentMassType == "monoisotopic";
    
    SearchDatabasePtr searchDatabase(new SearchDatabase());
    searchDatabase->id = "SD_1";
    searchDatabase->location = summary->searchDatabase.localPath;
    searchDatabase->version = summary->searchDatabase.databaseReleaseIdentifier;
    searchDatabase->numDatabaseSequences = summary->searchDatabase.sizeInDbEntries;
    searchDatabase->numResidues = summary->searchDatabase.sizeOfResidues;

    // Another case of crossing fingers and translating
    searchDatabase->DatabaseName.set(translator.translate(summary->searchEngine));

    if (summary->searchDatabase.type == "AA")
        searchDatabase->params.set(MS_database_type_amino_acid);
    else if (summary->searchDatabase.type == "NA")
        searchDatabase->params.set(MS_database_type_nucleotide);

    
    // TODO figure out if this is correct
    searchDatabase->DatabaseName.set(translator.translate(summary->searchDatabase.databaseName));
    searchDatabase->fileFormat.set(translator.translate(summary->searchDatabase.type));

    for (vector<AminoAcidModification>::const_iterator it=
             summary->aminoAcidModifications.begin();
         it != summary->aminoAcidModifications.end(); it++)
    {
        ModificationPtr mod(new Modification());

        if(precursorMonoisotopic)
            mod->monoisotopicMassDelta = it->massDiff;
        else
            mod->avgMassDelta = it->massDiff;
        mod->residues = it->aminoAcid;

        // TODO save terminus somewhere
        
        // TODO should this be put somewhere?
        //mod->paramGroup.userParams.push_back(UserParam("mass", lexical_cast<string>(it->mass)));
    }
}


void Pep2MzIdent::addModifications(
    const vector<AminoAcidModification>& aminoAcidModifications,
    PeptidePtr peptide, MzIdentMLPtr result)
{
    typedef vector<AminoAcidModification>::const_iterator aam_iterator;
    
    for (aam_iterator it=aminoAcidModifications.begin();
         it != aminoAcidModifications.end(); it++)
    {
        if (find(peptide->peptideSequence.begin(),
                 peptide->peptideSequence.end(), it->aminoAcid.at(0)) ==
            peptide->peptideSequence.end())
            continue;
        
        ModificationPtr mod(new Modification());

        if(precursorMonoisotopic)
            mod->monoisotopicMassDelta = it->massDiff;
        else
            mod->avgMassDelta = it->massDiff;
        mod->residues = it->aminoAcid;

        // TODO save terminus somewhere
        if (it->peptideTerminus == "c")
        {
            mod->location = peptide->peptideSequence.size();
        }
        else if (it->peptideTerminus == "n")
        {
            mod->location = 0;
        }
        else if (it->peptideTerminus == "cn")
        {
            mod->location = 0;

            // TODO is this right?
            ModificationPtr mod2(new Modification());
            mod2 = mod;
            mod2->location = peptide->peptideSequence.size();
            peptide->modification.push_back(mod2);
        }
        
        peptide->modification.push_back(mod);
    }
}


void Pep2MzIdent::translateQueries(const SpectrumQueryPtr query,
                                   MzIdentMLPtr result)
{
    PeptideEvidencePtr pep(new PeptideEvidence());

    // TODO make sure handle the spectrum field
    pep->paramGroup.userParams.push_back(UserParam("spectrum", query->spectrum));
    
    pep->start = query->startScan;
    pep->end = query->endScan;
    
    SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem());

    // TODO find out if this is right.
    sii->chargeState = query->assumedCharge;
    
    sii->peptideEvidence.push_back(pep);

    // TODO handle precursorNeutralMass
    // TODO handle index/retentionTimeSec fields
    
    SpectrumIdentificationResultPtr sip(new SpectrumIdentificationResult());
    sip->id = "SIR_1";
    sip->spectrumID = query->spectrum;
    sip->spectrumIdentificationItem.push_back(sii);

    SpectrumIdentificationListPtr sil;
    if (sil->spectrumIdentificationResult.empty())
    {
        sil = SpectrumIdentificationListPtr(new SpectrumIdentificationList());
        result->analysisCollection.proteinDetection.
            inputSpectrumIdentifications.push_back(sil);
    }
    else
    {
        sil = result->analysisCollection.proteinDetection.
            inputSpectrumIdentifications.back();
    }

    sil->spectrumIdentificationResult.push_back(sip);
}


MzIdentMLPtr Pep2MzIdent::translate()
{
    if (_translated) return _result;
    else
        {
            translateMetadata();
            translateSpectrumQueries();
            _translated = true;

            return _result;
        }

}

void Pep2MzIdent::translateMetadata()
{

}

void addPeptide(const SpectrumQueryPtr sq, MzIdentMLPtr& x)
{
    for (vector<SearchResultPtr>::const_iterator sr=sq->searchResult.begin();
         sr != sq->searchResult.end(); sr++)
    {
        for (vector<SearchHitPtr>::const_iterator sh=(*sr)->searchHit.begin();
             sh != (*sr)->searchHit.end(); sh++)
        {
            PeptidePtr pp(new Peptide());
            pp->id = (*sh)->peptide;
            pp->peptideSequence = (*sh)->peptide;
            
            x->sequenceCollection.peptides.push_back(pp);
        }
    }
    // TODO: Add modification info
}

void translateSpectrumQuery(SpectrumIdentificationListPtr result,
                            const SpectrumQueryPtr sq)
{
    SpectrumIdentificationResultPtr sir(new SpectrumIdentificationResult());
    
    for (vector<SearchResultPtr>::const_iterator sr=sq->searchResult.begin();
         sr != sq->searchResult.end(); sr++)
    {
        for (vector<SearchHitPtr>::const_iterator sh=(*sr)->searchHit.begin();
             sh != (*sr)->searchHit.end(); sh++)
        {
            SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem());    
            PeptideEvidencePtr pepEv(new PeptideEvidence());
            
            sii->rank = (*sh)->hitRank;
            sii->peptidePtr = PeptidePtr(new Peptide((*sh)->peptide));
            pepEv->pre = (*sh)->peptidePrevAA;
            pepEv->post = (*sh)->peptideNextAA;
            sii->chargeState = sq->assumedCharge;
            sii->experimentalMassToCharge = Ion::mz(sq->precursorNeutralMass, sq->assumedCharge);
            sii->calculatedMassToCharge = Ion::mz((*sh)->calcNeutralPepMass, sq->assumedCharge);
            
            sir->spectrumIdentificationItem.push_back(sii);
        }
    }

    result->spectrumIdentificationResult.push_back(sir);
}

void Pep2MzIdent::translateSpectrumQueries()
{
    // NOTE: _result is type MzIdentMLPtr
    vector<SpectrumQueryPtr>::iterator it = _mspa.msmsRunSummary.spectrumQueries.begin();
    for( ; it != _mspa.msmsRunSummary.spectrumQueries.end(); ++it) 
    {
        addPeptide(*it, _result);
        
        SpectrumIdentificationListPtr sil(new SpectrumIdentificationList());
        translateSpectrumQuery(sil, *it);
        _result->dataCollection.analysisData.spectrumIdentificationList.push_back(sil);
    }
}

