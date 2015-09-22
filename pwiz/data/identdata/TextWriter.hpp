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


#ifndef _IDENTDATA_TEXTWRITER_HPP_
#define _IDENTDATA_TEXTWRITER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"
#include "IdentData.hpp"
#include <boost/foreach.hpp>
#include <iostream>
#include <string>
#include <vector>


namespace pwiz {
namespace identdata {

using namespace pwiz::cv;
using namespace pwiz::data;
using namespace boost::logic;

class PWIZ_API_DECL TextWriter
{
    public:

    TextWriter(std::ostream& os, int depth = 0)
    :   os_(os), depth_(depth), indent_(depth*2, ' ')
    {
        os_.precision(14);
    }


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


    TextWriter& operator()(const std::string& label, const float& v)
    {
        os_ << indent_ << label << v << std::endl;
        return *this;
    }


    TextWriter& operator()(const std::string& label, const double& v)
    {
        os_ << indent_ << label << v << std::endl;
        return *this;
    }


    TextWriter& operator()(const std::string& label, const bool& v)
    {
        os_ << indent_ << label << std::boolalpha << v << std::endl;
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
    TextWriter& operator()(const std::vector<object_type>& v)
    {
        for_each(v.begin(), v.end(), child());
        return *this;
    }


    template<typename object_type>
    TextWriter& operator()(const std::string& label, const object_type& v)
    {
        (*this)(label + boost::lexical_cast<std::string>(v));
        return *this;
    }


    TextWriter& operator()(const std::string& label, const ParamContainer& paramContainer)
    {
        (*this)(label); // + ":"
        for_each(paramContainer.cvParams.begin(), paramContainer.cvParams.end(), child());
        for_each(paramContainer.userParams.begin(), paramContainer.userParams.end(), child());
        return *this;
    }


    TextWriter& operator()(const ParamContainer& paramContainer)
    {
        for_each(paramContainer.cvParams.begin(), paramContainer.cvParams.end(), *this);
        for_each(paramContainer.userParams.begin(), paramContainer.userParams.end(), *this);
        return *this;
    }


    TextWriter& operator()(const BibliographicReference& br)
    {
        (*this)("BibliographicReference: ");
        (*this)((Identifiable)br);
        if (!br.authors.empty())
            child()("authors: "+br.authors);
        if (!br.publication.empty())
            child()("publication: "+br.publication);
        if (!br.publisher.empty())
            child()(br.publisher);
        if (!br.editor.empty())
            child()("editor: "+br.editor);
        if (br.year != 0)
            child()("year: ", br.year);
        if (!br.volume.empty())
            child()("volume: "+br.volume);
        if (!br.issue.empty())
            child()("issue: "+br.issue);
        if (!br.pages.empty())
            child()("pages: "+br.pages);
        if (!br.title.empty())
            child()("title: "+br.title);

        return *this;
    }


    TextWriter& operator()(const TranslationTable& tt)
    {
        (*this)("TranslationTable:");
        (*this)((const IdentifiableParamContainer&)tt);
        return *this;
    }


    TextWriter& operator()(const DatabaseTranslation& dt)
    {
        (*this)("DatabaseTranslation:");
        if (!dt.frames.empty())
            child()("frames: ", dt.frames);
        if (!dt.translationTable.empty())
            child()("translationTable: ", dt.translationTable);
        return *this;
    }


    TextWriter& operator()(const SpectrumIdentificationProtocol& si)
    {
        (*this)("SpectrumIdentificationProtocol:");
        (*this)((Identifiable&)si);
        if (si.analysisSoftwarePtr.get() &&
            !si.analysisSoftwarePtr->empty())
            child()("analysisSoftware_ref: "+si.analysisSoftwarePtr->id);
        if (!si.searchType.empty())
            child()("SearchType: ", si.searchType);
        if (!si.additionalSearchParams.empty())
            child()("AdditionalSearchParams", si.additionalSearchParams);
        if (!si.modificationParams.empty())
            child()("ModificationParams", si.modificationParams);
        if (!si.enzymes.empty())
            child()(si.enzymes);
        if (!si.massTable.empty())
            child()(si.massTable);
        if (!si.fragmentTolerance.empty())
            child()("FragmentTolerance", si.fragmentTolerance);
        if (!si.parentTolerance.empty())
            child()("ParentTolerance", si.parentTolerance);
        if (!si.threshold.empty())
            child()("Threshold", si.threshold);
        if (!si.databaseFilters.empty())
            child()("DatabaseFilters", si.databaseFilters);
        if (si.databaseTranslation.get() && !si.databaseTranslation->empty())
            child()("DatabaseTranslation", si.databaseTranslation);

        return *this;
    }


    TextWriter& operator()(const DBSequence& ds)
    {
        (*this)("DBSequence: ");
        (*this)((const IdentifiableParamContainer&)ds);
        if (ds.length!=0)
            child()("length: ", ds.length);
        if (!ds.accession.empty())
            child()("accession: "+ds.accession);
        if (ds.searchDatabasePtr.get() && !ds.searchDatabasePtr->empty())
            child()("searchDatabase_ref: "+ds.searchDatabasePtr->id);
        if (!ds.seq.empty())
            child()("Seq: "+ds.seq);

        return *this;
    }


    TextWriter& operator()(const SubstitutionModification& ds)
    {
        (*this)("SubstitutionModification: ");
        if (ds.originalResidue != 0)
            child()("originalResidue: ", ds.originalResidue);
        if (ds.replacementResidue != 0)
            child()("replacementResidue: ", ds.replacementResidue);
        if (ds.location != 0)
            child()("location: ", ds.location);
        child()("avgMassDelta: ", ds.avgMassDelta);
        child()("monoisotopicMassDelta: ", ds.monoisotopicMassDelta);

        return *this;
    }


    TextWriter& operator()(const IonType& it)
    {
        (*this)("IonType: ");
        if (!it.index.empty())
            child()("index: " + makeDelimitedListString(it.index));
        if (it.charge != 0)
            child()("charge: ", it.charge);
        if (!it.fragmentArray.empty())
            (*this)(it.fragmentArray);
        (*this)((const CVParam&)it);
        return *this;
    }


    TextWriter& operator()(const Measure& m)
    {
        (*this)("Measure: ");
        (*this)((const ParamContainer&)m);
        
        return *this;
    }


    TextWriter& operator()(const SearchDatabase& sd)
    {
        (*this)("SearchDatabase: ");
        (*this)((const IdentifiableParamContainer&)sd);
        if (!sd.location.empty())
            child()("location: " + sd.location);
        if (!sd.version.empty())
            child()("version: " + sd.version);
        if (!sd.releaseDate.empty())
            child()("releaseDate: " + sd.releaseDate);
        if (sd.numDatabaseSequences != 0)
            child()("numDatabaseSequences: ", sd.numDatabaseSequences);
        if (sd.numResidues != 0)
            child()("numResidues: ", sd.numResidues);
        if (!sd.fileFormat.empty())
            child()("FileFormat: ", sd.fileFormat);
        if (!sd.databaseName.empty())
            child()("DatabaseName: ", sd.databaseName);
        return *this;
    }


    TextWriter& operator()(const SpectraData& sd)
    {
        (*this)("SpectraData: ");
        if (!sd.location.empty())
            child()("location: " + sd.location);
        if (!sd.externalFormatDocumentation.empty())
            child()("ExternalFormatDocumentation: ", sd.externalFormatDocumentation);
        if (!sd.fileFormat.empty())
            child()("FileFormat: ", sd.fileFormat);
        if (!sd.spectrumIDFormat.empty())
            child()("SpectrumIDFormat: ", sd.spectrumIDFormat);
        return *this;
    }


    TextWriter& operator()(const SpectrumIdentificationItem& sii)
    {
        (*this)("SpectrumIdentificationItem:");
        if (!sii.id.empty())
            child()("id: ", sii.id);
        if (!sii.name.empty())
            child()("name: ", sii.name);
        if (!sii.empty())
        {
            child()("rank: ", sii.rank);
            child()("chargeState: ", sii.chargeState);
            child()("experimentalMassToCharge: ", sii.experimentalMassToCharge);
            child()("calculatedMassToCharge: ", sii.calculatedMassToCharge);
            child()("calculatedPI: ", sii.calculatedPI);
            child()("passThreshold: ", sii.passThreshold);
        }
        if (sii.peptidePtr.get() && !sii.peptidePtr->empty())
            child()("peptide_ref: ", sii.peptidePtr->id);
        if (sii.massTablePtr.get() && !sii.massTablePtr->empty())
            child()("massTable_ref: ", sii.massTablePtr->id);
        if (sii.samplePtr.get() && !sii.samplePtr->empty())
            child()("sample_ref: ", sii.samplePtr->id);

        BOOST_FOREACH(const PeptideEvidencePtr& pe, sii.peptideEvidencePtr)
            if (pe.get() && !pe->empty())
                child()("peptideEvidence_ref: ", pe->id);

        if (!sii.fragmentation.empty())
            child()("fragmentation", sii.fragmentation);

        child()((const ParamContainer&)sii);

        return *this;
    }


    TextWriter& operator()(const SpectrumIdentificationResult& sir)
    {
        (*this)("SpectrumIdentificationResult: ");
        (*this)((const IdentifiableParamContainer&)sir);
        if (!sir.spectrumID.empty())
            child()("spectrumID: "+sir.spectrumID);
        if (sir.spectraDataPtr.get() && !sir.spectraDataPtr->empty())
            child()("spectraData_ref: "+sir.spectraDataPtr->id);
        if (!sir.spectrumIdentificationItem.empty())
            (*this)(sir.spectrumIdentificationItem);
        
        return *this;
    }


    TextWriter& operator()(const SpectrumIdentificationList& sil)
    {
        (*this)("SpectrumIdentificationList: ");
        (*this)((const IdentifiableParamContainer&)sil);
        if (!sil.empty())
            child()("numSequencesSearched: ", sil.numSequencesSearched);
        if (!sil.fragmentationTable.empty())
            child()("FragmentationTable", sil.fragmentationTable);
        if (!sil.spectrumIdentificationResult.empty())
            (*this)(sil.spectrumIdentificationResult);
        return *this;
    }


    TextWriter& operator()(const ProteinDetectionList& pdl)
    {
        (*this)("ProteinDetectionList: ");
        if (!pdl.proteinAmbiguityGroup.empty())
            (*this)(pdl.proteinAmbiguityGroup);
        (*this)((const ParamContainer&)pdl);
        return *this;
    }


    TextWriter& operator()(const AnalysisData& ad)
    {
        (*this)("AnalysisData: ");

        if (!ad.spectrumIdentificationList.empty())
            (*this)(ad.spectrumIdentificationList);
        if (ad.proteinDetectionListPtr.get() &&
            !ad.proteinDetectionListPtr->empty())
            (*this)(*ad.proteinDetectionListPtr);
        
        return *this;
    }


    TextWriter& operator()(const FragmentArray& fa)
    {
        (*this)("FragmentArray: ");

        if (fa.measurePtr.get() && !fa.measurePtr->empty())
            child()("measure_ref: " + fa.measurePtr->id);
        if (!fa.values.empty())
            child()("values: " + makeDelimitedListString(fa.values));

        return *this;
    }


    TextWriter& operator()(const SourceFile& sf)
    {
        //(*this)("sourceFile: ");

        (*this)((const IdentifiableParamContainer&)sf);
        if (!sf.location.empty())
            child()("location: " + sf.location);
        if (!sf.fileFormat.empty())
            child()(sf.fileFormat);
        if (!sf.externalFormatDocumentation.empty())
            child()("externalFormatDocumentation: ",
                    sf.externalFormatDocumentation);
        
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
            child()("fixedMod: ", sm.fixedMod);
        if (sm.massDelta != 0)
            child()("massDelta: ", sm.massDelta);
        if (!sm.residues.empty())
            child()("residues: " + makeDelimitedListString(sm.residues));
        if (!sm.specificityRules.empty())
            child()("specificityRules: ", sm.specificityRules);
        child()((const ParamContainer&)sm);  
        return *this;
    }


    TextWriter& operator()(const Enzymes& ezs)
    {
        (*this)("Enzymes: ");
        if (!indeterminate(ezs.independent))
            child()("independent: ", ezs.independent);
        if (!ezs.enzymes.empty())
            child()("enzymes: ", ezs.enzymes);
        return *this;
    }


    TextWriter& operator()(const MassTable& mt)
    {
        (*this)("MassTable: ");
        if (!mt.id.empty())
            child()("id: " + mt.id);
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
            child()("spectrumIdentificationProtocol: ",
                    apc.spectrumIdentificationProtocol);
        if (!apc.proteinDetectionProtocol.empty())
            child()("proteinDetectionProtocol: ",
                    apc.proteinDetectionProtocol);
        return *this;
    }


    TextWriter& operator()(const PeptideHypothesis& ph)
    {
        (*this)("PeptideHypothesis: ");

        if (ph.peptideEvidencePtr.get())
            child()("peptideEvidence: ", ph.peptideEvidencePtr->id);
        child()("spectrumIdentificationItem: " + makeDelimitedRefListString(ph.spectrumIdentificationItemPtr));
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
            (*this)(pdh.peptideHypothesis);
 
        child()((const ParamContainer&)pdh);                  
        return *this;
    }


    TextWriter& operator()(const ProteinAmbiguityGroup& pag)
    {
        (*this)("ProteinAmbiguityGroup: ");
        if (!pag.proteinDetectionHypothesis.empty())
            (*this)(pag.proteinDetectionHypothesis);
        (*this)((const ParamContainer&)pag);

        return *this;
    }


    TextWriter& operator()(const ProteinDetection& pd)
    {
        (*this)("ProteinDetection: ");
        if (pd.proteinDetectionProtocolPtr.get() &&
            !pd.proteinDetectionProtocolPtr->empty())
            child()("proteinDetectionProtocol_ref: "+pd.proteinDetectionProtocolPtr->id);
        if (pd.proteinDetectionListPtr.get() &&
            !pd.proteinDetectionListPtr->empty())
            child()("proteinDetectionList_ref: "+pd.proteinDetectionListPtr->id);
        if (!pd.activityDate.empty())
            child()("activityDate: "+pd.activityDate);
        child()("inputSpectrumIdentifications: " + makeDelimitedRefListString(pd.inputSpectrumIdentifications));
        return *this;
    }


    TextWriter& operator()(const SpectrumIdentification& si)
    {
        (*this)("SpectrumIdentification: ");
        if (si.spectrumIdentificationProtocolPtr.get() &&
            !si.spectrumIdentificationProtocolPtr->empty())
            child()("spectrumIdentificationProtocol_ref: "+si.spectrumIdentificationProtocolPtr->id);
        if (si.spectrumIdentificationListPtr.get() &&
            !si.spectrumIdentificationListPtr->empty())
            child()("spectrumIdentificationList_ref: "+si.spectrumIdentificationListPtr->id);
        if (!si.activityDate.empty())
            child()("activityDate: "+si.activityDate);
        if (!si.inputSpectra.empty())
            child()("inputSpectra: " + makeDelimitedRefListString(si.inputSpectra));
        if (!si.searchDatabase.empty())
            child()("searchDatabase: " + makeDelimitedRefListString(si.searchDatabase));
        
        return *this;
    }


    TextWriter& operator()(const AnalysisCollection& ac)
    {
        (*this)("AnalysisCollection: ", ac.spectrumIdentification);
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
        if (!sc.peptideEvidence.empty())
            child()("peptideEvidence: ", sc.peptideEvidence);
        return *this;
    }


    TextWriter& operator()(const Contact& cont)
    {
        (*this)((const IdentifiableParamContainer&)cont);

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
        if (!per.affiliations.empty())
            child()("affiliations: ", per.affiliations);
        
        return *this;
    }


    TextWriter& operator()(const Organization& org)
    {
        (*this)("Organization: ");
        (*this)((const Contact&)org);
        if (org.parent.get())
            child()("Parent: ", org.parent->id);

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
            child()("contact_ref: ", cr.contactPtr->id);
        if (!cr.CVParam::empty())
            child()("Role: ", (const CVParam&)cr);
        return (*this);
    }


    TextWriter& operator()(const Provider& provider)
    {
        (*this)("Provider: ");
        (*this)((Identifiable&)provider);
        if (provider.contactRolePtr.get() && !provider.contactRolePtr->empty())
            child()(provider.contactRolePtr);
        return *this;
    }


    TextWriter& operator()(const Sample& sample)
    {
        (*this)("Sample: ");
        (*this)((const IdentifiableParamContainer&)sample);
        (*this)(sample.contactRole);
        child()(sample.cvParams);
        child()("SubSamples:", sample.subSamples);

        return *this;
    }


    TextWriter& operator()(const AnalysisSampleCollection& asc)
    {
        (*this)("AnalysisSampleCollection: ", asc.samples);

        return *this;
    }


    TextWriter& operator()(const AnalysisSoftwarePtr& asp)
    {
        (*this)("analysisSoftware:");
        (*this)((Identifiable)*asp);
        if (!asp->version.empty())
            child()("version: "+asp->version);
        if (asp->contactRolePtr.get() && asp->contactRolePtr->empty())
            child()(*asp->contactRolePtr);
        if (!asp->softwareName.empty())
            child()("softwareName: ", asp->softwareName);
        if (!asp->URI.empty())
            child()("uri: "+asp->URI);
        if (!asp->customizations.empty())
            child()("customizations: "+asp->customizations);
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
        child()("semiSpecific: ", (enzyme.terminalSpecificity != proteome::Digestion::FullySpecific ? "true": "false"));
        if (enzyme.missedCleavages != 0)
            child()("missedCleavages: ", enzyme.missedCleavages);
        if (enzyme.minDistance != 0)
            child()("minDistance: ", enzyme.minDistance);
        if (!enzyme.siteRegexp.empty())
            child()("SiteRegexp: "+enzyme.siteRegexp);
        if (!enzyme.enzymeName.empty())
            child()("EnzymeName: ", enzyme.enzymeName);

        return *this;
    }


    TextWriter& operator()(const Identifiable& id)
    {
        if (!id.id.empty())
            child()("id: "+id.id);
        if (!id.name.empty())
            child()("name: "+id.name);

        return *this;
    }


    TextWriter& operator()(const IdentifiableParamContainer& id)
    {
        if (!id.id.empty())
            child()("id: "+id.id);
        if (!id.name.empty())
            child()("name: "+id.name);

        child()((const ParamContainer&)id);

        return *this;
    }


    TextWriter& operator()(const Residue& res)
    {
        (*this)("Residue: ");
        if (res.code != 0)
            child()("code: ", res.code);
        if (res.mass != 0)
            child()("mass: ", res.mass);
        return *this;
    }


    TextWriter& operator()(const AmbiguousResidue& res)
    {
        (*this)("AmbiguousResidue: ");
        if (res.code != 0)
            child()("code: ", res.code);
        (*this)((const ParamContainer&)res);
        
        return *this;
    }


    TextWriter& operator()(const Modification& mod)
    {
        (*this)("Modification: ");
        if (mod.location != 0)
            child()("location: ", mod.location);
        if (!mod.residues.empty())
            child()("residues: " + makeDelimitedListString(mod.residues));
        if (mod.avgMassDelta != 0)
            child()("avgMassDelta: ", mod.avgMassDelta);
        if (mod.monoisotopicMassDelta != 0)
            child()("monoisotopicMassDelta: ", mod.monoisotopicMassDelta);
        child()((const ParamContainer&)mod);

        return *this;
    }


    TextWriter& operator()(const Peptide& pep)
    {
        (*this)("Peptide: ");
        (*this)((const IdentifiableParamContainer&)pep);
        if (!pep.peptideSequence.empty())
            child()("peptideSequence: "+pep.peptideSequence);
        if (!pep.modification.empty())
            child()("modification", pep.modification);
        if (!pep.substitutionModification.empty())
            child()(pep.substitutionModification);
        
        return *this;
    }


    TextWriter& operator()(const PeptideEvidence& pe)
    {
        (*this)("PeptideEvidence: ");
        (*this)((const IdentifiableParamContainer&)pe);
        if (pe.peptidePtr.get() && !pe.peptidePtr->empty())
            child()("peptide_ref: "+pe.peptidePtr->id);
        if (pe.dbSequencePtr.get() && !pe.dbSequencePtr->empty())
            child()("dBSequence_ref: "+pe.dbSequencePtr->id);
        if (pe.start != 0)
            child()("start: ", pe.start);
        if (pe.end != 0)
            child()("end: ", pe.end);
        if (pe.pre != 0)
            child()("pre: ", pe.pre);
        if (pe.post != 0)
            child()("post: ", pe.post);
        if (pe.translationTablePtr.get() && !pe.translationTablePtr->empty())
            child()("translationTable_ref: "+pe.translationTablePtr->id);
        if (pe.frame != 0)
            child()("frame: ", pe.frame);
        child()("isDecoy: ", pe.isDecoy);
        
        return *this;
    }


    TextWriter& operator()(const IdentData& mzid)
    {
        (*this)("mzid:");
        child()((Identifiable)mzid);
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

    template <typename object_type>
    std::string makeDelimitedRefListString(const std::vector<boost::shared_ptr<object_type> >& objects, const char* delimiter = " ")
    {
        std::ostringstream oss;
        for (size_t i=0; i < objects.size(); ++i)
        {
            if (i > 0) oss << delimiter;
            oss << objects[i]->id;
        }
        return oss.str();
    }

    template <typename object_type>
    std::string makeDelimitedListString(const std::vector<object_type>& objects, const char* delimiter = " ")
    {
        std::ostringstream oss;
        oss.precision(9);
        for (size_t i=0; i < objects.size(); ++i)
        {
            if (i > 0) oss << delimiter;
            oss << objects[i];
        }
        return oss.str();
    }
};

	
} // namespace identdata
} // namespace pwiz


#endif // _IDENTDATA_TEXTWRITER_HPP_

