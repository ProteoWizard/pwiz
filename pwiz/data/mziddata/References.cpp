//
// $Id$
//
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

#include "References.hpp"
#include "TextWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace mziddata {
namespace References {



template <typename object_type>
struct HasID
{
    const string& id_;
    HasID(const string& id) : id_(id) {}

    bool operator()(const shared_ptr<object_type>& objectPtr)
    {
        return objectPtr.get() && objectPtr->id == id_;
    }
};

struct HasMTID
{
    const string& id_;
    HasMTID(const string& id) : id_(id) {}

    bool operator()(const SpectrumIdentificationProtocolPtr& sip)
    {
        return sip.get() && sip->massTable.id == id_;
    }
};


template <typename object_type>
void resolve(shared_ptr<object_type>& reference, 
             const vector< shared_ptr<object_type> >& referentList)
{
    if (!reference.get() || reference->id.empty())
        return; 

    typename vector< shared_ptr<object_type> >::const_iterator it = 
        find_if(referentList.begin(), referentList.end(), HasID<object_type>(reference->id));

    if (it == referentList.end())
    {
        ostringstream oss;
        oss << "[References::resolve()] Failed to resolve reference.\n"
            << "  object type: " << typeid(object_type).name() << endl
            << "  reference id: " << reference->id << endl
            << "  referent list: " << referentList.size() << endl;
        for (typename vector< shared_ptr<object_type> >::const_iterator it=referentList.begin();
             it!=referentList.end(); ++it)
            oss << "    " << (*it)->id << endl;
        throw runtime_error(oss.str().c_str());
    }

    reference = *it;
}


template <typename object_type>
void resolve(vector < shared_ptr<object_type> >& references,
             const vector< shared_ptr<object_type> >& referentList)
{
    for (typename vector< shared_ptr<object_type> >::iterator it=references.begin();
         it!=references.end(); ++it)
        resolve(*it, referentList);
}

PWIZ_API_DECL void resolve(ContactRole& cr, MzIdentML& mzid)
{
    resolve(cr.contactPtr, mzid.auditCollection);
}


PWIZ_API_DECL void resolve(AnalysisSoftwarePtr asp, MzIdentML& mzid)
{
    if (asp->contactRolePtr.get() && !asp->contactRolePtr->empty())
        resolve(*asp->contactRolePtr, mzid);
}


PWIZ_API_DECL void resolve(AnalysisSampleCollection& asc, MzIdentML& mzid)
{
    typedef vector<SamplePtr>::iterator Sit;
    typedef vector<Sample::subSample>::iterator SubSit;

    for (Sit sit = asc.samples.begin(); sit != asc.samples.end(); sit++)
    {
        for (SubSit ssit = (*sit)->subSamples.begin();
             ssit != (*sit)->subSamples.end(); ssit++)
        {
            if (!ssit->empty())
            {
                resolve((*ssit).samplePtr, asc.samples);
            }
        }
    }
}

PWIZ_API_DECL void resolve(vector<Affiliations>& vaff, vector<ContactPtr>& vcp)
{
    for (vector<Affiliations>::iterator it=vaff.begin(); it!=vaff.end(); it++)
    {
        if (it->organizationPtr.get() && !it->organizationPtr->empty())
            resolve(it->organizationPtr, vcp);
    }
}


PWIZ_API_DECL void resolve(vector<ContactPtr>& vcp, MzIdentML& mzid)
{
    for (vector<ContactPtr>::iterator it=vcp.begin(); it!=vcp.end(); it++)
    {
        if (dynamic_cast<Organization*>(it->get()))
            resolve(((Organization*)it->get())->parent.organizationPtr,
                     mzid.auditCollection);
        else if (dynamic_cast<Person*>((it->get())))
            resolve(((Person*)it->get())->affiliations, 
                     mzid.auditCollection);
    }
}


PWIZ_API_DECL void resolve(SequenceCollection& sc, MzIdentML& mzid)
{
    for (vector<DBSequencePtr>::iterator it=sc.dbSequences.begin();
         it!=sc.dbSequences.end(); it++)
    {
        resolve((*it)->searchDatabasePtr, mzid.dataCollection.inputs.searchDatabase);
    }
}

PWIZ_API_DECL void resolve(MassTablePtr& mt, const vector<SpectrumIdentificationProtocolPtr>& spectrumIdProts)
{
    if (!mt.get() || mt->id.empty())
        return; 

    vector<SpectrumIdentificationProtocolPtr>::const_iterator it = 
        find_if(spectrumIdProts.begin(), spectrumIdProts.end(), HasMTID(mt->id));

    if (it == spectrumIdProts.end())
    {
        ostringstream oss;
        oss << "[References::resolve()] Failed to resolve reference.\n"
            << "  object type: MassTable" << endl
            << "  reference id: " << mt->id << endl
            << "  referent list: " << spectrumIdProts.size() << endl;
        for (vector<SpectrumIdentificationProtocolPtr>::const_iterator it=spectrumIdProts.begin();
             it!=spectrumIdProts.end(); ++it)
            oss << "    " << (*it)->id << endl;
        throw runtime_error(oss.str().c_str());
    }

    mt = MassTablePtr(new MassTable((*it)->massTable));
}

PWIZ_API_DECL void resolve(PeptideEvidencePtr pe, const MzIdentML& mzid)
{
    if (!pe.get())
        throw runtime_error("NULL value passed into resolve(PeptideEvidencePtr,"
            "MzIdentML&)");

    if (pe->dbSequencePtr.get())
        resolve(pe->dbSequencePtr, mzid.sequenceCollection.dbSequences);

    // TODO construct a collection of TranslationTable's from all the
    // SpectrumIdentificationProtocolPtr's in AnalysisProtocolCollection.
    
    //if (pe->translationTablePtr.get())
    //    resolve(pe->translationTablePtr, mzid.);
    
}


//template <MzIdentML& mzid>
struct ResolvePE
{
    const MzIdentML* mzid_;
    ResolvePE(const MzIdentML* mzid) : mzid_(mzid) {}
    
    void operator()(PeptideEvidencePtr pe)
    {
        return resolve(pe, (*mzid_));
    }
};

PWIZ_API_DECL void resolve(SpectrumIdentificationListPtr si, MzIdentML& mzid)
{
    typedef vector<SpectrumIdentificationResultPtr>::iterator rit_iterator;
    typedef vector<SpectrumIdentificationItemPtr>::iterator iit_iterator;
    typedef vector<PeptideEvidencePtr>::iterator pe_iterator;
    
    for (rit_iterator rit=si->spectrumIdentificationResult.begin(); rit != si->spectrumIdentificationResult.end(); rit++)
    {
        if ((*rit)->spectraDataPtr.get())
            resolve((*rit)->spectraDataPtr, mzid.dataCollection.inputs.spectraData);
        
        for (iit_iterator iit=(*rit)->spectrumIdentificationItem.begin(); iit!=(*rit)->spectrumIdentificationItem.end(); iit++)
        {
            if ((*iit)->peptidePtr.get())
            {
                ResolvePE rpe(&mzid);
                for_each((*iit)->peptideEvidence.begin(),
                         (*iit)->peptideEvidence.end(),
                         rpe);
                resolve((*iit)->peptidePtr, mzid.sequenceCollection.peptides);
                resolve((*iit)->massTablePtr, mzid.analysisProtocolCollection.
                        spectrumIdentificationProtocol);
                resolve((*iit)->samplePtr, mzid.analysisSampleCollection.samples);
            }
        }
    }
}

PWIZ_API_DECL void resolve(SpectrumIdentification& si, MzIdentML& mzid)
{
    if (si.spectrumIdentificationProtocolPtr.get())
        resolve(si.spectrumIdentificationProtocolPtr,
                mzid.analysisProtocolCollection.spectrumIdentificationProtocol);
    
    if (si.spectrumIdentificationListPtr.get())
        resolve(si.spectrumIdentificationListPtr,
            mzid.dataCollection.analysisData.spectrumIdentificationList);
}


PWIZ_API_DECL void resolve(AnalysisCollection& ac, MzIdentML& mzid)
{
    for (vector<SpectrumIdentificationPtr>::iterator it=ac.spectrumIdentification.begin();
         it != ac.spectrumIdentification.end(); it++)
        resolve(**it, mzid);

    // TODO resolve proteinDetectionProtocolPtr & proteinDetectionListPtr;
    resolve(ac.proteinDetection.proteinDetectionProtocolPtr,
            mzid.analysisProtocolCollection.proteinDetectionProtocol);

    if (ac.proteinDetection.proteinDetectionListPtr.get())
    {
        if (mzid.dataCollection.analysisData.proteinDetectionListPtr.get() &&
            ac.proteinDetection.proteinDetectionListPtr->id ==
            mzid.dataCollection.analysisData.proteinDetectionListPtr->id)
        {
            ac.proteinDetection.proteinDetectionListPtr =
                mzid.dataCollection.analysisData.proteinDetectionListPtr;
        }
        else 
            throw runtime_error("[References::resolve] Unresolved ProteinDetectionList");
    }
    
    resolve(ac.proteinDetection.inputSpectrumIdentifications,
        mzid.dataCollection.analysisData.spectrumIdentificationList);
}


PWIZ_API_DECL void resolve(vector<SpectrumIdentificationProtocolPtr>& vsip, MzIdentML& mzid)
{
    for (vector<SpectrumIdentificationProtocolPtr>::iterator it=vsip.begin();
         it!=vsip.end(); it++)
    {
        if (it->get())
            resolve((*it)->analysisSoftwarePtr, mzid.analysisSoftwareList);
    }
}

PWIZ_API_DECL void resolve(vector<ProteinDetectionProtocolPtr>& vpdp, MzIdentML& mzid)
{
    for (vector<ProteinDetectionProtocolPtr>::iterator it=vpdp.begin();
         it!=vpdp.end(); it++)
    {
        if (it->get())
            resolve((*it)->analysisSoftwarePtr, mzid.analysisSoftwareList);
    }    
}


void fillPeptideEvidence(vector<SpectrumIdentificationListPtr>& sil, vector<PeptideEvidencePtr>& pe)
{
    typedef vector<SpectrumIdentificationListPtr>::const_iterator sil_iterator;
    typedef vector<SpectrumIdentificationResultPtr>::const_iterator sir_iterator;
    typedef vector<SpectrumIdentificationItemPtr>::const_iterator sii_iterator;
    
    for (sil_iterator sili=sil.begin(); sili != sil.end(); sili++)
    {
        vector<SpectrumIdentificationResultPtr>& sir = (*sili)->spectrumIdentificationResult;
        for(sir_iterator siri=sir.begin(); siri!=sir.end(); siri++)
        {
            vector<SpectrumIdentificationItemPtr>& sii=(*siri)->spectrumIdentificationItem;
            for(sii_iterator siii=sii.begin(); siii!=sii.end(); siii++)
            {
                for (vector<PeptideEvidencePtr>::const_iterator blah=
                         (*siii)->peptideEvidence.begin();
                     blah!=(*siii)->peptideEvidence.end(); blah++)
                    pe.push_back(*blah);
            }
        }
    }
}

PWIZ_API_DECL void resolve(DataCollection& dc, MzIdentML& mzid)
{
    vector<SpectrumIdentificationListPtr>& sil = dc.analysisData.spectrumIdentificationList;
    typedef vector<SpectrumIdentificationListPtr>::iterator sil_iterator;

    for (sil_iterator it=sil.begin(); it != sil.end(); it++)
    {
        resolve(*it, mzid);
    }

    // If there's no proteinDetectionListPtr, then we're done.
    if (!dc.analysisData.proteinDetectionListPtr.get())
        return;

    ProteinDetectionListPtr pdl=dc.analysisData.proteinDetectionListPtr;

    // Fill the vector of PeptideEvidencePtr's to make it easy to
    // resolve references
    vector<PeptideEvidencePtr> peptideEvidence;
    fillPeptideEvidence(
        mzid.analysisCollection.proteinDetection.inputSpectrumIdentifications,
        peptideEvidence);
    
    vector<ProteinAmbiguityGroupPtr>& pag = pdl->proteinAmbiguityGroup;
    typedef vector<ProteinAmbiguityGroupPtr>::iterator pag_iterator;
    
    for (pag_iterator pg=pag.begin(); pg != pag.end(); pg++)
    {
        vector<ProteinDetectionHypothesisPtr>& pdh = (*pg)->proteinDetectionHypothesis;
        typedef vector<ProteinDetectionHypothesisPtr>::iterator pdh_iterator;
        for (pdh_iterator pdhi=pdh.begin(); pdhi!=pdh.end(); pdhi++)
        {
            resolve((*pdhi)->dbSequencePtr,
                    mzid.sequenceCollection.dbSequences);

            vector<PeptideEvidencePtr>& pe = (*pdhi)->peptideHypothesis;
            typedef vector<PeptideEvidencePtr>::iterator pe_iterator;
            
            for(pe_iterator pei=pe.begin(); pei!=pe.end(); pei++)
            {
                resolve((*pei), peptideEvidence);
            }
        }
    }
}

PWIZ_API_DECL void resolve(MzIdentML& mzid)
{
    resolve(mzid.provider.contactRole, mzid);
    resolve(mzid.auditCollection, mzid);
    resolve(mzid.analysisSampleCollection, mzid);
    
    resolve(mzid.sequenceCollection, mzid);
    resolve(mzid.analysisCollection, mzid);
    resolve(mzid.analysisProtocolCollection.spectrumIdentificationProtocol,
            mzid);
    resolve(mzid.analysisProtocolCollection.proteinDetectionProtocol, mzid);
    resolve(mzid.dataCollection, mzid);
}


} // namespace References 
} // namespace mziddata
} // namespace pwiz 
