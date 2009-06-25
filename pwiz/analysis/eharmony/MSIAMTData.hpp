///
/// MSIAMTData.hpp
///

/// Not a verbatim reader/writer; mainly follows msinspect/amt schema but gets only the necessary elements, writing skipped element names to the console for monitoring , writes only elements necessary for eharmony

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include <vector>

namespace pwiz{
namespace eharmony{

using namespace pwiz::minimxml;

struct Observation
{
    double observedHydrophobicity;
    double peptideProphet;
    size_t runID;
    double timeInRun;
    size_t spectralCount;

    void read(std::istream& is);
    void write(XMLWriter& writer) const;

    Observation() : observedHydrophobicity(0), peptideProphet(0), runID(0), timeInRun(0), spectralCount(0){}

};

struct ModificationStateEntry
{
    std::string modifiedSequence;
    double modifiedMass;
    double medianObservedHydrophobicity;
    double medianPeptideProphet;
    
    std::vector<Observation> observations;

    void read(std::istream& is);
    void write(XMLWriter& writer) const;

    ModificationStateEntry() : modifiedSequence(""), modifiedMass(0), medianObservedHydrophobicity(0), medianPeptideProphet(0){}

};

struct PeptideEntry
{
    std::string peptideSequence;
    double calculatedHydrophobicity;
    double medianObservedHydrophobicity;
    double medianPeptideProphet;

    std::vector<ModificationStateEntry> modificationStateEntries;

    void read(std::istream& is);
    void write(XMLWriter& writer) const;

    PeptideEntry() : peptideSequence(""), calculatedHydrophobicity(0), medianObservedHydrophobicity(0), medianPeptideProphet(0){}

};

struct MSIAMTData // does not contain all elements of the <amt:amt_database ... > tag but serves as a container for reading in peptide entries
{
    std::vector<PeptideEntry> peptideEntries;
    
    void read(std::istream& is);
    void write(XMLWriter& writer) const;

    MSIAMTData(){}

};

} // eharmony
} // pwiz
