//
// Pep2MzIdent.cpp
//

#define PWIZ_SOURCE

#include "Pep2MzIdent.hpp"
#include "pwiz/utility/proteome/Ion.hpp"

using namespace pwiz;
using namespace pwiz::mziddata;
using namespace pwiz::data::pepxml;
using namespace pwiz::proteome;

Pep2MzIdent::Pep2MzIdent(const MSMSPipelineAnalysis& mspa, MzIdentMLPtr result) : _mspa(mspa), _result(result) 
{
    // add one SpectrumIdentificationResultPtr for the pep xml spectrumQueries object
    _result->dataCollection.analysisData.spectrumIdentificationList.push_back(SpectrumIdentificationListPtr(new SpectrumIdentificationList()));
    (*_result->dataCollection.analysisData.spectrumIdentificationList.begin())->spectrumIdentificationResult.push_back(SpectrumIdentificationResultPtr(new SpectrumIdentificationResult()));


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

void addPeptide(const SpectrumQuery& sq, MzIdentMLPtr& x)
{
    x->sequenceCollection.peptides.push_back(PeptidePtr(new Peptide()));
    PeptidePtr& pp = x->sequenceCollection.peptides.back();
    pp->id = sq.searchResult.searchHit.peptide;
    pp->peptideSequence = sq.searchResult.searchHit.peptide;

    // TODO: Add modification info
}

SpectrumIdentificationItemPtr translateSpectrumQuery(const SpectrumQuery& sq)
{
    SpectrumIdentificationItemPtr result(new SpectrumIdentificationItem());    

    result->chargeState = sq.assumedCharge;
    result->experimentalMassToCharge = Ion::mz(sq.precursorNeutralMass, sq.assumedCharge);
    result->calculatedMassToCharge = Ion::mz(sq.searchResult.searchHit.calcNeutralPepMass, sq.assumedCharge);
    result->Peptide_ref = sq.searchResult.searchHit.peptide; 

    return result;
}

void Pep2MzIdent::translateSpectrumQueries()
{
    vector<SpectrumQuery>::iterator it = _mspa.msmsRunSummary.spectrumQueries.begin();
    for( ; it != _mspa.msmsRunSummary.spectrumQueries.end(); ++it) 
        {
            addPeptide(*it, _result);
            (*(*_result->dataCollection.analysisData.spectrumIdentificationList.begin())->spectrumIdentificationResult.begin())->spectrumIdentificationItem.push_back(translateSpectrumQuery(*it));

        }
    

}

