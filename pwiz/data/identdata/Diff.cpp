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

#include "Diff.hpp"
#include "TextWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>


namespace pwiz {
namespace data {
namespace diff_impl {


using namespace pwiz::data;
using namespace pwiz::identdata;


const char* userParamName_FragmentArrayDifference_ = "FragmentArray difference";

PWIZ_API_DECL
void diff(const FragmentArray& a,
          const FragmentArray& b,
          FragmentArray& a_b,
          FragmentArray& b_a,
          const DiffConfig& config)
{
    bool valuesDiff = false;
    for (size_t i=0, end=max(a.values.size(), b.values.size()); i < end; ++i)
    {
        if (i < a.values.size() && i < b.values.size())
        {
            if (fabs(a.values[i] - b.values[i]) > config.precision + numeric_limits<double>::epsilon())
            {
                valuesDiff = true;
                a_b.values.push_back(a.values[i] - b.values[i]);
                b_a.values.push_back(b.values[i] - a.values[i]);
                if (config.partialDiffOK)
                    return; // we just want to know that they differ, not how they differ
            }
            else
            {
                a_b.values.push_back(0);
                b_a.values.push_back(0);
            }
        }
        else
        {
            valuesDiff = true;
            if (i < a.values.size())
                a_b.values.push_back(a.values[i]);
            else
                b_a.values.push_back(b.values[i]);
            if (config.partialDiffOK)
                return; // we just want to know that they differ, not how they differ
        }
    }

    if (!valuesDiff)
    {
        a_b.values.clear();
        b_a.values.clear();
    }

    ptr_diff(a.measurePtr, b.measurePtr, a_b.measurePtr, b_a.measurePtr, config);
}


PWIZ_API_DECL
void diff(const Measure& a,
          const Measure& b,
          Measure& a_b,
          Measure& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const IdentifiableParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const SearchModification& a,
          const SearchModification& b,
          SearchModification& a_b,
          SearchModification& b_a,
          const DiffConfig& config)
{
    if (a.fixedMod != b.fixedMod)
    {
        a_b.fixedMod = a.fixedMod;
        b_a.fixedMod = b.fixedMod;
    }
    diff_floating(a.massDelta, b.massDelta, a_b.massDelta, b_a.massDelta, config);
    vector_diff(a.residues, b.residues, a_b.residues, b_a.residues);
    diff(a.specificityRules, b.specificityRules, a_b.specificityRules, b_a.specificityRules, config);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const IonType& a,
          const IonType& b,
          IonType& a_b,
          IonType& b_a,
          const DiffConfig& config)
{
    diff_integral(a.charge, b.charge, a_b.charge, b_a.charge, config);
    vector_diff(a.index, b.index, a_b.index, b_a.index);
    vector_diff_deep(a.fragmentArray, b.fragmentArray, a_b.fragmentArray, b_a.fragmentArray, config);
    diff(static_cast<const CVParam&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const DataCollection& a,
          const DataCollection& b,
          DataCollection& a_b,
          DataCollection& b_a,
          const DiffConfig& config)
{
    diff(a.inputs, b.inputs, a_b.inputs, b_a.inputs, config);
    diff(a.analysisData, b.analysisData, a_b.analysisData, b_a.analysisData, config);
}

#define QUICKCHECK() \
    if (config.partialDiffOK && \
        (!a_b.id.empty() || !b_a.id.empty() || \
         !a_b.name.empty() || !b_a.name.empty())) \
        return; // we just want to know that they differ, not how they differ

PWIZ_API_DECL
void diff(const PeptideEvidence& a,
          const PeptideEvidence& b,
          PeptideEvidence& a_b,
          PeptideEvidence& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const IdentifiableParamContainer&>(a), b, a_b, b_a, config);
    QUICKCHECK(); // in case we just want to know that they differ, not how they differ
    ptr_diff(a.peptidePtr, b.peptidePtr, a_b.peptidePtr, b_a.peptidePtr, config);
    ptr_diff(a.dbSequencePtr, b.dbSequencePtr, a_b.dbSequencePtr, b_a.dbSequencePtr, config);
    diff_integral(a.start, b.start, a_b.start, b_a.start, config);
    diff_integral(a.end, b.end, a_b.end, b_a.end, config);
    diff_char(a.pre, b.pre, a_b.pre, b_a.pre);
    diff_char(a.post, b.post, a_b.post, b_a.post);
    ptr_diff(a.translationTablePtr, b.translationTablePtr,
             a_b.translationTablePtr, b_a.translationTablePtr, config);
    diff_integral(a.frame, b.frame, a_b.frame, b_a.frame, config);
    if(a.isDecoy != b.isDecoy)
    {
        a_b.isDecoy = a.isDecoy;
        b_a.isDecoy = b.isDecoy;
    }
}


PWIZ_API_DECL
void diff(const SpectrumIdentificationItem& a,
          const SpectrumIdentificationItem& b,
          SpectrumIdentificationItem& a_b,
          SpectrumIdentificationItem& b_a,
          const DiffConfig& config)
{
    diff_integral(a.chargeState, b.chargeState, a_b.chargeState, b_a.chargeState, config);
    diff_floating(a.experimentalMassToCharge, b.experimentalMassToCharge,
                  a_b.experimentalMassToCharge, b_a.experimentalMassToCharge,
                  config);
    diff_floating(a.calculatedMassToCharge, b.calculatedMassToCharge,
                  a_b.calculatedMassToCharge, b_a.calculatedMassToCharge,
                  config);
    diff_floating(a.calculatedPI, b.calculatedPI, a_b.calculatedPI, b_a.calculatedPI, config);
    if (config.partialDiffOK && (!a_b.empty() || !b_a.empty()))
        return; // we just want to know that they differ, not how they differ
    ptr_diff(a.peptidePtr, b.peptidePtr, a_b.peptidePtr, b_a.peptidePtr, config);
    diff_integral(a.rank, b.rank, a_b.rank, b_a.rank, config);

    if(a.passThreshold != b.passThreshold)
    {
        a_b.passThreshold = a.passThreshold;
        b_a.passThreshold = b.passThreshold;
    }

    ptr_diff(a.massTablePtr, b.massTablePtr, a_b.massTablePtr, b_a.massTablePtr, config);
    ptr_diff(a.samplePtr, b.samplePtr, a_b.samplePtr, b_a.samplePtr, config);
    vector_diff_deep(a.peptideEvidencePtr, b.peptideEvidencePtr,
                     a_b.peptideEvidencePtr, b_a.peptideEvidencePtr, config);
    vector_diff_deep(a.fragmentation, b.fragmentation,
                     a_b.fragmentation, b_a.fragmentation, config);

    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const SpectrumIdentificationResult& a,
          const SpectrumIdentificationResult& b,
          SpectrumIdentificationResult& a_b,
          SpectrumIdentificationResult& b_a,
          const DiffConfig& config)
{
    diff(a.spectrumID, b.spectrumID, a_b.spectrumID, b_a.spectrumID, config);
    if (config.partialDiffOK && (!a_b.empty() || !b_a.empty()))
        return; // we just want to know that they differ, not how they differ
    ptr_diff(a.spectraDataPtr, b.spectraDataPtr, a_b.spectraDataPtr, b_a.spectraDataPtr, config);

    vector_diff_deep(a.spectrumIdentificationItem, b.spectrumIdentificationItem,
                     a_b.spectrumIdentificationItem, b_a.spectrumIdentificationItem,
                     config);

    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const SpectrumIdentificationListPtr a,
          const SpectrumIdentificationListPtr b,
          SpectrumIdentificationListPtr a_b,
          SpectrumIdentificationListPtr b_a,
          const DiffConfig& config)
{
    ptr_diff(a, b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const SpectrumIdentificationList& a,
          const SpectrumIdentificationList& b,
          SpectrumIdentificationList& a_b,
          SpectrumIdentificationList& b_a,
          const DiffConfig& config)
{
    diff_integral(a.numSequencesSearched, b.numSequencesSearched,
                  a_b.numSequencesSearched, b_a.numSequencesSearched, config);
    vector_diff_deep(a.fragmentationTable, b.fragmentationTable,
                     a_b.fragmentationTable, b_a.fragmentationTable,
                     config);
    vector_diff_deep(a.spectrumIdentificationResult,
                     b.spectrumIdentificationResult,
                     a_b.spectrumIdentificationResult,
                     b_a.spectrumIdentificationResult, config);
}


PWIZ_API_DECL
void diff(const PeptideHypothesis& a,
          const PeptideHypothesis& b,
          PeptideHypothesis& a_b,
          PeptideHypothesis& b_a,
          const DiffConfig& config)
{
    ptr_diff(a.peptideEvidencePtr, b.peptideEvidencePtr,
             a_b.peptideEvidencePtr, b_a.peptideEvidencePtr,
             config);
    vector_diff_deep(a.spectrumIdentificationItemPtr, b.spectrumIdentificationItemPtr,
                     a_b.spectrumIdentificationItemPtr, b_a.spectrumIdentificationItemPtr,
                     config);
}


PWIZ_API_DECL
void diff(const ProteinDetectionHypothesis& a,
          const ProteinDetectionHypothesis& b,
          ProteinDetectionHypothesis& a_b,
          ProteinDetectionHypothesis& b_a,
          const DiffConfig& config)
{
    ptr_diff(a.dbSequencePtr, b.dbSequencePtr,
         a_b.dbSequencePtr, b_a.dbSequencePtr, config);
    if (a.passThreshold != b.passThreshold)
    {
        a_b.passThreshold = a.passThreshold;
        b_a.passThreshold = b.passThreshold;
    }
    vector_diff_diff(a.peptideHypothesis, b.peptideHypothesis,
                     a_b.peptideHypothesis, b_a.peptideHypothesis, config);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const ProteinAmbiguityGroup& a,
          const ProteinAmbiguityGroup& b,
          ProteinAmbiguityGroup& a_b,
          ProteinAmbiguityGroup& b_a,
          const DiffConfig& config)
{
    vector_diff_deep(a.proteinDetectionHypothesis, b.proteinDetectionHypothesis, a_b.proteinDetectionHypothesis, b_a.proteinDetectionHypothesis, config);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const ProteinDetectionList& a,
          const ProteinDetectionList& b,
          ProteinDetectionList& a_b,
          ProteinDetectionList& b_a,
          const DiffConfig& config)
{    
    diff(static_cast<const IdentifiableParamContainer&>(a), b, a_b, b_a, config);
    QUICKCHECK(); // in case we just want to know that they differ, not how they differ
    vector_diff_deep(a.proteinAmbiguityGroup, b.proteinAmbiguityGroup,
                     a_b.proteinAmbiguityGroup, b_a.proteinAmbiguityGroup,
                     config);
}


PWIZ_API_DECL
void diff(const AnalysisData& a,
          const AnalysisData& b,
          AnalysisData& a_b,
          AnalysisData& b_a,
          const DiffConfig& config)
{
    vector_diff_deep(a.spectrumIdentificationList,
                     b.spectrumIdentificationList,
                     a_b.spectrumIdentificationList,
                     b_a.spectrumIdentificationList, config);
    ptr_diff(a.proteinDetectionListPtr, b.proteinDetectionListPtr,
             a_b.proteinDetectionListPtr, b_a.proteinDetectionListPtr,
             config);
}


PWIZ_API_DECL
void diff(const SearchDatabase& a,
          const SearchDatabase& b,
          SearchDatabase& a_b,
          SearchDatabase& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const IdentifiableParamContainer&>(a), b, a_b, b_a, config);
    QUICKCHECK(); // in case we just want to know that they differ, not how they differ
    diff(a.location, b.location, a_b.location, b_a.location, config);
	if (!config.ignoreVersions)
        diff(a.version, b.version, a_b.version, b_a.version, config);
    diff(a.releaseDate, b.releaseDate, a_b.releaseDate, b_a.releaseDate, config);
    if (a.numDatabaseSequences != b.numDatabaseSequences)
    {
        a_b.numDatabaseSequences = a.numDatabaseSequences;
        b_a.numDatabaseSequences = b.numDatabaseSequences;
    }

    if (a.numResidues != b.numResidues)
    {
        a_b.numResidues = a.numResidues;
        b_a.numResidues = b.numResidues;
    }

    diff(a.fileFormat, b.fileFormat, a_b.fileFormat, b_a.fileFormat, config);
    diff(a.databaseName, b.databaseName, a_b.databaseName, b_a.databaseName, config);
}


PWIZ_API_DECL
void diff(const SourceFile& a,
          const SourceFile& b,
          SourceFile& a_b,
          SourceFile& b_a,
          const DiffConfig& config)
{
    diff(a.location, b.location, a_b.location, b_a.location, config);
    diff(a.fileFormat, b.fileFormat, a_b.fileFormat, b_a.fileFormat, config);
    vector_diff_diff(a.externalFormatDocumentation,
                     b.externalFormatDocumentation,
                     a_b.externalFormatDocumentation,
                     b_a.externalFormatDocumentation, config);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const SpectraData& a,
          const SpectraData& b,
          SpectraData& a_b,
          SpectraData& b_a,
          const DiffConfig& config)
{
    diff(a.location, b.location, a_b.location, b_a.location, config);
    vector_diff_diff(a.externalFormatDocumentation,
                     b.externalFormatDocumentation,
                     a_b.externalFormatDocumentation,
                     b_a.externalFormatDocumentation, config);
    diff(a.fileFormat, b.fileFormat, a_b.fileFormat, b_a.fileFormat, config);
    diff(a.spectrumIDFormat, b.spectrumIDFormat, a_b.spectrumIDFormat, b_a.spectrumIDFormat, config);
}


PWIZ_API_DECL
void diff(const Inputs& a,
          const Inputs& b,
          Inputs& a_b,
          Inputs& b_a,
          const DiffConfig& config)
{
    vector_diff_deep(a.sourceFile, b.sourceFile, a_b.sourceFile, b_a.sourceFile, config);
    vector_diff_deep(a.searchDatabase, b.searchDatabase, a_b.searchDatabase, b_a.searchDatabase, config);
    vector_diff_deep(a.spectraData, b.spectraData, a_b.spectraData, b_a.spectraData, config);
}


PWIZ_API_DECL
void diff(const Enzyme& a,
          const Enzyme& b,
          Enzyme& a_b,
          Enzyme& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const Identifiable&>(a), b, a_b, b_a, config);
    QUICKCHECK(); // in case we just want to know that they differ, not how they differ
    diff(a.nTermGain, b.nTermGain, a_b.nTermGain, b_a.nTermGain,config);
    diff(a.cTermGain, b.cTermGain, a_b.cTermGain, b_a.cTermGain,config);
    diff_integral(a.terminalSpecificity, b.terminalSpecificity,
                  a_b.terminalSpecificity, b_a.terminalSpecificity, config);
    diff_integral(a.missedCleavages, b.missedCleavages, a_b.missedCleavages, b_a.missedCleavages,config);
    diff_integral(a.minDistance, b.minDistance, a_b.minDistance, b_a.minDistance,config);
    diff(a.siteRegexp, b.siteRegexp, a_b.siteRegexp, b_a.siteRegexp,config);
    diff(a.enzymeName, b.enzymeName, a_b.enzymeName, b_a.enzymeName,config);
}


PWIZ_API_DECL
void diff(const Enzymes& a,
          const Enzymes& b,
          Enzymes& a_b,
          Enzymes& b_a,
          const DiffConfig& config)
{
    diff(a.independent, b.independent, a_b.independent, b_a.independent,config);
    vector_diff_deep(a.enzymes, b.enzymes, a_b.enzymes, b_a.enzymes,config);
}


PWIZ_API_DECL
void diff(const MassTable& a,
          const MassTable& b,
          MassTable& a_b,
          MassTable& b_a,
          const DiffConfig& config)
{
    diff(a.id, b.id, a_b.id, b_a.id, config);
    vector_diff(a.msLevel, b.msLevel, a_b.msLevel, b_a.msLevel);
    vector_diff_deep(a.residues, b.residues, a_b.residues, b_a.residues, config);
    vector_diff_deep(a.ambiguousResidue, b.ambiguousResidue, a_b.ambiguousResidue, b_a.ambiguousResidue, config); 
}


PWIZ_API_DECL
void diff(const Residue& a,
          const Residue& b,
          Residue& a_b,
          Residue& b_a,
          const DiffConfig& config)
{
    diff_char(a.code, b.code, a_b.code, b_a.code);
    diff_floating(a.mass, b.mass, a_b.mass, b_a.mass, config);
}


PWIZ_API_DECL
void diff(const AmbiguousResidue& a,
          const AmbiguousResidue& b,
          AmbiguousResidue& a_b,
          AmbiguousResidue& b_a,
          const DiffConfig& config)
{
    diff_char(a.code, b.code, a_b.code, b_a.code);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const Filter& a,
          const Filter& b,
          Filter& a_b,
          Filter& b_a,
          const DiffConfig& config)
{
    diff(a.filterType, b.filterType, a_b.filterType, b_a.filterType, config);
    diff(a.include, b.include, a_b.include, b_a.include, config);
    diff(a.exclude, b.exclude, a_b.exclude, b_a.exclude, config);
}


PWIZ_API_DECL
void diff(const DatabaseTranslation& a,
          const DatabaseTranslation& b,
          DatabaseTranslation& a_b,
          DatabaseTranslation& b_a,
          const DiffConfig& config)
{
    vector_diff(a.frames, b.frames, a_b.frames, b_a.frames);
    vector_diff_deep(a.translationTable, b.translationTable, a_b.translationTable, b_a.translationTable, config);
}


PWIZ_API_DECL
void diff(const SpectrumIdentificationProtocol& a,
          const SpectrumIdentificationProtocol& b,
          SpectrumIdentificationProtocol& a_b,
          SpectrumIdentificationProtocol& b_a,
          const DiffConfig& config)
{
    ptr_diff(a.analysisSoftwarePtr, b.analysisSoftwarePtr,
             a_b.analysisSoftwarePtr, b_a.analysisSoftwarePtr,
             config);
    diff(a.searchType, b.searchType, a_b.searchType, b_a.searchType, config);
    vector_diff_deep(a.modificationParams, b.modificationParams,
                     a_b.modificationParams, b_a.modificationParams, config);
    diff(a.additionalSearchParams, b.additionalSearchParams,
         a_b.additionalSearchParams, b_a.additionalSearchParams, config);
    diff(a.enzymes, b.enzymes, a_b.enzymes, b_a.enzymes, config);
    vector_diff_deep(a.massTable, b.massTable, a_b.massTable, b_a.massTable, config);
    diff(a.fragmentTolerance, b.fragmentTolerance, a_b.fragmentTolerance, b_a.fragmentTolerance, config);
    diff(a.parentTolerance, b.parentTolerance, a_b.parentTolerance, b_a.parentTolerance, config);
    diff(a.threshold, b.threshold, a_b.threshold, b_a.threshold, config);
    vector_diff_deep(a.databaseFilters, b.databaseFilters, a_b.databaseFilters, b_a.databaseFilters, config);
    
    ptr_diff(a.databaseTranslation, b.databaseTranslation,
             a_b.databaseTranslation, b_a.databaseTranslation,
             config);
}


PWIZ_API_DECL
void diff(const ProteinDetectionProtocol& a,
          const ProteinDetectionProtocol& b,
          ProteinDetectionProtocol& a_b,
          ProteinDetectionProtocol& b_a,
          const DiffConfig& config)
{
    ptr_diff(a.analysisSoftwarePtr, b.analysisSoftwarePtr, a_b.analysisSoftwarePtr, b_a.analysisSoftwarePtr, config);
    diff(a.analysisParams, b.analysisParams, a_b.analysisParams, b_a.analysisParams, config);
    diff(a.threshold, b.threshold, a_b.threshold, b_a.threshold, config);
}


PWIZ_API_DECL
void diff(const AnalysisProtocolCollection& a,
          const AnalysisProtocolCollection& b,
          AnalysisProtocolCollection& a_b,
          AnalysisProtocolCollection& b_a,
          const DiffConfig& config)
{
    vector_diff_deep(a.spectrumIdentificationProtocol,
                     b.spectrumIdentificationProtocol,
                     a_b.spectrumIdentificationProtocol,
                     b_a.spectrumIdentificationProtocol,
                     config);
    vector_diff_deep(a.proteinDetectionProtocol,
                     b.proteinDetectionProtocol,
                     a_b.proteinDetectionProtocol,
                     b_a.proteinDetectionProtocol,
                     config);
}


PWIZ_API_DECL
void diff(const Contact& a,
          const Contact& b,
          Contact& a_b,
          Contact& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const IdentifiableParamContainer&>(a), b, a_b, b_a, config);
}


const char* ContactPtr_diff_string_ = "Contact type different ";

PWIZ_API_DECL
void diff(const ContactPtr a,
          const ContactPtr b,
          ContactPtr& a_b,
          ContactPtr& b_a,
          const DiffConfig& config)
{
    Person* a_person = dynamic_cast<Person*>(a.get());
    Person* b_person = dynamic_cast<Person*>(b.get());
    
    Organization* a_organization = dynamic_cast<Organization*>(a.get());
    Organization* b_organization = dynamic_cast<Organization*>(b.get());
    
    if (a_person && b_person)
    {
        a_b = ContactPtr(new Person());
        b_a = ContactPtr(new Person());
        diff(*a_person, *b_person,
                 (Person&)*a_b, (Person&)*b_a, config);
    }
    else if (a_organization && b_organization)
    {
        a_b = ContactPtr(new Organization());
        b_a = ContactPtr(new Organization());
        diff(*a_organization, *b_organization,
                 (Organization&)*a_b, (Organization&)*b_a, config);
    }
    else
    {
        ptr_diff(a, b, a_b, b_a, config);

        string a_type = (a_person ? "Person" : (a_organization ? "Organization" : "Contact"));
        string b_type = (b_person ? "Person" : (b_organization ? "Organization" : "Contact"));
    }
}


PWIZ_API_DECL
void diff(const Person& a,
          const Person& b,
          Person& a_b,
          Person& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const Contact&>(a), b, a_b, b_a, config);
    diff(a.lastName, b.lastName, a_b.lastName, b_a.lastName, config);
    diff(a.firstName, b.firstName, a_b.firstName, b_a.firstName, config);
    diff(a.midInitials, b.midInitials, a_b.midInitials, b_a.midInitials, config);
    vector_diff_deep(a.affiliations, b.affiliations, a_b.affiliations, b_a.affiliations, config);
}


PWIZ_API_DECL
void diff(const Organization& a,
          const Organization& b,
          Organization& a_b,
          Organization& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const Contact&>(a), b, a_b, b_a, config);
    ptr_diff(a.parent, b.parent, a_b.parent, b_a.parent, config);
}


PWIZ_API_DECL
void diff(const BibliographicReference& a,
          const BibliographicReference& b,
          BibliographicReference& a_b,
          BibliographicReference& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const Identifiable&>(a), b, a_b, b_a, config);
    QUICKCHECK(); // in case we just want to know that they differ, not how they differ
    diff(a.authors, b.authors, a_b.authors, b_a.authors, config);
    diff(a.publication, b.publication, a_b.publication, b_a.publication, config);
    diff(a.publisher, b.publisher, a_b.publisher, b_a.publisher, config);
    diff(a.editor, b.editor, a_b.editor, b_a.editor, config);
    diff_integral(a.year, b.year, a_b.year, b_a.year, config);
    diff(a.volume, b.volume, a_b.volume, b_a.volume, config);
    diff(a.issue, b.issue, a_b.issue, b_a.issue, config);
    diff(a.pages, b.pages, a_b.pages, b_a.pages, config);
    diff(a.title, b.title, a_b.title, b_a.title, config);
}


PWIZ_API_DECL
void diff(const ProteinDetection& a,
          const ProteinDetection& b,
          ProteinDetection& a_b,
          ProteinDetection& b_a,
          const DiffConfig& config)
{
    ptr_diff(a.proteinDetectionProtocolPtr, b.proteinDetectionProtocolPtr,
             a_b.proteinDetectionProtocolPtr, b_a.proteinDetectionProtocolPtr, config);
    ptr_diff(a.proteinDetectionListPtr, b.proteinDetectionListPtr,
             a_b.proteinDetectionListPtr, b_a.proteinDetectionListPtr, config);
    diff(a.activityDate, b.activityDate, a_b.activityDate, b_a.activityDate, config);
    vector_diff_deep(a.inputSpectrumIdentifications, b.inputSpectrumIdentifications,
                     a_b.inputSpectrumIdentifications, b_a.inputSpectrumIdentifications, config);
}


PWIZ_API_DECL
void diff(const SpectrumIdentification& a,
          const SpectrumIdentification& b,
          SpectrumIdentification& a_b,
          SpectrumIdentification& b_a,
          const DiffConfig& config)
{
    ptr_diff(a.spectrumIdentificationProtocolPtr, b.spectrumIdentificationProtocolPtr,
             a_b.spectrumIdentificationProtocolPtr, b_a.spectrumIdentificationProtocolPtr, config);
    ptr_diff(a.spectrumIdentificationListPtr, b.spectrumIdentificationListPtr,
             a_b.spectrumIdentificationListPtr, b_a.spectrumIdentificationListPtr, config);
    diff(a.activityDate, b.activityDate, a_b.activityDate, b_a.activityDate, config);
    vector_diff_deep(a.inputSpectra, b.inputSpectra, a_b.inputSpectra, b_a.inputSpectra, config);
    vector_diff_deep(a.searchDatabase, b.searchDatabase, a_b.searchDatabase, b_a.searchDatabase, config);
}


PWIZ_API_DECL
void diff(const AnalysisCollection& a,
          const AnalysisCollection& b,
          AnalysisCollection& a_b,
          AnalysisCollection& b_a,
          const DiffConfig& config)
{
    vector_diff_deep(a.spectrumIdentification, b.spectrumIdentification,
                     a_b.spectrumIdentification, b_a.spectrumIdentification,
                     config);
    diff(a.proteinDetection, b.proteinDetection,
         a_b.proteinDetection, b_a.proteinDetection, config);
}


PWIZ_API_DECL
void diff(const DBSequence& a,
          const DBSequence& b,
          DBSequence& a_b,
          DBSequence& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const IdentifiableParamContainer&>(a), b, a_b, b_a, config);
    QUICKCHECK(); // in case we just want to know that they differ, not how they differ
    diff_integral(a.length, b.length, a_b.length, b_a.length, config);
    diff(a.accession, b.accession, a_b.accession, b_a.accession, config);
    ptr_diff(a.searchDatabasePtr, b.searchDatabasePtr, a_b.searchDatabasePtr, b_a.searchDatabasePtr, config);
    diff(a.seq, b.seq, a_b.seq, b_a.seq, config);
}


PWIZ_API_DECL
void diff(const Peptide& a,
          const Peptide& b,
          Peptide& a_b,
          Peptide& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const IdentifiableParamContainer&>(a), b, a_b, b_a, config);
    QUICKCHECK(); // in case we just want to know that they differ, not how they differ
    diff(a.peptideSequence, b.peptideSequence, a_b.peptideSequence, b_a.peptideSequence, config);
    vector_diff_deep(a.modification, b.modification, a_b.modification, b_a.modification, config);
    vector_diff_deep(a.substitutionModification, b.substitutionModification,
                     a_b.substitutionModification,b_a.substitutionModification, config);
}


PWIZ_API_DECL
void diff(const Modification& a,
          const Modification& b,
          Modification& a_b,
          Modification& b_a,
          const DiffConfig& config)
{
    diff_integral(a.location, b.location, a_b.location, b_a.location, config);
    vector_diff(a.residues, b.residues, a_b.residues, b_a.residues);
    diff_floating(a.avgMassDelta, b.avgMassDelta, a_b.avgMassDelta, b_a.avgMassDelta, config);
    diff_floating(a.monoisotopicMassDelta, b.monoisotopicMassDelta, a_b.monoisotopicMassDelta, b_a.monoisotopicMassDelta, config);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const SubstitutionModification& a,
          const SubstitutionModification& b,
          SubstitutionModification& a_b,
          SubstitutionModification& b_a,
          const DiffConfig& config)
{
    diff_char(a.originalResidue, b.originalResidue, a_b.originalResidue, b_a.originalResidue);
    diff_char(a.replacementResidue, b.replacementResidue, a_b.replacementResidue, b_a.replacementResidue);
    diff_integral(a.location, b.location, a_b.location, b_a.location, config);
    diff_floating(a.avgMassDelta, b.avgMassDelta,
                  a_b.avgMassDelta, b_a.avgMassDelta, config);
    diff_floating(a.monoisotopicMassDelta, b.monoisotopicMassDelta,
                  a_b.monoisotopicMassDelta, b_a.monoisotopicMassDelta, config);
}


PWIZ_API_DECL
void diff(const SequenceCollection& a,
          const SequenceCollection& b,
          SequenceCollection& a_b,
          SequenceCollection& b_a,
          const DiffConfig& config)
{
    vector_diff_deep(a.dbSequences, b.dbSequences, a_b.dbSequences, b_a.dbSequences, config);
    vector_diff_deep(a.peptides, b.peptides, a_b.peptides, b_a.peptides, config);
    vector_diff_deep(a.peptideEvidence, b.peptideEvidence, a_b.peptideEvidence, b_a.peptideEvidence, config);
}


PWIZ_API_DECL
void diff(const Sample& a,
          const Sample& b,
          Sample& a_b,
          Sample& b_a,
          const DiffConfig& config)
{
    vector_diff_deep(a.contactRole, b.contactRole, a_b.contactRole, b_a.contactRole, config);
    vector_diff_deep(a.subSamples, b.subSamples, a_b.subSamples, b_a.subSamples, config);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const AnalysisSampleCollection& a,
          const AnalysisSampleCollection& b,
          AnalysisSampleCollection& a_b,
          AnalysisSampleCollection& b_a,
          const DiffConfig& config)
{
    vector_diff_deep(a.samples, b.samples, a_b.samples, b_a.samples, config);
}


PWIZ_API_DECL
void diff(const Provider& a,
          const Provider& b,
          Provider& a_b,
          Provider& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const Identifiable&>(a), b, a_b, b_a, config);
    QUICKCHECK(); // in case we just want to know that they differ, not how they differ
    ptr_diff(a.contactRolePtr, b.contactRolePtr, a_b.contactRolePtr, b_a.contactRolePtr, config);
    ptr_diff(a.analysisSoftwarePtr, b.analysisSoftwarePtr,
             a_b.analysisSoftwarePtr, b_a.analysisSoftwarePtr, config);
}


PWIZ_API_DECL
void diff(const ContactRole& a,
          const ContactRole& b,
          ContactRole& a_b,
          ContactRole& b_a,
          const DiffConfig& config)
{
    diff(a.contactPtr, b.contactPtr, a_b.contactPtr, b_a.contactPtr, config);
    diff(static_cast<const CVParam&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const AnalysisSoftware& a,
          const AnalysisSoftware& b,
          AnalysisSoftware& a_b,
          AnalysisSoftware& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const Identifiable&>(a), b, a_b, b_a, config);
    QUICKCHECK(); // in case we just want to know that they differ, not how they differ
	if (!config.ignoreVersions)
        diff(a.version, b.version, a_b.version, b_a.version, config);
    ptr_diff(a.contactRolePtr, b.contactRolePtr, a_b.contactRolePtr, b_a.contactRolePtr, config);
    diff(a.softwareName, b.softwareName, a_b.softwareName, b_a.softwareName, config);
    diff(a.URI, b.URI, a_b.URI, b_a.URI, config);
    diff(a.customizations, b.customizations, a_b.customizations, b_a.customizations, config);
}


PWIZ_API_DECL
void diff(const IdentData& a, 
          const IdentData& b, 
          IdentData& a_b, 
          IdentData& b_a,
          const DiffConfig& config)
{
    string a_b_version, b_a_version;

    // Attributes
    diff(static_cast<const Identifiable&>(a), b, a_b, b_a, config);
    QUICKCHECK(); // in case we just want to know that they differ, not how they differ
	if (!config.ignoreVersions)
        diff(a.version(), b.version(), a_b_version, b_a_version, config);
    diff(a.creationDate, b.creationDate, a_b.creationDate, b_a.creationDate, config);

    // Elements
    vector_diff_diff(a.cvs, b.cvs, a_b.cvs, b_a.cvs, config);
    vector_diff_deep(a.analysisSoftwareList, b.analysisSoftwareList, a_b.analysisSoftwareList, b_a.analysisSoftwareList, config);
    diff(a.provider, b.provider, a_b.provider, b_a.provider, config);
    vector_diff_deep(a.auditCollection, b.auditCollection, a_b.auditCollection, b_a.auditCollection, config);
    diff(a.analysisSampleCollection, b.analysisSampleCollection,
         a_b.analysisSampleCollection, b_a.analysisSampleCollection, config);
    diff(a.sequenceCollection, b.sequenceCollection,
         a_b.sequenceCollection, b_a.sequenceCollection, config);
    diff(a.analysisCollection, b.analysisCollection, a_b.analysisCollection, b_a.analysisCollection, config);
    diff(a.analysisProtocolCollection, b.analysisProtocolCollection, a_b.analysisProtocolCollection, b_a.analysisProtocolCollection, config);
    diff(a.dataCollection, b.dataCollection, a_b.dataCollection, b_a.dataCollection, config);
    vector_diff_deep(a.bibliographicReference, b.bibliographicReference, a_b.bibliographicReference, b_a.bibliographicReference, config);
    

    if (!a_b_version.empty() || !b_a_version.empty())
    {
        a_b.name = a.name + " (" + a_b_version + ")";
        b_a.name = b.name + " (" + b_a_version + ")";
    }

    // provide names for context
    //if (!a_b.empty() && a_b.name.empty()) a_b.name = a.name; 
    //if (!b_a.empty() && b_a.name.empty()) b_a.name = b.name; 
}


PWIZ_API_DECL
void diff(const Identifiable& a,
          const Identifiable& b,
          Identifiable& a_b,
          Identifiable& b_a,
          const DiffConfig& config)
{
    diff_ids(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.name, b.name, a_b.name, b_a.name, config);
}


PWIZ_API_DECL
void diff(const IdentifiableParamContainer& a,
          const IdentifiableParamContainer& b,
          IdentifiableParamContainer& a_b,
          IdentifiableParamContainer& b_a,
          const DiffConfig& config)
{
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.name, b.name, a_b.name, b_a.name, config);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}

} // namespace diff_impl
} // namespace data


namespace identdata {


PWIZ_API_DECL
std::ostream& operator<<(std::ostream& os, const data::Diff<IdentData, DiffConfig>& diff)
{
    TextWriter write(os,1);

    if(!diff.a_b.empty() || !diff.b_a.empty())
    {
        os<<"+\n";
        write(diff.a_b);
        os<<"-\n";
        write(diff.b_a);
    }

    return os;
}

    
} // namespace identdata
} // namespace pwiz
