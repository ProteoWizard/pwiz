#ifndef MZXML_PARSER_H
#define MZXML_PARSER_H

#include "pwiz/data/msdata/MSDataFile.hpp"
#include "SpecFileReader.h"

#include <fstream>
#include <stack>
#include <string>
#include <vector>

namespace BiblioSpec {

class MzXMLParser : public SpecFileReader {
public:
    MzXMLParser();
    virtual ~MzXMLParser();

    // SpecFileReader functions
    virtual void openFile(const char*, bool mzSort = false);
    virtual void setIdType(SPEC_ID_TYPE type) {}
    virtual bool getSpectrum(int identifier, SpecData& returnData, SPEC_ID_TYPE findBy, bool getPeaks = true);
    virtual bool getSpectrum(string identifier, SpecData& returnData, bool getPeaks = true);
    virtual bool getNextSpectrum(SpecData& returnData, bool getPeaks = true);

protected:
    pwiz::msdata::MSDataPtr msd_;
    int lastIndex_;
    std::string filename_;
};

}

#endif
