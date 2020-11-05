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
namespace identdata {
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
        for (typename vector< shared_ptr<object_type> >::const_iterator it = referentList.begin();
            it != referentList.end(); ++it)
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

PWIZ_API_DECL void resolve(ContactRole& cr, IdentData& mzid)
{
    resolve(cr.contactPtr, mzid.auditCollection);
}


PWIZ_API_DECL void resolve(AnalysisSoftwarePtr& asp, IdentData& mzid)
{
    if (asp->contactRolePtr.get() && !asp->contactRolePtr->empty())
        resolve(*asp->contactRolePtr, mzid);
}


void resolve(Provider& provider, IdentData& mzid)
{   
    if (mzid.provider.contactRolePtr.get())
        resolve(*mzid.provider.contactRolePtr, mzid);
    if (mzid.provider.analysisSoftwarePtr.get())
        resolve(mzid.provider.analysisSoftwarePtr, mzid);
}


PWIZ_API_DECL void resolve(AnalysisSampleCollection& asc, IdentData& mzid)
{
    BOOST_FOREACH(SamplePtr& s, asc.samples)
    {
        BOOST_FOREACH(ContactRolePtr& cr, s->contactRole)
            resolve(*cr, mzid);
        BOOST_FOREACH(SamplePtr& ss, s->subSamples)
            if (ss.get() && !ss->empty())
                resolve(ss, asc.samples);
    }
}


PWIZ_API_DECL void resolve(OrganizationPtr& reference, vector<ContactPtr>& referentList)
{
    if (!reference.get() || reference->id.empty())
        return; 

    vector<ContactPtr>::iterator it = 
        find_if(referentList.begin(), referentList.end(), HasID<Contact>(reference->id));

    if (it == referentList.end())
    {
        ostringstream oss;
        oss << "[References::resolve()] Failed to resolve reference.\n"
            << "  object type: OrganizationPtr" << endl
            << "  reference id: " << reference->id << endl
            << "  referent list: " << referentList.size() << endl;
        for (vector<ContactPtr>::const_iterator it=referentList.begin(); it!=referentList.end(); ++it)
            oss << "    " << (*it)->id << endl;
        throw runtime_error(oss.str().c_str());
    }

    reference = boost::static_pointer_cast<Organization>(*it);
}


PWIZ_API_DECL void resolve(vector<ContactPtr>& vcp, IdentData& mzid)
{
    BOOST_FOREACH(ContactPtr& c, vcp)
    {
        if (dynamic_cast<Organization*>(c.get()))
            resolve(static_cast<Organization*>(c.get())->parent, mzid.auditCollection);
        else if (dynamic_cast<Person*>(c.get()))
            BOOST_FOREACH(OrganizationPtr& org, static_cast<Person*>(c.get())->affiliations)
                if (org.get() && !org->empty())
                    resolve(org, vcp);
    }
}

PWIZ_API_DECL void resolve(MassTablePtr& mt, const vector<SpectrumIdentificationProtocolPtr>& spectrumIdProts)
{
    if (!mt.get() || mt->id.empty())
        return; 

    BOOST_FOREACH(const SpectrumIdentificationProtocolPtr& sip, spectrumIdProts)
    BOOST_FOREACH(const MassTablePtr& mt2, sip->massTable)
    {
        if (mt == mt2)
            return;
        else if (mt->id == mt2->id)
        {
            mt = mt2;
            return;
        }
    }

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

PWIZ_API_DECL void resolve(PeptideEvidencePtr& pe, const IdentData& mzid)
{
    if (!pe.get())
        throw runtime_error("NULL value passed into resolve(PeptideEvidencePtr, IdentData&)");

    if (!pe->peptidePtr || pe->peptidePtr->peptideSequence.empty())
        resolve(pe->peptidePtr, mzid.sequenceCollection.peptides);

    if (!pe->dbSequencePtr || (pe->dbSequencePtr->seq.empty() && pe->dbSequencePtr->length == 0))
        resolve(pe->dbSequencePtr, mzid.sequenceCollection.dbSequences);

    // TODO construct a collection of TranslationTable's from all the
    // SpectrumIdentificationProtocolPtr's in AnalysisProtocolCollection.
    
    //if (pe->translationTablePtr.get())
    //    resolve(pe->translationTablePtr, mzid.);
    
}


//template <IdentData& mzid>
struct ResolvePE
{
    const IdentData* mzid_;
    ResolvePE(const IdentData* mzid) : mzid_(mzid) {}
    
    void operator()(PeptideEvidencePtr& pe)
    {
        return resolve(pe, (*mzid_));
    }
};

PWIZ_API_DECL void resolve(SpectrumIdentificationListPtr& sil, IdentData& mzid)
{
    BOOST_FOREACH(SpectrumIdentificationResultPtr& sir, sil->spectrumIdentificationResult)
    {
        if (sir->spectraDataPtr.get())
            resolve(sir->spectraDataPtr, mzid.dataCollection.inputs.spectraData);

        BOOST_FOREACH(SpectrumIdentificationItemPtr& sii, sir->spectrumIdentificationItem)
        {
            resolve(sii->massTablePtr, mzid.analysisProtocolCollection.spectrumIdentificationProtocol);
            resolve(sii->samplePtr, mzid.analysisSampleCollection.samples);

            BOOST_FOREACH(IonTypePtr& it, sii->fragmentation)
            BOOST_FOREACH(FragmentArrayPtr& fa, it->fragmentArray)
                resolve(fa->measurePtr, sil->fragmentationTable);

            if (!mzid.sequenceCollection.empty() &&
                sii->peptidePtr.get() &&
                sii->peptidePtr->peptideSequence.empty())
            {
                resolve(sii->peptidePtr, mzid.sequenceCollection.peptides);
            }

            ResolvePE rpe(&mzid);
            for_each(sii->peptideEvidencePtr.begin(),
                     sii->peptideEvidencePtr.end(),
                     rpe);
        }
    }
}

PWIZ_API_DECL void resolve(SequenceCollection& sc, IdentData& mzid)
{
    BOOST_FOREACH(DBSequencePtr& dbs, sc.dbSequences)
        resolve(dbs->searchDatabasePtr, mzid.dataCollection.inputs.searchDatabase);
    
    // look for unresolved PeptidePtr and DBSequencePtr in PeptideEvidences

    bool needPeptideResolved = false, needDbSeqResolved = false;
    for (const auto& pe : sc.peptideEvidence)
    {
        if (!pe) throw runtime_error("NULL PeptideEvidencePtr");
        if (!pe->dbSequencePtr) throw runtime_error("NULL dbSequencePtr for PeptideEvidence (no id to resolve)");
        if (!pe->peptidePtr) throw runtime_error("NULL peptidePtr for PeptideEvidence (no id to resolve)");

        if (pe->dbSequencePtr->seq.empty() && pe->dbSequencePtr->length == 0)
            needDbSeqResolved = true;
        if (pe->peptidePtr->peptideSequence.empty())
            needPeptideResolved = true;

        if (needDbSeqResolved && needPeptideResolved)
            break;
    }

    map<string, const DBSequencePtr*> dbSeqById;
    if (needDbSeqResolved)
        for (const auto& s : sc.dbSequences)
            if (s)
                dbSeqById[s->id] = &s;

    map<string, const PeptidePtr*> peptideById;
    if (needPeptideResolved)
        for (const auto& p : sc.peptides)
            if (p)
                peptideById[p->id] = &p;

    for (auto& pe : sc.peptideEvidence)
    {
        // null check still needed in case loop above broke early
        if (!pe) throw runtime_error("NULL PeptideEvidencePtr");
        if (!pe->dbSequencePtr) throw runtime_error("NULL dbSequencePtr for PeptideEvidence (no id to resolve)");
        if (!pe->peptidePtr) throw runtime_error("NULL peptidePtr for PeptideEvidence (no id to resolve)");

        if (needDbSeqResolved && pe->dbSequencePtr->seq.empty() && pe->dbSequencePtr->length == 0)
        {
            auto findItr = dbSeqById.find(pe->dbSequencePtr->id);
            if (findItr == dbSeqById.end())
                throw runtime_error("dBSequence_ref for PeptideEvidence " + pe->id + " does not resolve to a DBSequence element");
            pe->dbSequencePtr = *findItr->second;
        }

        if (needPeptideResolved && pe->peptidePtr->peptideSequence.empty())
        {
            auto findItr = peptideById.find(pe->peptidePtr->id);
            if (findItr == peptideById.end())
                throw runtime_error("peptide_ref for PeptideEvidence " + pe->id + " does not resolve to a Peptide element");
            pe->peptidePtr = *findItr->second;
        }
    }

    // Create a single Enzyme referent list
    vector<EnzymePtr> enzymePtrs;
    BOOST_FOREACH(const SpectrumIdentificationProtocolPtr& sip, mzid.analysisProtocolCollection.spectrumIdentificationProtocol)
        enzymePtrs.insert(enzymePtrs.end(), sip->enzymes.enzymes.begin(), sip->enzymes.enzymes.end());
}

PWIZ_API_DECL void resolve(SpectrumIdentification& si, IdentData& mzid)
{
    if (si.spectrumIdentificationProtocolPtr.get())
        resolve(si.spectrumIdentificationProtocolPtr,
                mzid.analysisProtocolCollection.spectrumIdentificationProtocol);
    
    if (si.spectrumIdentificationListPtr.get() &&
        !mzid.dataCollection.analysisData.spectrumIdentificationList.empty())
        resolve(si.spectrumIdentificationListPtr,
                mzid.dataCollection.analysisData.spectrumIdentificationList);

    resolve(si.inputSpectra, mzid.dataCollection.inputs.spectraData);
    resolve(si.searchDatabase, mzid.dataCollection.inputs.searchDatabase);
}


PWIZ_API_DECL void resolve(AnalysisCollection& ac, IdentData& mzid)
{
    for (vector<SpectrumIdentificationPtr>::iterator it=ac.spectrumIdentification.begin();
         it != ac.spectrumIdentification.end(); it++)
        resolve(**it, mzid);

    // TODO resolve proteinDetectionProtocolPtr & proteinDetectionListPtr;
    resolve(ac.proteinDetection.proteinDetectionProtocolPtr,
            mzid.analysisProtocolCollection.proteinDetectionProtocol);

    if (ac.proteinDetection.proteinDetectionListPtr.get() &&
        mzid.dataCollection.analysisData.proteinDetectionListPtr.get())
    {
        if (ac.proteinDetection.proteinDetectionListPtr->id ==
            mzid.dataCollection.analysisData.proteinDetectionListPtr->id)
        {
            ac.proteinDetection.proteinDetectionListPtr =
                mzid.dataCollection.analysisData.proteinDetectionListPtr;
        }
        else 
            throw runtime_error("[References::resolve] Unresolved ProteinDetectionList");
    }

    if (!mzid.dataCollection.analysisData.spectrumIdentificationList.empty())
        resolve(ac.proteinDetection.inputSpectrumIdentifications,
                mzid.dataCollection.analysisData.spectrumIdentificationList);
}


PWIZ_API_DECL void resolve(vector<SpectrumIdentificationProtocolPtr>& vsip, IdentData& mzid)
{
    for (vector<SpectrumIdentificationProtocolPtr>::iterator it=vsip.begin();
         it!=vsip.end(); it++)
    {
        if (it->get())
            resolve((*it)->analysisSoftwarePtr, mzid.analysisSoftwareList);
    }
}


PWIZ_API_DECL void resolve(vector<ProteinDetectionProtocolPtr>& vpdp, IdentData& mzid)
{
    for (vector<ProteinDetectionProtocolPtr>::iterator it=vpdp.begin();
         it!=vpdp.end(); it++)
    {
        if (it->get())
            resolve((*it)->analysisSoftwarePtr, mzid.analysisSoftwareList);
    }    
}


PWIZ_API_DECL void resolve(DataCollection& dc, IdentData& mzid)
{
    BOOST_FOREACH(SpectrumIdentificationListPtr& sil, dc.analysisData.spectrumIdentificationList)
        resolve(sil, mzid);

    // If there's no proteinDetectionListPtr, then we're done.
    if (!dc.analysisData.proteinDetectionListPtr.get())
        return;

    // If SequenceCollection wasn't populated, then we're done.
    if (mzid.sequenceCollection.empty())
        return;

    ProteinDetectionListPtr pdl=dc.analysisData.proteinDetectionListPtr;

    BOOST_FOREACH(ProteinAmbiguityGroupPtr& pag, pdl->proteinAmbiguityGroup)
    {
        BOOST_FOREACH(ProteinDetectionHypothesisPtr& pdh, pag->proteinDetectionHypothesis)
        {
            resolve(pdh->dbSequencePtr, mzid.sequenceCollection.dbSequences);

            BOOST_FOREACH(PeptideHypothesis& ph, pdh->peptideHypothesis)
            {
                if (ph.peptideEvidencePtr && ph.peptideEvidencePtr->peptidePtr)
                    continue;
                resolve(ph.peptideEvidencePtr, mzid.sequenceCollection.peptideEvidence);

                //BOOST_FOREACH(SpectrumIdentificationItemPtr& sii, ph.spectrumIdentificationItemPtr)
                //    resolve(sii, mzid.analysisCollection.proteinDetection.inputSpectrumIdentifications);
            }
        }
    }
}


PWIZ_API_DECL void resolve(IdentData& mzid)
{
    BOOST_FOREACH(AnalysisSoftwarePtr& as, mzid.analysisSoftwareList)
        if (as->contactRolePtr.get())
            resolve(*as->contactRolePtr, mzid);

    resolve(mzid.provider, mzid);
    resolve(mzid.auditCollection, mzid);
    resolve(mzid.analysisSampleCollection, mzid);
    
    resolve(mzid.sequenceCollection, mzid);
    resolve(mzid.analysisCollection, mzid);
    resolve(mzid.analysisProtocolCollection.spectrumIdentificationProtocol, mzid);
    resolve(mzid.analysisProtocolCollection.proteinDetectionProtocol, mzid);
    resolve(mzid.dataCollection, mzid);
}


} // namespace References 
} // namespace identdata
} // namespace pwiz 
