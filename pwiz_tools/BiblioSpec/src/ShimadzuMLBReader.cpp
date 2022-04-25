//
// $Id: ShimadzuMLBReader.cpp 9898 2016-07-13 22:38:39Z kaipot $
//
//
// Original author: Brian Pratt <bspratt@u.washington.edu>
// Based on msfReader by Kaipo Tamura <kaipot@u.washington.edu>
//
// Copyright 2016 University of Washington - Seattle, WA 98195
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

#include "ShimadzuMLBReader.h"

namespace BiblioSpec
{
    ShimadzuMLBReader::ShimadzuMLBReader(BlibBuilder& maker, 
                         const char* mlbFile, 
                         const ProgressIndicator* parent_progress)
        : BuildParser(maker, mlbFile, parent_progress),
        mlbName_(mlbFile), schemaVersion_(-1)
    {
        setSpecFileName(mlbFile, false);
        lookUpBy_ = INDEX_ID;
        // point to self as spec reader
        delete specReader_;
        specReader_ = this;
    }

    ShimadzuMLBReader::~ShimadzuMLBReader()
    {
        sqlite3_close(mlbFile_);

        specReader_ = NULL; // so the parent class doesn't try to delete itself
        // free spectra
        for (map<int, SpecData*>::iterator it = spectra_.begin(); it != spectra_.end(); ++it)
        {
            if (it->second != NULL)
            {
                delete it->second;
                it->second = NULL;
            }
        }
    }


    bool ShimadzuMLBReader::parseFile()
    {
        sqlite3_open(mlbName_, &mlbFile_);
        if (!mlbFile_)
        {
            throw BlibException(true, "Couldn't open '%s'.", mlbName_);
        }
        // Get the schema version
        sqlite3_stmt* statement = getStmt(
            "SELECT DBVer FROM PROPERTY");
        if (hasNext(statement))
        {
            string version = lexical_cast<string>(sqlite3_column_text(statement, 0));
            sqlite3_finalize(statement);
            vector<string> versionPieces;
            boost::split(versionPieces, version, boost::is_any_of("."));
            try {
                schemaVersion_ = lexical_cast<int>(versionPieces.front());
                Verbosity::debug("Schema version is %d (%s)", schemaVersion_, version.c_str());
            } catch (...) {
                Verbosity::error("Unknown schema version format: '%s'", version.c_str());
            }
        }
        if (schemaVersion_ < 1) {
            Verbosity::error("Could not determine schema version.");
        }
        readMSMSSP();
        // add psms by filename
        for (map< string, vector<PSM*> >::iterator iter = fileMap_.begin();
             iter != fileMap_.end();
             ++iter)
        {
            if (iter->second.size() > 0)
            {
                psms_.assign(iter->second.begin(), iter->second.end());
                setSpecFileName(iter->first, false);
                buildTables(UNKNOWN_SCORE_TYPE, iter->first);
            }
        }

        return true;
    }

    vector<PSM_SCORE_TYPE> ShimadzuMLBReader::getScoreTypes() {
        return vector<PSM_SCORE_TYPE>(1, UNKNOWN_SCORE_TYPE);
    }

    double ShimadzuMLBReader::ReadDoubleFromBuffer(const char * & buf)
    {
        double result;
        char *p = reinterpret_cast<char *>(&result);
        for (int n = sizeof(double); n--;)
            *p++ = *buf++;
        return result;
    }


    const char * ShimadzuMLBReader::getAdduct(int adductType, int& charge)
    {
        const char *precursorAdduct;
        switch (adductType) // Per email from Shimadzu's Yutaro Yamamura to Brian Pratt
        {
        case 0x1:
            precursorAdduct = "[M+H]";
            charge = 1;
            break;
        case 0x2:
            precursorAdduct = "[M+Na]";
            charge = 1;
            break;
        case 0x4:
            precursorAdduct = "[M+K]";
            charge = 1;
            break;
        case 0x8:
            precursorAdduct = "[M+NH4]";
            charge = 1;
            break;
        case 0x10: // "other(+)"
            precursorAdduct = "[M+]";
            charge = 1;
            break;
        case 0x20:
            precursorAdduct = "[M-H]";
            charge = 1;
            break;
        case 0x40:
            precursorAdduct = "[M+HCOO]";
            charge = 1;
            break;
        case 0x80:
            precursorAdduct = "[M+CH3COO]";
            charge = 1;
            break;
        case 0x100:
            precursorAdduct = "[M+Cl]";
            charge = 1;
            break;
        case 0x200: // "other(-)"
            precursorAdduct = "[M-]";
            charge = 1;
            break;
        default:
            precursorAdduct = NULL;
            charge = 0;
            break;
        }
        if (precursorAdduct == NULL)
        {
            throw BlibException(false, "Unknown adduct type");
        }
        return precursorAdduct;
    }

    void ShimadzuMLBReader::readMSMSSP()
    {
        int specCount =
            getRowCount("(SELECT DISTINCT SP_ID FROM MSMSSP WHERE MSStage IS 2)");
        Verbosity::status("Parsing %d spectra.", specCount);
        ProgressIndicator progress(specCount);
        sqlite3_stmt* statement = 
            getStmt(
                "SELECT SP_ID, RT, PrecursorMZ, SpecPeak, AdductType, CompForm, CompName, IUPACNo, CASNo, DataFilePath "
                "FROM MSMSSP "
                "WHERE SP_ID IN (SELECT DISTINCT SP_ID FROM MSMSSP WHERE MSStage IS 2)");

        // turn each row of returned table into a spectrum
        while (hasNext(statement))
        {
            SpecData* specData = new SpecData();
            int specId = sqlite3_column_int(statement, 0);
            specData->id = specId;
            specData->retentionTime = sqlite3_column_double(statement, 1) / 60000.0; // convert msec to minutes
            specData->mz = sqlite3_column_int(statement, 2);
            const char* spectrum_ptr = static_cast<const char*>(sqlite3_column_blob(statement, 3));
            size_t spectrumLen = sqlite3_column_bytes(statement, 3);
            specData->numPeaks = spectrumLen / (4 * sizeof(double));
            specData->mzs = new double[specData->numPeaks];
            specData->intensities = new float[specData->numPeaks];
            int confirm_nPeaks = ReadDoubleFromBuffer(spectrum_ptr)/4;
            if (confirm_nPeaks != specData->numPeaks)
            {
                throw BlibException(false, "Inconsistent peak count");
            }
            for (int n = 0; n <  specData->numPeaks; n++)
            {
                specData->mzs[n] = ReadDoubleFromBuffer(spectrum_ptr);
                specData->intensities[n] = ReadDoubleFromBuffer(spectrum_ptr);
                spectrum_ptr += 2 * sizeof(double);
            }
            int adductType = sqlite3_column_int(statement, 4);
            int charge;

            curPSM_ = new PSM();
            curPSM_->specKey = specId;
            curPSM_->specIndex = specId;
			curPSM_->smallMolMetadata.precursorAdduct = getAdduct(adductType, charge);
            curPSM_->charge = charge;
            curPSM_->smallMolMetadata.chemicalFormula = lexical_cast<string>(sqlite3_column_text(statement, 5));
            curPSM_->smallMolMetadata.moleculeName = lexical_cast<string>(sqlite3_column_text(statement, 6));
            curPSM_->smallMolMetadata.inchiKey = lexical_cast<string>(sqlite3_column_text(statement, 7));
            string cas = lexical_cast<string>(sqlite3_column_text(statement, 8));
            bal::replace_all(cas, " ", ""); // Example had random spaces in the CAS string
            if (!cas.empty())
            {
                curPSM_->smallMolMetadata.otherKeys = "cas:" + cas;
            }

            string datafilepath = lexical_cast<string>(sqlite3_column_text(statement, 9));
            if (datafilepath.empty())
                datafilepath = "unknown";
            fileMap_[datafilepath].push_back(curPSM_);

            // add spectrum to map
            spectra_[specId] = specData;
            spectraChargeStates_[specId] = charge;

            progress.increment();
        }

        Verbosity::debug("Map has %d spectra", spectra_.size());
    }

    /**
     * Prepares the given string as a SQLite query and verifies the return value.
     */
    sqlite3_stmt* ShimadzuMLBReader::getStmt(const string& query) const
    {
        return getStmt(mlbFile_, query);
    }
    
    sqlite3_stmt* ShimadzuMLBReader::getStmt(sqlite3* handle, const string& query) {
        sqlite3_stmt* statement;
        if (sqlite3_prepare(handle, query.c_str(), -1, &statement, NULL) != SQLITE_OK) {
            throw BlibException("Cannot prepare SQL statement: %s ", sqlite3_errmsg(handle));
        }
        return statement;
    }

    /**
     * Attempts to return the next row of the SQLite statement. Returns true on success.
     * Otherwise, finalize the statement and return false.
     */
    bool ShimadzuMLBReader::hasNext(sqlite3_stmt* statement)
    {
        if (sqlite3_step(statement) != SQLITE_ROW) {
            sqlite3_finalize(statement);
            return false;
        }
        return true;
    }

    /**
     * Returns the number of records in the SQLite table.
     */
    int ShimadzuMLBReader::getRowCount(string table) const
    {
        sqlite3_stmt* statement = getStmt("SELECT COUNT(*) FROM " + table);

        sqlite3_step(statement);
        int count = sqlite3_column_int(statement, 0);
        sqlite3_finalize(statement);

        return count;
    }

    // SpecFileReader methods
    /**
     * Implemented to satisfy SpecFileReader interface.  Since spec and
     * results files are the same, no need to open a new one.
     */
    void ShimadzuMLBReader::openFile(const char* filename, bool mzSort) {}

    void ShimadzuMLBReader::setIdType(SPEC_ID_TYPE type) {}

    /**
     * Return a spectrum via the returnData argument.  If not found in the
     * spectra map, return false and leave returnData unchanged.
     */
    bool ShimadzuMLBReader::getSpectrum(int identifier, SpecData& returnData, SPEC_ID_TYPE findBy, bool getPeaks)
    {
        map<int, SpecData*>::iterator found = spectra_.find(identifier);
        if (found == spectra_.end())
        {
            return false;
        }

        SpecData* foundData = found->second;
        returnData = *foundData;
        return true;
    }

    /**
     * Only specific spectra can be accessed from the ShimadzuMLBReader.
     */
    bool ShimadzuMLBReader::getSpectrum(string identifier, SpecData& returnData, bool getPeaks)
    {
        Verbosity::warn("ShimadzuMLBReader cannot fetch spectra by string identifier, "
                        "only by spectrum index.");
        return false;
    }

    /**
     * Only specific spectra can be accessed from the ShimadzuMLBReader.
     */
    bool ShimadzuMLBReader::getNextSpectrum(SpecData& returnData, bool getPeaks)
    {
        Verbosity::warn("ShimadzuMLBReader does not support sequential file reading.");
        return false;
    }
}