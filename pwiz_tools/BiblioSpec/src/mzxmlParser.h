#ifndef MZXML_PARSER_H
#define MZXML_PARSER_H

#include "pwiz/data/msdata/BinaryDataEncoder.hpp"
#include "saxhandler.h"
#include "SpecFileReader.h"

#include <fstream>
#include <stack>
#include <string>
#include <vector>

namespace BiblioSpec {

class MzXMLParser : public SAXHandler, public SpecFileReader {
public:
    MzXMLParser();
    virtual ~MzXMLParser();

    // SAXHandler functions
    virtual void startElement(const XML_Char *el, const XML_Char **attr);
    virtual void endElement(const XML_Char *el);
    virtual void characters(const XML_Char *s, int len);

    // SpecFileReader functions
    virtual void openFile(const char*, bool mzSort = false);
    virtual void setIdType(SPEC_ID_TYPE type) {}
    virtual bool getSpectrum(int identifier, SpecData& returnData, SPEC_ID_TYPE findBy, bool getPeaks = true);
    virtual bool getSpectrum(string identifier, SpecData& returnData, bool getPeaks = true);
    virtual bool getNextSpectrum(SpecData& returnData, bool getPeaks = true);

protected:
    enum STATE {
        MS_RUN_STATE,
        SCAN_STATE,
        PRECURSOR_MZ_STATE,
        PEAKS_STATE
    };

    SpecData* startSpectrum(const XML_Char **attr);
    void abortSpectrum();
    void parseChunk();
    bool popSpectrum(SpecData* dst = NULL);

    std::ifstream* file_;
    std::stack<STATE> state_;
    std::string charBuf_;
    int discardCount_;
    std::vector<SpecData*> spectra_;
    SpecData* currentSpectrum_;
    pwiz::msdata::BinaryDataEncoder::Config bdeConfig_;
};

}

#endif
