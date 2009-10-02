//
// $Id$
//

#define PWIZ_SOURCE

#include "Pep2MzIdent.hpp"
#include "pwiz/utility/proteome/Ion.hpp"

using namespace pwiz;
using namespace pwiz::msdata;
using namespace pwiz::mziddata;
using namespace pwiz::data::pepxml;
using namespace pwiz::proteome;

using namespace std;

Pep2MzIdent::Pep2MzIdent(const MSMSPipelineAnalysis& mspa, MzIdentMLPtr result) : _mspa(mspa), _result(result) 
{
    translateRoot();
    // add one SpectrumIdentificationResultPtr for the pep xml spectrumQueries object
    //_result->dataCollection.analysisData.spectrumIdentificationList.push_back(SpectrumIdentificationListPtr(new SpectrumIdentificationList()));
    //(*_result->dataCollection.analysisData.spectrumIdentificationList.begin())->spectrumIdentificationResult.push_back(SpectrumIdentificationResultPtr(new SpectrumIdentificationResult()));


}

void Pep2MzIdent::translateRoot()
{
    _result->creationDate = _mspa.date;
    translateEnzyme(_mspa.msmsRunSummary.sampleEnzyme, _result);
    translateSearch(_mspa.msmsRunSummary.searchSummary, _result);
}

void Pep2MzIdent::translateEnzyme(const SampleEnzyme& sampleEnzyme, MzIdentMLPtr result)
{
    SpectrumIdentificationProtocolPtr sip(new SpectrumIdentificationProtocol());

    EnzymePtr enzyme(new Enzyme());

    // Cross fingers and pray that the name enzyme matches a cv name.
    // TODO create a more flexable conversion.
    enzyme->enzymeName.set(translator.translate(sampleEnzyme.name));
    //if (sampleEnzyme.
    //enzyme->semiSpecific
    sip->enzymes.enzymes.push_back(enzyme);

    result->analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sip);
}

void Pep2MzIdent::translateSearch(const vector<SearchSummaryPtr>& sampleEnzyme, MzIdentMLPtr result)
{
    
}


void Pep2MzIdent::translateQueries(const std::vector<SpectrumQuery>& queries,
                                   MzIdentMLPtr result)
{
    for (vector<SpectrumQuery>::const_iterator it=queries.begin(); it!=queries.end(); it++)
    {
        SpectrumIdentificationResultPtr sip(new SpectrumIdentificationResult());
        SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem());
        
        
    }
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

