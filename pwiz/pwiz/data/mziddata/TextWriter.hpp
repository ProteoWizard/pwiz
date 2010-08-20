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


#ifndef _MZIDDATA_TEXTWRITER_HPP_
#define _MZIDDATA_TEXTWRITER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MzIdentML.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <string>
#include <vector>


namespace pwiz {
namespace mziddata {

using namespace pwiz::cv;
using namespace pwiz::data;
using namespace boost::logic;

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
        (*this)("BibliographicReference: ");
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
        if (sip.get())
            (*this)(*sip);

        return *this;
    }

    
    TextWriter& operator()(const SpectrumIdentificationProtocol& si)
    {
        (*this)("SpectrumIdentificationProtocol:");
        (*this)((IdentifiableType&)si);
        if (si.analysisSoftwarePtr.get() &&
            !si.analysisSoftwarePtr->empty())
            child()("AnalysisSoftware_ref: "+si.analysisSoftwarePtr->id);
        if (!si.searchType.empty())
            child()("searchType", si.searchType);
        if (!si.additionalSearchParams.empty())
            child()("additionalSearchParams", si.additionalSearchParams);
        if (!si.modificationParams.empty())
        {
            child()("modificationParams");
            for_each(si.modificationParams.begin(),
                     si.modificationParams.end(), child());
        }
        if (!si.enzymes.empty())
            child()(si.enzymes);
        if (!si.massTable.empty())
            child()(si.massTable);
        if (!si.fragmentTolerance.empty())
            child()("fragmentTolerance", si.fragmentTolerance);
        if (!si.parentTolerance.empty())
            child()("parentTolerance", si.parentTolerance);
        if (!si.threshold.empty())
            child()("threshold", si.threshold);
        if (!si.databaseFilters.empty())
            child()("databaseFilters", si.databaseFilters);

        return *this;
    }

    
    TextWriter& operator()(const DBSequence& ds)
    {
        (*this)("DBSequence: ");
        (*this)((const IdentifiableType&)ds);
        if (ds.length!=0)
            child()("length"+boost::lexical_cast<std::string>(ds.length));
        if (!ds.accession.empty())
            child()("accession: "+ds.accession);
        if (ds.searchDatabasePtr.get() && !ds.searchDatabasePtr->empty())
            child()("SearchDatabase_ref: "+ds.searchDatabasePtr->id);
        if (!ds.seq.empty())
            child()("seq: "+ds.seq);
        if (!ds.paramGroup.empty())
            child()("paramGroup", ds.paramGroup);

        return *this;
    }

    
    TextWriter& operator()(const SubstitutionModification& ds)
    {
        (*this)("SubstitutionModification: ");
        if (!ds.originalResidue.empty())
            child()("originalResidue: "+ds.originalResidue);
        if (!ds.replacementResidue.empty())
            child()("replacementResidue: "+ds.replacementResidue);
        if (ds.location != 0)
            child()("location: "+boost::lexical_cast<std::string>(ds.location));
        child()("avgMassDelta: "+
                boost::lexical_cast<std::string>(ds.avgMassDelta));
        child()("monoisotopicMassDelta: "+
                boost::lexical_cast<std::string>(ds.monoisotopicMassDelta));

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

    TextWriter& operator()(const SearchDatabase& sd)
    {
        (*this)("SearchDatabase: ");
        if (!sd.version.empty())
            child()("version: " + sd.version);
        if (!sd.releaseDate.empty())
            child()("releaseDate: " + sd.releaseDate);
        if (sd.numDatabaseSequences != 0)
            child()("numDatabaseSequences: " + boost::lexical_cast<std::string>(sd.numDatabaseSequences));
        if (sd.numResidues != 0)
            child()("numResidues: " + boost::lexical_cast<std::string>(sd.numResidues));
        if (!sd.fileFormat.empty())
            child()("fileFormat: ", sd.fileFormat);
        if (!sd.DatabaseName.empty())
            child()("DatabaseName: ", sd.DatabaseName);
        return *this;
    }
    
    TextWriter& operator()(const SpectraData& sd)
    {
        (*this)("SpectraData: ");
        if (!sd.location.empty())
            child()("location: " + sd.location);
        if (!sd.externalFormatDocumentation.empty())
            child()("externalFormatDocumentation: ", sd.externalFormatDocumentation);
        if (!sd.fileFormat.empty())
            child()("fileFormat: ", sd.fileFormat);
        return *this;
    }


    TextWriter& operator()(const SpectrumIdentificationResult& sir)
    {
        (*this)("SpectrumIdentificationResult: ");
        (*this)((IdentifiableType)sir);
        if (!sir.spectrumID.empty())
            child()("spectrumID: "+sir.spectrumID);
        if (sir.spectraDataPtr.get() && !sir.spectraDataPtr->empty())
            child()("SpectraData_ref: "+sir.spectraDataPtr->id);
        if (!sir.spectrumIdentificationItem.empty())
            child()("spectrumIdentificationItem", sir.spectrumIdentificationItem);
        if (!sir.paramGroup.empty())
            child()("paramGroup", sir.paramGroup);
        
        return *this;
    }

    
    TextWriter& operator()(const SpectrumIdentificationList& sil)
    {
        (*this)("SpectrumIdentificationList: ");
        (*this)((IdentifiableType)sil);
        if (!sil.empty())
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
        if (ad.proteinDetectionListPtr.get() &&
            !ad.proteinDetectionListPtr->empty())
            child()(*ad.proteinDetectionListPtr);
        
        return *this;
    }
    

    TextWriter& operator()(const FragmentArray& fa)
    {
        (*this)("FragmentArray: ");

        if (!fa.values.empty())
            child()("values: ", fa.values);
        if (fa.measurePtr.get() && !fa.measurePtr->empty())
            child()("Measure_ref: " + fa.measurePtr->id);
        if (!fa.params.empty())
            child()("params", fa.params);
        
        return *this;
    }

    
    TextWriter& operator()(const SourceFile& sf)
    {
        //(*this)("sourceFile: ");

        (*this)((IdentifiableType)sf);
        if (!sf.location.empty())
            child()("location: " + sf.location);
        if (!sf.fileFormat.empty())
            child()(sf.fileFormat);
        if (!sf.externalFormatDocumentation.empty())
            child()("externalFormatDocumentation: ",
                    sf.externalFormatDocumentation);
        if (!sf.paramGroup.empty())
            child()("paramGroup: ", sf.paramGroup);
        
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
        (*this)("Enzymes: ");
        if (!ezs.independent.empty())
            child()("independent: ", ezs.independent);
        if (!ezs.enzymes.empty())
            child()("enzymes: ", ezs.enzymes);
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
        if (pdh.dbSequencePtr.get() && !pdh.dbSequencePtr->empty())
            child()("DBSequence_ref: " + pdh.dbSequencePtr->id);
        // TODO: Resolve        if (!pdh.passThreshold.empty())
        //  child()("passThreshold: " + boost::lexical_cast<std::string>(pdh.passThreshold));
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
        if (pd.proteinDetectionProtocolPtr.get() &&
            !pd.proteinDetectionProtocolPtr->empty())
        {
            child()("ProteinDetectionProtocol_ref: ");
            child()(*pd.proteinDetectionProtocolPtr);
        }
        if (pd.proteinDetectionListPtr.get() &&
            !pd.proteinDetectionListPtr->empty())
        {
            child()("ProteinDetectionList_ref: ");
            child()(*pd.proteinDetectionListPtr);
        }
        if (!pd.activityDate.empty())
            child()("activityDate: "+pd.activityDate);
        child()("inputSpectrumIdentifications:");
        std::for_each(pd.inputSpectrumIdentifications.begin(),
                      pd.inputSpectrumIdentifications.end(), child());
        return *this;
    }
    

    TextWriter& operator()(const SpectrumIdentification& si)
    {
        (*this)("SpectrumIdentification: ");
        if (si.spectrumIdentificationProtocolPtr.get() &&
            !si.spectrumIdentificationProtocolPtr->empty())
            child()("SpectrumIdentificationProtocol_ref: "+si.spectrumIdentificationProtocolPtr->id);
        if (si.spectrumIdentificationListPtr.get() &&
            !si.spectrumIdentificationListPtr->empty())
            child()("SpectrumIdentificationList_ref: "+si.spectrumIdentificationListPtr->id);
        if (!si.activityDate.empty())
            child()("activityDate: "+si.activityDate);

        if (!si.inputSpectra.empty())
        {
            child()("inputSpectra: ");
            std::for_each(si.inputSpectra.begin(), si.inputSpectra.end(), child());
        }
        if (!si.searchDatabase.empty())
        {
            child()("searchDatabase: ");
            std::for_each(si.searchDatabase.begin(), si.searchDatabase.end(), child());
        }
        
        return *this;
    }

    
    TextWriter& operator()(const SpectrumIdentificationPtr si)
    {
        if (si.get())
            (*this)(*si);

        return *this;
    }
    

    TextWriter& operator()(const AnalysisCollection& ac)
    {
        (*this)("AnalysisCollection: ");
        std::for_each(ac.spectrumIdentification.begin(),
                      ac.spectrumIdentification.end(), child());
        if (!ac.proteinDetection.empty())
            child()(ac.proteinDetection);
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

    
    TextWriter& operator()(const Contact& cont)
    {
        (*this)((IdentifiableType&)cont);
        if (!cont.address.empty())
            child()("address: "+cont.address);
        if (!cont.phone.empty())
            child()("phone: "+cont.phone);
        if (!cont.email.empty())
            child()("email: "+cont.email);
        if (!cont.fax.empty())
            child()("fax: "+cont.fax);
        if (!cont.tollFreePhone.empty())
            child()("tollFreePhone: "+cont.tollFreePhone);

        return *this;
    }


    TextWriter& operator()(const Affiliations& aff)
    {
        (*this)("Affiliations: ");

        if (aff.organizationPtr.get())
            child()("organizationPtr: "+aff.organizationPtr->id);
        
        return *this;
    }

    TextWriter& operator()(const Person& per)
    {
        (*this)("Person: ");
        (*this)((const Contact&)per);
        if (!per.lastName.empty())
            child()("lastName: "+per.lastName);
        if (!per.firstName.empty())
            child()("firstName: "+per.firstName);
        if (!per.midInitials.empty())
            child()("midInitials: "+per.midInitials);

        std::for_each(per.affiliations.begin(), per.affiliations.end(), child());
        
        return *this;
    }
    
    TextWriter& operator()(const Organization& org)
    {
        (*this)("Organization: ");
        (*this)((const Contact&)org);
        if (org.parent.organizationPtr.get() && !org.parent.organizationPtr->empty())
        {
            child()("Parent::organizationPtr: ");
            child()(*org.parent.organizationPtr);
        }
        
        return *this;
    }

    
    TextWriter& operator()(const ContactPtr cont)
    {
        if (dynamic_cast<Person*>(cont.get()))
            (*this)((const Person&)(*cont));
        else if (dynamic_cast<Organization*>(cont.get()))
            (*this)((const Organization&)(*cont));
        else
            (*this)(*cont);

        return *this;
    }

    
    TextWriter& operator()(const std::string& label, const ContactPtr cont)
    {
        (*this)(label);
        (*this)(cont);

        return *this;
    }

    
    TextWriter& operator()(const ContactRole& cr)
    {
        (*this)("ContactRole: ");
        if (cr.contactPtr.get() && !cr.contactPtr->empty())
        {
            child()("Contact_ref: ", cr.contactPtr);
        }
        if (!cr.role.empty())
            child()("role: ", cr.role);
        return (*this);
    }

    
    TextWriter& operator()(const Provider& provider)
    {
        (*this)("Provider: ");
        (*this)((IdentifiableType&)provider);
        if (!provider.contactRole.empty())
            child()(provider.contactRole);
        return *this;
    }

    
    TextWriter& operator()(const Sample::subSample& subSample)
    {
        if (subSample.samplePtr.get())
            (*this)("Sample_ref: "+subSample.samplePtr->id);

        return *this;
    }

    
    TextWriter& operator()(const Material& material)
    {
        child()(material.contactRole);
        child()(material.cvParams);

        return *this;
    }

    
    TextWriter& operator()(const Sample& sample)
    {
        (*this)("samples: ");
        (*this)((const Material&)sample);
        child()("subSamples:");
        std::for_each(sample.subSamples.begin(), sample.subSamples.end(), child());

        return *this;
    }

    
    TextWriter& operator()(const SamplePtr& sample)
    {
        (*this)(*sample);

        return *this;
    }

    
    TextWriter& operator()(const AnalysisSampleCollection& asc)
    {
        (*this)("AnalysisSampleCollection: ");
        for_each(asc.samples.begin(), asc.samples.end(), *this);

        return *this;
    }

    
    TextWriter& operator()(const AnalysisSoftwarePtr& asp)
    {
        (*this)("analysisSoftwareList:");
        (*this)((IdentifiableType)*asp);
        if (asp->version.empty())
            child()("version: "+asp->version);
        if (asp->contactRolePtr.get() && asp->contactRolePtr->empty())
            child()(*asp->contactRolePtr);
        if (asp->softwareName.empty())
            child()("softwareName: ", asp->softwareName);
        if (asp->URI.empty())
            child()("URI: "+asp->URI);
        if (asp->customizations.empty())
            child()("customizations: "+asp->customizations);
        //for_each(window.cvParams.begin(), window.cvParams.end(), child());
        return *this;
    }
    

    TextWriter& operator()(const Enzyme& enzyme)
    {
        (*this)("Enzyme: ");
        if (!enzyme.id.empty())
            child()("id: "+enzyme.id);
        if (!enzyme.nTermGain.empty())
            child()("nTermGain: "+enzyme.nTermGain);
        if (!enzyme.cTermGain.empty())
            child()("cTermGain: "+enzyme.cTermGain);
        if (enzyme.semiSpecific != boost::logic::indeterminate)
            child()(std::string("semiSpecific: ")+ (enzyme.semiSpecific ? "true": "false"));
        if (enzyme.missedCleavages != 0)
            child()("missedCleavages: "+
                    boost::lexical_cast<std::string>(enzyme.missedCleavages));
        if (enzyme.minDistance != 0)
            child()("minDistance: "+boost::lexical_cast<std::string>(enzyme.minDistance));

        if (!enzyme.siteRegexp.empty())
            child()("siteRegexp: "+enzyme.siteRegexp);
        if (!enzyme.enzymeName.empty())
            child()("enzymeName: ", enzyme.enzymeName);

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

    
    TextWriter& operator()(const AmbiguousResidue& res)
    {
        (*this)("AmbiguousResidue: ");
        if (!res.Code.empty())
            child()("Code: ", res.Code);
        if (!res.params.empty())
            child()("params: ", res.params);
        
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


    TextWriter& operator()(const ModificationPtr modp)
    {
        if (!modp.get())
            return *this;
        
        return (*this)(*modp);
    }

    
    TextWriter& operator()(const Peptide& pep)
    {
        (*this)("Peptide: ");

        (*this)((IdentifiableType)pep);
        if (!pep.peptideSequence.empty())
            child()("peptideSequence: "+pep.peptideSequence);
        if (!pep.modification.empty())
            child()("modification", pep.modification);
        if (!pep.substitutionModification.empty())
            child()(pep.substitutionModification);
        if (!pep.paramGroup.empty())
            child()(pep.paramGroup);
        
        return *this;
    }

    
    TextWriter& operator()(const PeptideEvidence& pe)
    {
        (*this)("PeptideEvidence: ");
        if (pe.dbSequencePtr.get() && !pe.dbSequencePtr->empty())
            child()("DBSequence_ref: "+pe.dbSequencePtr->empty());
        if (pe.start != 0)
            child()("start: " + boost::lexical_cast<std::string>(pe.start));
        if (pe.end != 0)
            child()("end: " + boost::lexical_cast<std::string>(pe.end));
        if (!pe.pre.empty())
            child()("pre: " + pe.pre);
        if (!pe.post.empty())
            child()("post: " + pe.post);
        if (pe.translationTablePtr.get() && !pe.translationTablePtr->empty())
            child()("TranslationTable_ref: "+pe.translationTablePtr->id);
        if (pe.frame != 0)
            child()("frame: " + boost::lexical_cast<std::string>(pe.frame));
        child()(std::string("isDecoy: ")+(pe.isDecoy ? "true" : "false"));
        if (pe.missedCleavages != 0)
            child()("missedCleavages: "+
                    boost::lexical_cast<std::string>(pe.missedCleavages));

        if (!pe.paramGroup.empty())
            child()("paramGroup", pe.paramGroup);
        
        return *this;
    }

    TextWriter& operator()(const SpectrumIdentificationItem& sii)
    {
        (*this)("SpectrumIdentificationItem:");

        if (!sii.empty())
            child()("chargeState: "+boost::lexical_cast<std::string>(sii.chargeState));
        if (!sii.empty())
            child()("experimentalMassToCharge: "+boost::lexical_cast<std::string>(sii.experimentalMassToCharge));
        if (!sii.empty())
            child()("calculatedMassToCharge: "+boost::lexical_cast<std::string>(sii.calculatedMassToCharge));
        if (!sii.empty())
            child()("calculatedPI: "+boost::lexical_cast<std::string>(sii.calculatedPI));
        if (sii.peptidePtr.get() && !sii.peptidePtr->empty())
            child()("Peptide_ref: "+sii.peptidePtr->id);
        if (!sii.empty())
            child()("rank: "+boost::lexical_cast<std::string>(sii.rank));
        if (!sii.empty())
            child()("passThreshold: "+boost::lexical_cast<std::string>(sii.passThreshold));
        if (sii.massTablePtr.get() && !sii.massTablePtr->empty())
            child()("MassTable_ref: "+sii.massTablePtr->id);
        if (sii.samplePtr.get() && !sii.samplePtr->empty())
            child()("Sample_ref: "+sii.samplePtr->id);

    
        if (!sii.peptideEvidence.empty())
            child()("peptideEvidence", sii.peptideEvidence);
        if (!sii.fragmentation.empty())
            child()("fragmentation", sii.fragmentation);
        if (!sii.paramGroup.empty())
            child()("paramGroup", sii.paramGroup);

        return *this;
    }
    
    TextWriter& operator()(const MzIdentML& mzid)
    {
        (*this)("mzid:");
        child()((IdentifiableType)mzid);
               ("version: " + mzid.version());
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

