//
// TextWriter.hpp
//
//
// Original author: Robert Burke <robetr.burke@proteowizard.org>
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


#ifndef _MZIDDATA_TEXTWRITER_HPP_
#define _MZIDDATA_TEXTWRITER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MzIdentML.hpp"
#include "pwiz/data/msdata/CVParam.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <string>
#include <vector>


namespace pwiz {
namespace mziddata {

using msdata::CVParam;
using msdata::UserParam;

class PWIZ_API_DECL TextWriter
{
    public:

    TextWriter(std::ostream& os, int depth = 0)
    :   os_(os), depth_(depth), indent_(depth*2, ' ')
    {}

    TextWriter child() {return TextWriter(os_, depth_+1);}

    TextWriter& operator()(const std::string& text)
    {
        os_ << indent_ << text << std::endl;
        return *this;
    }

    TextWriter& operator()(const float value)
    {
        os_ << indent_ << value << std::endl;
        return *this;
    }

    TextWriter& operator()(const CVParam& cvParam)
    {
        os_ << indent_ << "cvParam: " << cvTermInfo(cvParam.cvid).name;
        if (!cvParam.value.empty())
            os_ << ", " << cvParam.value;
        if (cvParam.units != CVID_Unknown)
            os_ << ", " << cvParam.unitsName();
        os_ << std::endl; 
        return *this;    
    }

    TextWriter& operator()(const UserParam& userParam)
    {
        os_ << indent_ << "userParam: " << userParam.name;
        if (!userParam.value.empty()) os_ << ", " << userParam.value; 
        if (!userParam.type.empty()) os_ << ", " << userParam.type; 
        if (userParam.units != CVID_Unknown) os_ << ", " << cvTermInfo(userParam.units).name;
        os_ << std::endl; 
        return *this;    
    }

    template<typename object_type>
    TextWriter& operator()(const std::string& label, const std::vector<object_type>& v)
    {
        (*this)(label);
        for_each(v.begin(), v.end(), child());
        return *this;
    }

    template<typename object_type>
    TextWriter& operator()(const std::string& label, const object_type& v)
    {
        (*this)(label)(boost::lexical_cast<std::string>(v));
        return *this;
    }


    TextWriter& operator()(const std::string& label, const ParamContainer& paramContainer)
    {
        (*this)(label); // + ":"
        for_each(paramContainer.cvParams.begin(), paramContainer.cvParams.end(), *this);
        for_each(paramContainer.userParams.begin(), paramContainer.userParams.end(), *this);
        return *this;
    }
    
    TextWriter& operator()(const ParamContainer& paramContainer)
    {
        for_each(paramContainer.cvParams.begin(), paramContainer.cvParams.end(), *this);
        for_each(paramContainer.userParams.begin(), paramContainer.userParams.end(), *this);
        return *this;
    }


    TextWriter& operator()(const BibliographicReferencePtr& br)
    {
        if (!br.get())
            return *this;

        (*this)((IdentifiableType)*br);
        if (!br->authors.empty())
            child()("authors: "+br->authors);
        if (!br->publication.empty())
            child()("publication: "+br->publication);
        if (!br->publisher.empty())
            child()(br->publisher);
        if (!br->editor.empty())
            child()("editor: "+br->editor);
        if (br->year != 0)
            child()("year: "+boost::lexical_cast<std::string>(br->year));
        if (!br->volume.empty())
            child()("volume: "+br->volume);
        if (!br->issue.empty())
            child()("issue: "+br->issue);
        if (!br->pages.empty())
            child()("pages: "+br->pages);
        if (!br->title.empty())
            child()("title: "+br->title);

        return *this;
    }
    
    TextWriter& operator()(const std::vector<BibliographicReferencePtr>& br)
    {
        (*this)("BibliographicReference ");
        if (!br.empty())
            for_each(br.begin(), br.end(), *this);
        return *this;
    }

    
    TextWriter& operator()(const SpectrumIdentificationProtocolPtr& sip)
    {
        (*this)("SpectrumIdentificationProtocol:");
        (*this)((IdentifiableType)*sip);
        if (!sip->AnalysisSoftware_ref.empty())
            child()("AnalysisSoftware_ref: "+sip->AnalysisSoftware_ref);
        if (!sip->searchType.empty())
            child()("searchType", sip->searchType);
        if (!sip->additionalSearchParams.empty())
            child()("additionalSearchParams", sip->additionalSearchParams);
        if (!sip->modificationParams.empty())
        {
            child()("modificationParams");
            for_each(sip->modificationParams.begin(),
                     sip->modificationParams.end(), child());
        }
        if (!sip->enzymes.empty())
            child()(sip->enzymes);
        if (!sip->massTable.empty())
            child()(sip->massTable);
        if (!sip->fragmentTolerance.empty())
            child()("fragmentTolerance", sip->fragmentTolerance);
        if (!sip->parentTolerance.empty())
            child()("parentTolerance", sip->parentTolerance);
        if (!sip->threshold.empty())
            child()("threshold", sip->threshold);
        if (!sip->databaseFilters.empty())
            child()("databaseFilters", sip->databaseFilters);

        return *this;
    }

    
    TextWriter& operator()(const IonType& it)
    {
        (*this)("IonType: ");

        if (!it.index.empty())
            child()("index: ", it.index);
        if (it.charge != 0)
            child()("charge: "+boost::lexical_cast<std::string>(it.charge));
        
        return *this;
    }

    
    TextWriter& operator()(const Measure& m)
    {
        (*this)("Measure: ");

        if (!m.paramGroup.empty())
            child()(m.paramGroup);
        
        return *this;
    }

    
    TextWriter& operator()(const ModParam& mp)
    {
        (*this)("ModParam: ");
        if (mp.massDelta != 0)
            child()("massDelta: ", boost::lexical_cast<std::string>(mp.massDelta));
        if (!mp.residues.empty())
            child()("residues: ", mp.residues);
        if (!mp.cvParams.empty())
            child()("cvParams: ", mp.cvParams);
        return *this;
    }

    
    TextWriter& operator()(const SpectrumIdentificationList& sil)
    {
        (*this)("SpectrumIdentificationList: ");
        (*this)((IdentifiableType)sil);
        if (!sil.numSequencesSearched != 0)
            child()("numSequencesSearched: "+
                    boost::lexical_cast<std::string>(sil.numSequencesSearched));
        if (!sil.fragmentationTable.empty())
            child()("fragmentationTable", sil.fragmentationTable);
        if (!sil.spectrumIdentificationResult.empty())
            child()("spectrumIdentificationResult", sil.spectrumIdentificationResult);
        return *this;
    }

    TextWriter& operator()(const ProteinDetectionList& pdl)
    {
        (*this)("ProteinDetectionList: ");
        if (!pdl.proteinAmbiguityGroup.empty())
            child()("proteinAmbiguityGroup: ", pdl.proteinAmbiguityGroup);
        if (!pdl.paramGroup.empty())
            child()("paramGroup: ", pdl.paramGroup);
        return *this;
    }

    
    TextWriter& operator()(const AnalysisData& ad)
    {
        (*this)("analysisData: ");

        if (!ad.spectrumIdentificationList.empty())
            child()("spectrumIdentificationList: ", ad.spectrumIdentificationList);
        if (!ad.proteinDetectionList.empty())
            child()(ad.proteinDetectionList);
        
        return *this;
    }
    

    TextWriter& operator()(const FragmentArray& fa)
    {
        (*this)("FragmentArray: ");

        if (!fa.values.empty())
            child()("values: ", fa.values);
        if (!fa.Measure_ref.empty())
            child()("Measure_ref: " + fa.Measure_ref);
        return *this;
    }

    
    TextWriter& operator()(const SourceFile& sf)
    {
        //(*this)("sourceFile: ");

        (*this)((IdentifiableType)sf);
        if (!sf.location.empty())
            child()("location", sf.location);
        if (!sf.fileFormat.empty())
            child()(sf.fileFormat);
        if (!sf.externalFormatDocumentation.empty())
            child()("externalFormatDocumentation",
                    sf.externalFormatDocumentation);
        if (!sf.paramGroup.empty())
            child()(sf.paramGroup);
        
        return *this;
    }
    
    TextWriter& operator()(const Inputs& inputs)
    {
        (*this)("Inputs: ");

        if (!inputs.sourceFile.empty())
            child()("sourceFile: ", inputs.sourceFile);
        if (!inputs.searchDatabase.empty())
            child()("searchDatabase: ", inputs.searchDatabase);
        if (!inputs.spectraData.empty())
            child()("spectraData: ", inputs.spectraData);
        
        return *this;
    }

    
    TextWriter& operator()(const DataCollection& dc)
    {
        (*this)("DataCollection: ");

        if (!dc.inputs.empty())
            child()(dc.inputs);
        if (!dc.analysisData.empty())
            child()(dc.analysisData);
        
        return *this;
    }

    
    TextWriter& operator()(const Filter& f)
    {
        (*this)("Filter: ");
        if (!f.filterType.empty())
            child()("filterType: ", f.filterType);
        if (!f.include.empty())
            child()("include: ", f.include);
        if (!f.exclude.empty())
            child()("exclude: ", f.exclude);
        return *this;
    }

    
    TextWriter& operator()(const SearchModification& sm)
    {
        (*this)("SearchModification: ");
        if (sm.fixedMod != 0)
            child()("fixedMod: ", boost::lexical_cast<std::string>(sm.fixedMod));
        if (!sm.modParam.empty()){}
            //TODO          child()("modParam: ", sm.modParam);
        if (!sm.specificityRules.empty())
            child()("specificityRules: ", sm.specificityRules);       
        return *this;
    }

    TextWriter& operator()(const Enzymes& ezs)
    {
        (*this)("Enzyme: ");
        if (!ezs.independent.empty())
            child()("independent: ", ezs.independent);
        if (!ezs.enzymes.empty())
            child()("enzymes: ", ezs.enzymes);
        return *this;
    }

    TextWriter& operator()(const Enzyme& ez)
    {
        (*this)("Enzymes: ");
        if (!ez.id.empty())
            child()("id: ", ez.id);
        if (!ez.nTermGain.empty())
            child()("nTermGain: ", ez.nTermGain);
        if (!ez.cTermGain.empty())
            child()("cTermGain: ", ez.cTermGain);
        if (ez.semiSpecific != false)
            child()("semiSpecific: " , boost::lexical_cast<std::string>(ez.semiSpecific));
        if (!ez.missedCleavages.empty())
            child()("missedCleavages: ", ez.missedCleavages);
        if (!ez.minDistance.empty())
            child()("minDistance: ", ez.minDistance);
        if (!ez.siteRegexp.empty())
            child()("siteRegexp: ", ez.siteRegexp);
        if (!ez.enzymeName.empty())
            child()("enzymeName: ", ez.enzymeName);
        return *this;
    }

    
    TextWriter& operator()(const MassTable& mt)
    {
        (*this)("MassTable: ");
        if (!mt.id.empty())
            child()("id: ", mt.id);
        if (!mt.msLevel.empty())
            child()("msLevel: ", mt.msLevel);
        if (!mt.residues.empty())
            child()("residues: ", mt.residues);
        if (!mt.ambiguousResidue.empty())
            child()("ambiguousResidue: ", mt.residues);
        return *this;
    }

    
    TextWriter& operator()(const AnalysisProtocolCollection& apc)
    {
        (*this)("AnalysisProtocolCollection: ");
        if (!apc.spectrumIdentificationProtocol.empty())
            child()("spectrumIdentificationProtocol: ", apc.spectrumIdentificationProtocol);
        if (!apc.proteinDetectionProtocol.empty())
            child()("proteinDetectionProtocol: ", apc.proteinDetectionProtocol);
        return *this;
    }

    TextWriter& operator()(const ProteinDetectionHypothesis& pdh)
    {
        (*this)("ProteinDetectionHypothesis: ");
        if (!pdh.DBSequence_ref.empty())
            child()("DBSequence_ref: " + pdh.DBSequence_ref);
        // TODO: Resolve        if (!pdh.passThreshold.empty())
        //  child()("passThreshold: " + boost::lexical_cast<string>(pdh.passThreshold));
        if (!pdh.peptideHypothesis.empty())
            child()("peptideHypothesis: ", pdh.peptideHypothesis);
        if (!pdh.paramGroup.empty())
            child()("paramGroup: ", pdh.paramGroup);                    
        return *this;
    }

    TextWriter& operator()(const ProteinAmbiguityGroup& pag)
    {
        (*this)("ProteinAmbiguityGroup: ");
        if (!pag.proteinDetectionHypothesis.empty())
            child()("proteinDetectionHypothesis: ", pag.proteinDetectionHypothesis);
        if (!pag.paramGroup.empty())
            child()("paramGroup: ", pag.paramGroup);

        return *this;
    }

    TextWriter& operator()(const ProteinDetection& pd)
    {
        (*this)("ProteinDetection: ");
        if (!pd.ProteinDetectionProtocol_ref.empty())
            child()("ProteinDetectionProtocol_ref: ", pd.ProteinDetectionProtocol_ref);
        if (!pd.ProteinDetectionList_ref.empty())
            child()("ProteinDetectionList_ref: ", pd.ProteinDetectionList_ref);
        if (!pd.activityDate.empty())
            child()("activityDate: ", pd.activityDate);
        if (!pd.inputSpectrumIdentifications.empty())
            child()("inputSpectrumIdentifications: ", pd.inputSpectrumIdentifications);
        return *this;
    }
    
    TextWriter& operator()(const AnalysisCollection& ac)
    {
        (*this)("AnalysisCollection: ");
        if (!ac.spectrumIdentification.empty())
            child()("spectrumIdentification: ", ac.spectrumIdentification);
        if (!ac.proteinDetection.empty()){}
            //TODO           child()("proteinDetection: ", ac.proteinDetection);
        return *this;
    }

    
    TextWriter& operator()(const SequenceCollection& sc)
    {
        (*this)("SequenceCollection: ");
        if (!sc.dbSequences.empty())
            child()("dbSequences: ", sc.dbSequences);
        if (!sc.peptides.empty())
            child()("peptides: ", sc.peptides);
        return *this;
    }

    
    TextWriter& operator()(const Provider& provider)
    {
        (*this)("Provider: ");
        if (!provider.contactRole.empty())
            child()(provider.contactRole);
        return *this;
    }

    
    TextWriter& operator()(const Sample::subSample& subSample)
    {
        (*this)("component: "+subSample.Sample_ref);

        return *this;
    }

    
    TextWriter& operator()(const SamplePtr& sample)
    {
        (*this)("samples: ");
        for_each(sample->subSamples.begin(), sample->subSamples.end(), *this);

        return *this;
    }

    
    TextWriter& operator()(const AnalysisSampleCollection& asc)
    {
        for_each(asc.samples.begin(), asc.samples.end(), *this);

        return *this;
    }

    
    TextWriter& operator()(const ContactRole& cr)
    {
        (*this)("ContactRole: ");
        if (!cr.Contact_ref.empty())
            child()("Contact_ref: "+cr.Contact_ref);
        if (!cr.role.empty())
            child()("role: ", cr.role);
        return (*this);
    }

    
    TextWriter& operator()(const AnalysisSoftwarePtr& asp)
    {
        (*this)("analysisSoftwareList:");
        (*this)((IdentifiableType)*asp);
        if (asp->version.empty())
            child()("version: "+asp->version);
        if (asp->contactRole.empty())
            child()(asp->contactRole);
        if (asp->softwareName.empty())
            child()("softwareName: ", asp->softwareName);
        if (asp->URI.empty())
            child()("URI: "+asp->URI);
        if (asp->customizations.empty())
            child()("customizations: "+asp->customizations);
        //for_each(window.cvParams.begin(), window.cvParams.end(), child());
        return *this;
    }
    
    TextWriter& operator()(const IdentifiableType& id)
    {
        if (!id.id.empty())
            child()("id: "+id.id);
        if (!id.name.empty())
            child()("name: "+id.name);

        return *this;
    }
    
    TextWriter& operator()(const Residue& res)
    {
        (*this)("Residue: ");
        if (!res.Code.empty())
            child()("Code: ", res.Code);
        if (res.Mass != 0)
            child()("Mass: " + boost::lexical_cast<std::string>(res.Mass));
        return *this;
    }

    TextWriter& operator()(const Modification& mod)
    {
        (*this)("Modification: ");
        if (mod.location != 0)
            child()("location: " + boost::lexical_cast<std::string>(mod.location));
        if (!mod.residues.empty())
            child()("residues: " + mod.residues);
        if (mod.avgMassDelta != 0)
            child()("avgMassDelta: " + boost::lexical_cast<std::string>(mod.avgMassDelta));
        if (mod.monoisotopicMassDelta != 0)
            child()("monoisotopicMassDelta: ", boost::lexical_cast<std::string>(mod.monoisotopicMassDelta));
        if (!mod.paramGroup.empty())
            child()("paramGroup: ", mod.paramGroup);

        return *this;
    }

    TextWriter& operator()(const PeptideEvidence& pe)
    {
        (*this)("PeptideEvidence: ");
        if (pe.start != 0)
            child()("start: " + boost::lexical_cast<std::string>(pe.start));
        if (pe.end != 0)
            child()("end: " + boost::lexical_cast<std::string>(pe.end));
        if (!pe.pre.empty())
            child()("pre: " + pe.pre);
        if (!pe.post.empty())
            child()("post: " + pe.post);
        if (pe.frame != 0)
            child()("frame: " + boost::lexical_cast<std::string>(pe.frame));
        if (pe.missedCleavages != 0)
            child()("frame: " + boost::lexical_cast<std::string>(pe.missedCleavages));
        return *this;
    }

    TextWriter& operator()(const MzIdentML& mzid)
    {
        (*this)("mzid:");
        child()((IdentifiableType)mzid);
        if (!mzid.version.empty())
            child()("version: " + mzid.version);
        if (!mzid.cvs.empty())
            child()("cvList: ", mzid.cvs);
        if (!mzid.analysisSoftwareList.empty())
            child()("analysisSoftwareList: ", mzid.analysisSoftwareList);
        if (!mzid.provider.empty())
            child()(mzid.provider);
        if (!mzid.auditCollection.empty())
            child()("auditCollection: ", mzid.auditCollection);
        if (!mzid.analysisSampleCollection.empty())
            child()(mzid.analysisSampleCollection);
        if (!mzid.sequenceCollection.empty())
            child()(mzid.sequenceCollection);
        if (!mzid.analysisCollection.empty())
            child()(mzid.analysisCollection);
        if (!mzid.analysisProtocolCollection.empty())
            child()(mzid.analysisProtocolCollection);
        if (!mzid.dataCollection.empty())
            child()(mzid.dataCollection);
        if (!mzid.bibliographicReference.empty())
            child()(mzid.bibliographicReference);
        return *this;
    }
    
    TextWriter& operator()(const CV& cv)
    {
        (*this)("cv:");
        child()
            ("id: " + cv.id)
            ("fullName: " + cv.fullName)
            ("version: " + cv.version)
            ("URI: " + cv.URI);
        return *this;
    }

    // if no other overload matches, assume the object is a shared_ptr of a valid overloaded type
    template<typename object_type>
    TextWriter& operator()(const boost::shared_ptr<object_type>& p)
    {
        return p.get() ? (*this)(*p) : *this;
    }

    private:
    std::ostream& os_;
    int depth_;
    std::string indent_;
};

	
} // namespace mziddata
} // namespace pwiz


#endif // _MZIDDATA_TEXTWRITER_HPP_

