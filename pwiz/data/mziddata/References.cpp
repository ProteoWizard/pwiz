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
#include <stdexcept>


namespace pwiz {
namespace mziddata {
namespace References {


using namespace std;
using boost::shared_ptr;

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
}


} // namespace References 
} // namespace mziddata
} // namespace pwiz 
