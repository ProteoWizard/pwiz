//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

#include "MSIAMTData.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
using namespace eharmony;

void Observation::write(XMLWriter& writer) const
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("observed_hydrophobicity", boost::lexical_cast<string>(observedHydrophobicity)));
    attributes.push_back(make_pair("peptide_prophet", boost::lexical_cast<string>(peptideProphet)));
    attributes.push_back(make_pair("run_id", boost::lexical_cast<string>(runID)));
    attributes.push_back(make_pair("time_in_run", boost::lexical_cast<string>(timeInRun)));
    attributes.push_back(make_pair("spectral_count", boost::lexical_cast<string>(spectralCount)));

    writer.startElement("observation", attributes, XMLWriter::EmptyElement);

}

struct HandlerObservation : public SAXParser::Handler
{
    Observation* _observation;
    HandlerObservation(Observation* observation = 0) : _observation(observation){}
    
    virtual Status startElement(const string& name,
                                const Attributes& attributes,
                                stream_offset position)

    {
        if (name == "observation")            
            {
                getAttribute(attributes, "observed_hydrophobicity", _observation->observedHydrophobicity);
                getAttribute(attributes, "peptide_prophet", _observation->peptideProphet);
                getAttribute(attributes, "run_id", _observation->runID);
                getAttribute(attributes, "time_in_run", _observation->timeInRun);
                getAttribute(attributes, "spectral_count", _observation->spectralCount);
         
                return Status::Ok;

            }

        else 
            {
                cerr << ("[HandlerObservation]: unexpected element " + name).c_str() << endl;
                
                return Status::Done;

            }
            
    }

};

void Observation::read(istream& is)
{
    HandlerObservation handlerObservation(this);
    parse(is, handlerObservation);

}

void ModificationStateEntry::write(XMLWriter& writer) const
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("modified_sequence", boost::lexical_cast<string>(modifiedSequence)));
    attributes.push_back(make_pair("modified_mass", boost::lexical_cast<string>(modifiedMass)));
    attributes.push_back(make_pair("median_observed_hydrophobicity", boost::lexical_cast<string>(medianObservedHydrophobicity)));
    attributes.push_back(make_pair("median_peptide_prophet", boost::lexical_cast<string>(medianPeptideProphet)));
    
    writer.startElement("modification_state_entry", attributes);

    vector<Observation>::const_iterator it = observations.begin();
    for(; it != observations.end(); ++it) it->write(writer);

    writer.endElement();

}

struct HandlerModificationStateEntry : public SAXParser::Handler
{
    ModificationStateEntry* _modificationStateEntry;
    HandlerModificationStateEntry(ModificationStateEntry* modificationStateEntry = 0) : _modificationStateEntry(modificationStateEntry){}

    virtual Status startElement(const string& name,
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "modification_state_entry")
            {
                getAttribute(attributes, "modified_sequence", _modificationStateEntry->modifiedSequence);
                getAttribute(attributes, "modified_mass", _modificationStateEntry->modifiedMass);
                getAttribute(attributes, "median_observed_hydrophobicity", _modificationStateEntry->medianObservedHydrophobicity);
                getAttribute(attributes, "median_peptide_prophet", _modificationStateEntry->medianPeptideProphet);
                
                return Status::Ok;

            }

        else if (name == "observation")
            {
                _modificationStateEntry->observations.push_back(Observation());
                _handlerObservation._observation = &_modificationStateEntry->observations.back();
                return Status(Status::Delegate, &_handlerObservation);

            }

        else
            {
                cerr << ("[HandlerModificationStateEntry] Unexpected element : " + name).c_str() << endl;
                return Status::Ok;
        
            }

    }

    HandlerObservation _handlerObservation;

};

void ModificationStateEntry::read(istream& is)
{
    HandlerModificationStateEntry handler(this);
    parse(is,handler);

}

void PeptideEntry::write(XMLWriter& writer) const
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("peptide_seqence", peptideSequence));
    attributes.push_back(make_pair("calculated_hydrophobicity", boost::lexical_cast<string>(calculatedHydrophobicity)));
    attributes.push_back(make_pair("median_observed_hydrophobicity", boost::lexical_cast<string>(medianObservedHydrophobicity)));
    attributes.push_back(make_pair("median_peptide_prophet", boost::lexical_cast<string>(medianPeptideProphet)));
    
    writer.startElement("peptide_entry", attributes);
    vector<ModificationStateEntry>::const_iterator it = modificationStateEntries.begin();
    for(; it != modificationStateEntries.end(); ++it) it->write(writer);
    writer.endElement();

}

struct HandlerPeptideEntry : public SAXParser::Handler
{
    PeptideEntry* _peptideEntry;
    HandlerPeptideEntry(PeptideEntry* peptideEntry = 0) : _peptideEntry(peptideEntry){}

    virtual Status startElement(const string& name,
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "peptide_entry")
            {
                getAttribute(attributes, "peptide_sequence", _peptideEntry->peptideSequence);
                getAttribute(attributes, "calculated_hydrophobicity", _peptideEntry->calculatedHydrophobicity);
                getAttribute(attributes, "median_observed_hydrophobicity", _peptideEntry->medianObservedHydrophobicity);
                getAttribute(attributes, "median_peptide_prophet", _peptideEntry->medianPeptideProphet);
                return Status::Ok;

            }

        else if (name == "modification_state_entry")
            {
                _peptideEntry->modificationStateEntries.push_back(ModificationStateEntry());
                _handlerModificationStateEntry._modificationStateEntry = &_peptideEntry->modificationStateEntries.back();
                return Status(Status::Delegate, &_handlerModificationStateEntry);

            }

        else
            {
                cerr << ("[HandlerPeptideEntry] Unexpected element: " + name).c_str();
                return Status::Done;
        
            }

        
    }

    HandlerModificationStateEntry _handlerModificationStateEntry;
};

void PeptideEntry::read(istream& is)
{
    HandlerPeptideEntry handler(this);
    parse(is,handler);

}

void MSIAMTData::write(XMLWriter& writer) const
{
    XMLWriter::Attributes attributes;

    writer.startElement("amt:amt_database", attributes);
    vector<PeptideEntry>::const_iterator it = peptideEntries.begin();
    for(; it!= peptideEntries.end(); ++it) it->write(writer);
    writer.endElement();

}

struct HandlerMSIAMTData : public SAXParser::Handler
{
    MSIAMTData* _msiAmtData;
    HandlerMSIAMTData(MSIAMTData* msiAmtData = 0) : _msiAmtData(msiAmtData){}
    
    virtual Status startElement(const string& name,
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "amt:amt_database")
            {
                return Status::Ok;
            }

        else if (name == "peptide_entry")
            {
                _msiAmtData->peptideEntries.push_back(PeptideEntry());
                _handlerPeptideEntry._peptideEntry = &_msiAmtData->peptideEntries.back();
                return Status(Status::Delegate, &_handlerPeptideEntry);
            }

        else 
            {
                cerr << ("[HandlerMSIAMTData] Unexpected element : " + name).c_str() << endl;
                return Status::Ok; // don't throw on skipped elements, but do report
            }
    }

    HandlerPeptideEntry _handlerPeptideEntry;

};

void MSIAMTData::read(istream& is)
{
    HandlerMSIAMTData handler(this);
    parse(is, handler);

}
