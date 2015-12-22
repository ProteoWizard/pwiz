#include "BlibException.h"
#include "mzxmlParser.h"
#include "Verbosity.h"

#include <cstdlib>

using namespace BiblioSpec;
using namespace pwiz::msdata;
using namespace std;

MzXMLParser::MzXMLParser()
    : file_(NULL), discardCount_(0), currentSpectrum_(NULL) {
}

MzXMLParser::~MzXMLParser() {
    delete file_;
    delete currentSpectrum_;
}

void MzXMLParser::startElement(const XML_Char *el, const XML_Char **attr) {
    if (state_.empty()) {
        if (isIElement("msRun", el)) {
            state_.push(MS_RUN_STATE);
        }
        return;
    }

    switch (state_.top()) {
    case MS_RUN_STATE:
        if (isIElement("scan", el)) {
            state_.push(SCAN_STATE);
            currentSpectrum_ = startSpectrum(attr);
        }
        break;
    case SCAN_STATE:
        if (isIElement("scan", el)) {
            abortSpectrum();
            state_.push(SCAN_STATE);
            currentSpectrum_ = startSpectrum(attr);
        } else if (isIElement("precursorMz", el)) {
            state_.push(PRECURSOR_MZ_STATE);
            charBuf_.clear();
        } else if (isIElement("peaks", el)) {
            state_.push(PEAKS_STATE);
            charBuf_.clear();

            string contentType(getAttrValue("contentType", attr));
            if (!contentType.empty() && contentType != "m/z-int") {
                Verbosity::warn("Unsupported content type for peaks: must be m/z-int (spectrum %d)",
                                currentSpectrum_->id);
                abortSpectrum();
                break;
            }

            string precision = getAttrValue("precision", attr);
            string byteOrder = getAttrValue("byteOrder", attr);
            string compression = getAttrValue("compressionType", attr);

            bdeConfig_.precision = (precision == "64")
                ? BinaryDataEncoder::Precision_64
                : BinaryDataEncoder::Precision_32;
            bdeConfig_.byteOrder = (!byteOrder.empty() && byteOrder != "network")
                ? BinaryDataEncoder::ByteOrder_LittleEndian
                : BinaryDataEncoder::ByteOrder_BigEndian;
            bdeConfig_.compression = (compression == "zlib")
                ? BinaryDataEncoder::Compression_Zlib
                : BinaryDataEncoder::Compression_None;
        }
        break;
    }
}

void MzXMLParser::endElement(const XML_Char *el) {
    if (state_.empty())
        return;

    switch (state_.top()) {
    case MS_RUN_STATE:
        if (isIElement("msRun", el)) {
            state_.pop();
        }
        break;
    case SCAN_STATE:
        if (isIElement("scan", el) && currentSpectrum_) {
            state_.pop();

            spectra_.push_back(currentSpectrum_);
            currentSpectrum_ = NULL;
        }
        break;
    case PRECURSOR_MZ_STATE:
        if (isIElement("precursorMz", el)) {
            state_.pop();

            currentSpectrum_->mz = atof(charBuf_.c_str());
        }
        break;
    case PEAKS_STATE:
        if (isIElement("peaks", el)) {
            state_.pop();

            BinaryDataEncoder encoder(bdeConfig_);
            vector<double> decoded;
            encoder.decode(charBuf_, decoded);
            size_t numDecodedPeaks = decoded.size() / 2;

            if (decoded.size() % 2 == 0) {
                if (currentSpectrum_->numPeaks < 0) {
                    currentSpectrum_->numPeaks = numDecodedPeaks;
                } else if (currentSpectrum_->numPeaks != numDecodedPeaks) {
                    Verbosity::warn("Expected %d peaks for spectrum %d, but found %d",
                                    currentSpectrum_->numPeaks, currentSpectrum_->id, decoded.size() / 2);
                    abortSpectrum();
                    break;
                }
            } else {
                Verbosity::warn("Error reading peaks for spectrum %d", currentSpectrum_->id);
                abortSpectrum();
                break;
            }

            currentSpectrum_->mzs = new double[numDecodedPeaks];
            currentSpectrum_->intensities = new float[numDecodedPeaks];
            for (size_t i = 0, j = 0; j < decoded.size(); i++, j += 2) {
                currentSpectrum_->mzs[i] = decoded[j];
                currentSpectrum_->intensities[i] = (float)decoded[j + 1];
            }
        }
        break;
    }
}

void MzXMLParser::characters(const XML_Char *s, int len) {
    if (state_.empty())
        return;

    switch (state_.top()) {
    case PRECURSOR_MZ_STATE:
    case PEAKS_STATE:
        charBuf_.append(s, len);
        break;
    }
}

SpecData* MzXMLParser::startSpectrum(const XML_Char **attr) {
    SpecData* spectrum = new SpecData();
    spectrum->id = getIntRequiredAttrValue("num", attr);
    string rtStr(getAttrValue("retentionTime", attr));
    double rt;
    if (sscanf(rtStr.c_str(), "PT%lfS", &rt) > 0) {
        spectrum->retentionTime = rt / 60;
    } else if (!rtStr.empty()) {
        spectrum->retentionTime = atof(rtStr.c_str()) / 60;
    }
    string numPeaksStr(getAttrValue("peaksCount", attr));
    if (!numPeaksStr.empty()) {
        spectrum->numPeaks = atoi(numPeaksStr.c_str());
    }
    return spectrum;
}

void MzXMLParser::abortSpectrum() {
    if (!currentSpectrum_) {
        Verbosity::warn("not aborting non-existent spectrum");
        return;
    }
    delete currentSpectrum_;
    currentSpectrum_ = NULL;
    while (!state_.empty() && state_.top() != SCAN_STATE) {
        state_.pop();
    }
    if (!state_.empty() && state_.top() == SCAN_STATE) {
        state_.pop();
    } else if (state_.empty()) {
        Verbosity::error("abortSpectrum called with no scan on stack");
    }
}

void MzXMLParser::parseChunk() {
    if (!file_ || file_->bad()) {
        throw BlibException(true, "MzXMLParser: filestream error");
    }

    char buf[65536];
    file_->read(buf, sizeof(buf));
    if (!XML_Parse(m_parser_, buf, file_->gcount(), false)) {
        string error = generateError(getParserError());
        throw BlibException(true, error.c_str());
    }

    if (file_->eof()) {
        delete file_;
        file_ = NULL;
        if (!XML_Parse(m_parser_, buf, 0, true)) {
            string error = generateError(getParserError());
            throw BlibException(true, error.c_str());
        }
    }
}

bool MzXMLParser::popSpectrum(SpecData* dst) {
    if (spectra_.empty()) {
        return false;
    }
    SpecData* src = spectra_.front();
    if (dst) {
        *dst = *src;
    }
    delete src;
    spectra_.erase(spectra_.begin());
    ++discardCount_;
    return true;
}

void MzXMLParser::openFile(const char* filename, bool mzSort) {
    setFileName(filename);

    // Reset variables
    delete file_;
    XML_ParserFree(m_parser_);
    initParser();

    while (!state_.empty()) {
        state_.pop();
    }
    charBuf_.clear();
    discardCount_ = 0;
    spectra_.clear();
    if (currentSpectrum_) {
        delete currentSpectrum_;
        currentSpectrum_ = NULL;
    }

    file_ = new ifstream(filename);
    if (!file_->good()) {
        throw BlibException(true, "MzXMLParser: Failed to open file");
    }
}

bool MzXMLParser::getSpectrum(int identifier, SpecData& returnData, SPEC_ID_TYPE findBy, bool getPeaks) {
    switch (findBy) {
    case SCAN_NUM_ID:
        while (!spectra_.empty()) {
            int current = spectra_.front()->id;
            if (identifier < current) {
                // Already been discarded
                return false;
            } else if (identifier == current) {
                popSpectrum(&returnData);
                return true;
            } else {
                popSpectrum();
            }
        }
        break;
    case INDEX_ID:
        if (identifier < discardCount_) {
            // Already been discarded
            return false;
        } else if (discardCount_ <= identifier && identifier <= discardCount_ + (int)spectra_.size() - 1) {
            // In current range
            while (discardCount_ < identifier) {
                popSpectrum();
            }
            popSpectrum(&returnData);
            return true;
        }
        while (popSpectrum());
        break;
    case NAME_ID:
        return getSpectrum("", returnData, getPeaks);
    }
    if (!file_) {
        // End of file reached and spectrum not found
        return false;
    }
    // Parse more of the file and try again
    parseChunk();
    return getSpectrum(identifier, returnData, findBy, getPeaks);
}

bool MzXMLParser::getSpectrum(string identifier, SpecData& returnData, bool getPeaks) {
    throw BlibException(false, "Lookup by scan name is not implemented");
}

bool MzXMLParser::getNextSpectrum(SpecData& returnData, bool getPeaks) {
    while (spectra_.empty()) {
        if (!file_) {
            return false;
        }
        parseChunk();
    }
    popSpectrum(&returnData);
    return true;
}
