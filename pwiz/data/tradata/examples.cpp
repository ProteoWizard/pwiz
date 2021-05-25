//
// $Id$ 
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

#include "examples.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace tradata {
namespace examples {




PWIZ_API_DECL void initializeTiny(TraData& td)
{
    //td.id = "urn:lsid:psidev.info:mzML.instanceDocuments.tiny.pwiz";
    td.cvs = defaultCVList();


    Transition transition1;
    transition1.id = "Transition1";
    transition1.precursor.set(MS_isolation_window_target_m_z, 456.78, MS_m_z);
    transition1.product.set(MS_isolation_window_target_m_z, 678.90, MS_m_z);

    Transition transition2;
    transition2.id = "Transition2";
    transition2.precursor.set(MS_isolation_window_target_m_z, 456.78, MS_m_z);
    transition2.product.set(MS_isolation_window_target_m_z, 789.00, MS_m_z);

    td.transitions.push_back(transition1);
    td.transitions.push_back(transition2);


    Target target1;
    target1.id = "Target1";
    target1.precursor.set(MS_isolation_window_target_m_z, 456.78, MS_m_z);

    Target target2;
    target2.id = "Target2";
    target2.precursor.set(MS_isolation_window_target_m_z, 567.89, MS_m_z);

    td.targets.set(MS_includes_supersede_excludes);
    td.targets.targetExcludeList.push_back(target1);
    td.targets.targetIncludeList.push_back(target2);

} // initializeTiny()


PWIZ_API_DECL void addMIAPEExampleMetadata(TraData& td)
{    
    ContactPtr contactPtr(new Contact("JQP"));
    contactPtr->set(MS_contact_name, "John Q. Public");
    contactPtr->set(MS_contact_affiliation, "Department of Redundancy Department");
    contactPtr->set(MS_contact_address, "1600 Pennsylvania Ave.");
    td.contactPtrs.push_back(contactPtr);


    Publication publication;
    publication.id = "Al_et_al";
    publication.set(MS_PubMed_identifier, 123456);
    td.publications.push_back(publication);


    InstrumentPtr lcqInstrumentPtr(new Instrument("LCQ"));
    lcqInstrumentPtr->set(MS_LCQ_Deca);
    lcqInstrumentPtr->set(MS_instrument_serial_number,"23433");
    td.instrumentPtrs.push_back(lcqInstrumentPtr);

    InstrumentPtr qtrapInstrumentPtr(new Instrument("QTRAP"));
    qtrapInstrumentPtr->set(MS_4000_QTRAP);
    td.instrumentPtrs.push_back(qtrapInstrumentPtr);


    SoftwarePtr softwarePtr(new Software("Xcalibur"));
    softwarePtr->set(MS_Xcalibur);
    softwarePtr->version = "2.0.5";

    SoftwarePtr softwareMaRiMba(new Software("MaRiMba"));
    softwareMaRiMba->set(MS_MaRiMba);
    softwareMaRiMba->version = "0.5";
     
    SoftwarePtr softwarepwiz(new Software("pwiz"));
    softwarepwiz->set(MS_pwiz);
    softwarepwiz->version = "1.0";

    td.softwarePtrs.push_back(softwareMaRiMba);
    td.softwarePtrs.push_back(softwarepwiz);
    td.softwarePtrs.push_back(softwarePtr);


    ProteinPtr proteinPtr(new Protein("Q123"));
    proteinPtr->set(MS_protein_accession, "Q123");
    proteinPtr->sequence = "ABCD";
    proteinPtr->set(MS_protein_name, "A short protein.");
    proteinPtr->set(MS_molecular_mass, 12345, UO_dalton);
    td.proteinPtrs.push_back(proteinPtr);


    PeptidePtr peptide1Ptr(new Peptide("Pep1"));
    peptide1Ptr->sequence = "AB";
    peptide1Ptr->set(MS_theoretical_mass, 1234, UO_dalton);
    peptide1Ptr->proteinPtrs.push_back(proteinPtr);
    peptide1Ptr->modifications.push_back(Modification());
    peptide1Ptr->modifications.back().set(UNIMOD_Methylmalonylation);
    peptide1Ptr->modifications.back().location = 1;
    peptide1Ptr->modifications.back().monoisotopicMassDelta = 123;
    peptide1Ptr->retentionTimes.push_back(RetentionTime());
    peptide1Ptr->retentionTimes.back().set(MS_predicted_retention_time, 42, UO_minute);
    peptide1Ptr->retentionTimes.back().softwarePtr = softwareMaRiMba;
    peptide1Ptr->evidence.set(MS_confident_peptide_sequence_number, 6);
    td.peptidePtrs.push_back(peptide1Ptr);


    CompoundPtr compound1Ptr(new Compound("Cmp1"));
    compound1Ptr->set(MS_theoretical_mass, 1234, UO_dalton);
    compound1Ptr->retentionTimes.push_back(RetentionTime());
    compound1Ptr->retentionTimes.back().set(MS_predicted_retention_time, 42, UO_minute);
    compound1Ptr->retentionTimes.back().softwarePtr = softwareMaRiMba;
    td.compoundPtrs.push_back(compound1Ptr);


    Interpretation interpretation1;
    interpretation1.set(MS_frag__y_ion);
    interpretation1.set(MS_product_ion_series_ordinal, 8);
    interpretation1.set(MS_product_ion_m_z_delta, 0.03);

    Interpretation interpretation2;
    interpretation2.set(MS_frag__b_ion___H2O);
    interpretation2.set(MS_product_ion_series_ordinal, 9);
    interpretation2.set(MS_product_ion_m_z_delta, -0.43);


    Validation qtrapValidation;
    qtrapValidation.set(MS_transition_optimized_on_specified_instrument);
    qtrapValidation.set(MS_4000_QTRAP);
    qtrapValidation.set(MS_peak_intensity, 4072, MS_percent_of_base_peak_times_100);
    qtrapValidation.set(MS_peak_intensity_rank, 2);
    qtrapValidation.set(MS_peak_targeting_suitability_rank, 1);


    Configuration qtrapConfiguration;
    qtrapConfiguration.instrumentPtr = qtrapInstrumentPtr;
    qtrapConfiguration.contactPtr = contactPtr;
    qtrapConfiguration.set(MS_dwell_time, 0.12, UO_second);
    qtrapConfiguration.set(MS_collision_gas, "argon");
    qtrapConfiguration.set(MS_collision_gas_pressure, 12, UO_pascal);
    qtrapConfiguration.set(MS_collision_energy, 26, UO_electronvolt);
    qtrapConfiguration.set(MS_cone_voltage, 1200, UO_volt);
    qtrapConfiguration.set(MS_interchannel_delay, 0.1, UO_second);
    qtrapConfiguration.set(MS_tube_lens_voltage, 23, UO_volt);
    qtrapConfiguration.validations.push_back(qtrapValidation);


    Transition& tra0 = td.transitions[0];
    tra0.peptidePtr = peptide1Ptr;
    tra0.precursor.set(MS_charge_state, 2);
    tra0.product.set(MS_charge_state, 1);
    tra0.prediction.softwarePtr = softwareMaRiMba;
    tra0.prediction.set(MS_transition_purported_from_an_MS_MS_spectrum_on_a_different__specified_instrument);
    tra0.prediction.set(MS_linear_ion_trap);
    tra0.prediction.set(MS_peak_intensity, 10000, UO_electronvolt);
    tra0.prediction.set(MS_peak_intensity_rank, 1);
    tra0.prediction.set(MS_peak_targeting_suitability_rank, 1);
    tra0.retentionTime.set(MS_local_retention_time, 40.02, UO_minute);
    tra0.retentionTime.set(MS_retention_time_window_lower_offset, 3, UO_minute);
    tra0.retentionTime.set(MS_retention_time_window_upper_offset, 3, UO_minute);
    tra0.configurationList.push_back(qtrapConfiguration);



    Transition& tra1 = td.transitions[1];
    tra1.compoundPtr = compound1Ptr;
    tra1.precursor.set(MS_charge_state, 2);
    tra1.product.set(MS_charge_state, 1);


    Configuration lcqConfiguration;
    lcqConfiguration.instrumentPtr = lcqInstrumentPtr;
    lcqConfiguration.contactPtr = contactPtr;
    lcqConfiguration.set(MS_collision_energy, 42, UO_electronvolt);


    Target& tar0 = td.targets.targetIncludeList[0];
    tar0.compoundPtr = compound1Ptr;
    tar0.precursor.set(MS_charge_state, 2);
    tar0.retentionTime.set(MS_local_retention_time, 12.3, UO_minute);
    tar0.retentionTime.set(MS_retention_time_window_lower_offset, 1, UO_minute);
    tar0.retentionTime.set(MS_retention_time_window_upper_offset, 2, UO_minute);
    tar0.configurationList.push_back(lcqConfiguration);

    
    td.targets.targetExcludeList[0].peptidePtr = peptide1Ptr;
    td.targets.targetExcludeList[0].precursor.set(MS_charge_state, 2);

} // addMIAPEExampleMetadata()


} // namespace examples
} // namespace tdata
} // namespace pwiz
