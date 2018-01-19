//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2017 Vanderbilt University
//
// Contributor(s):
//

#define PWIZ_SOURCE

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "IdpSqlExtensions.hpp"
#include "boost/crc.hpp"
#include "boost/range/algorithm/sort.hpp"
#include "sqlite3ext.h" /* Do not use <sqlite3.h>! */
#include "sqlite3pp.h"
SQLITE_EXTENSION_INIT1

using namespace pwiz::util;
namespace sqlite = sqlite3pp;


namespace {

    struct DistinctDoubleArraySum
    {
        typedef DistinctDoubleArraySum MyType;
        set<int> arrayIds;
        vector<double> result;
        boost::crc_32_type crc32;

        DistinctDoubleArraySum(int arrayLength) : result((size_t)arrayLength, 0.0) {}

        static void Step(sqlite3_context* context, int numValues, sqlite3_value** values)
        {
            void* aggContext = sqlite3_aggregate_context(context, sizeof(MyType*));
            if (aggContext == NULL)
                throw runtime_error(sqlite3_errmsg(sqlite3_context_db_handle(context)));

            MyType** ppThis = static_cast<MyType**>(aggContext);
            MyType*& pThis = *ppThis;

            if (numValues > 1 || values[0] == NULL)
                return;

            int arrayByteCount = sqlite3_value_bytes(values[0]);
            int arrayLength = arrayByteCount / 8;
            const char* arrayBytes = static_cast<const char*>(sqlite3_value_blob(values[0]));
            if (arrayBytes == NULL)
                return;

            if (arrayByteCount % 8 > 0)
                throw runtime_error("distinct_double_array_sum only works with BLOBs of double precision floats");

            if (pThis == NULL)
                pThis = new DistinctDoubleArraySum(arrayLength);
            else
                pThis->crc32.reset();

            // if the arrayId was already in the set, ignore its values
            pThis->crc32.process_bytes(arrayBytes, arrayByteCount);
            int arrayId = pThis->crc32.checksum();
            if (!pThis->arrayIds.insert(arrayId).second)
                return;

            const double* arrayValues = reinterpret_cast<const double*>(arrayBytes);

            for (int i = 0; i < arrayLength; ++i)
                pThis->result[i] += arrayValues[i];
        }

        static void Final(sqlite3_context* context)
        {
            void* aggContext = sqlite3_aggregate_context(context, 0);
            if (aggContext == NULL)
                throw runtime_error(sqlite3_errmsg(sqlite3_context_db_handle(context)));

            MyType** ppThis = static_cast<MyType**>(aggContext);
            MyType*& pThis = *ppThis;

            if (pThis == NULL)
                pThis = new DistinctDoubleArraySum(0);

            sqlite3_result_blob(context, pThis->result.empty() ? NULL : &pThis->result[0], pThis->result.size() * sizeof(double), SQLITE_TRANSIENT);

            delete pThis;
        }
    };


    struct DistinctDoubleArrayMean
    {
        typedef DistinctDoubleArrayMean MyType;
        set<int> arrayIds;
        vector<double> result;
        boost::crc_32_type crc32;

        DistinctDoubleArrayMean(int arrayLength) : result((size_t)arrayLength, 0.0) {}

        static void Step(sqlite3_context* context, int numValues, sqlite3_value** values)
        {
            void* aggContext = sqlite3_aggregate_context(context, sizeof(MyType*));
            if (aggContext == NULL)
                throw runtime_error(sqlite3_errmsg(sqlite3_context_db_handle(context)));

            MyType** ppThis = static_cast<MyType**>(aggContext);
            MyType*& pThis = *ppThis;

            if (numValues > 1 || values[0] == NULL)
                return;

            int arrayByteCount = sqlite3_value_bytes(values[0]);
            int arrayLength = arrayByteCount / 8;
            const char* arrayBytes = static_cast<const char*>(sqlite3_value_blob(values[0]));
            if (arrayBytes == NULL)
                return;

            if (arrayByteCount % 8 > 0)
                throw runtime_error("distinct_double_array_sum only works with BLOBs of double precision floats");

            if (pThis == NULL)
                pThis = new DistinctDoubleArrayMean(arrayLength);
            else
                pThis->crc32.reset();

            // if the arrayId was already in the set, ignore its values
            pThis->crc32.process_bytes(arrayBytes, arrayByteCount);
            int arrayId = pThis->crc32.checksum();
            if (!pThis->arrayIds.insert(arrayId).second)
                return;

            const double* arrayValues = reinterpret_cast<const double*>(arrayBytes);

            for (int i = 0; i < arrayLength; ++i)
                pThis->result[i] += arrayValues[i];
        }

        static void Final(sqlite3_context* context)
        {
            void* aggContext = sqlite3_aggregate_context(context, 0);
            if (aggContext == NULL)
                throw runtime_error(sqlite3_errmsg(sqlite3_context_db_handle(context)));

            MyType** ppThis = static_cast<MyType**>(aggContext);
            MyType*& pThis = *ppThis;

            if (pThis == NULL)
                pThis = new DistinctDoubleArrayMean(0);

            // divide sum by number of elements
            if (!pThis->result.empty())
                for (auto& r : pThis->result)
                    r /= pThis->arrayIds.size();

            sqlite3_result_blob(context, pThis->result.empty() ? NULL : &pThis->result[0], pThis->result.size() * sizeof(double), SQLITE_TRANSIENT);

            delete pThis;
        }
    };

    struct DistinctTukeyBiweightAverage
    {
        typedef DistinctTukeyBiweightAverage MyType;
        set<int> arrayIds;
        vector<double> result;
        vector<vector<double> > tukeyBuffer;
        boost::crc_32_type crc32;

        DistinctTukeyBiweightAverage(int arrayLength) : result((size_t)arrayLength, 0.0), tukeyBuffer(arrayLength) {}

        static void Step(sqlite3_context* context, int numValues, sqlite3_value** values)
        {
            void* aggContext = sqlite3_aggregate_context(context, sizeof(MyType*));
            if (aggContext == NULL)
                throw runtime_error(sqlite3_errmsg(sqlite3_context_db_handle(context)));

            MyType** ppThis = static_cast<MyType**>(aggContext);
            MyType*& pThis = *ppThis;

            if (numValues > 1 || values[0] == NULL)
                return;

            int arrayByteCount = sqlite3_value_bytes(values[0]);
            int arrayLength = arrayByteCount / 8;
            const char* arrayBytes = static_cast<const char*>(sqlite3_value_blob(values[0]));
            if (arrayBytes == NULL)
                return;

            if (arrayByteCount % 8 > 0)
                throw runtime_error("distinct_double_array_sum only works with BLOBs of double precision floats");

            if (pThis == NULL)
                pThis = new DistinctTukeyBiweightAverage(arrayLength);
            else
                pThis->crc32.reset();

            // if the arrayId was already in the set, ignore its values
            pThis->crc32.process_bytes(arrayBytes, arrayByteCount);
            int arrayId = pThis->crc32.checksum();
            if (!pThis->arrayIds.insert(arrayId).second)
                return;

            const double* arrayValues = reinterpret_cast<const double*>(arrayBytes);

            for (int i = 0; i < arrayLength; ++i)
                pThis->tukeyBuffer[i].push_back(arrayValues[i]);
        }

        static inline double weight_bisquare(double x)
        {
            return fabs(x) <= 1.0 ? (1 - x*x) * (1 - x*x) : 0;
        }

        static inline double safe_log(double x)
        {
            return x > 1 ? std::log(x) : 0.0;
        }

        static void Final(sqlite3_context* context) { Final(context, false); }

        static void FinalLog(sqlite3_context* context){ Final(context, true); }

        static void Final(sqlite3_context* context, bool logValues)
        {
            void* aggContext = sqlite3_aggregate_context(context, 0);
            if (aggContext == NULL)
                throw runtime_error(sqlite3_errmsg(sqlite3_context_db_handle(context)));

            MyType** ppThis = static_cast<MyType**>(aggContext);
            MyType*& pThis = *ppThis;

            if (pThis == NULL)
                pThis = new DistinctTukeyBiweightAverage(0);
            else
            {
                double c = 5.0;
                double epsilon = 0.0001;

                vector<double> medians(pThis->tukeyBuffer[0].size());
                auto length = pThis->tukeyBuffer[0].size();
                for (int column = 0; column < pThis->tukeyBuffer.size(); ++column)
                {
                    vector<double>& values = pThis->tukeyBuffer[column];
                    if (logValues)
                        std::transform(values.begin(), values.end(), values.begin(), static_cast<double(*)(double)>(safe_log));

                    medians = values;
                    boost::range::sort(medians);

                    double median = (length % 2 == 0) ? (medians[length / 2 - 1] + medians[length / 2]) / 2.0 : medians[length / 2];

                    for (size_t i = 0; i < length; ++i)
                        medians[i] = fabs(values[i] - median);
                    boost::range::sort(medians);

                    double S = (length % 2 == 0) ? (medians[length / 2 - 1] + medians[length / 2]) / 2.0 : medians[length / 2];

                    for (size_t i = 0; i < length; ++i)
                        medians[i] = (values[i] - median) / (c*S + epsilon);

                    double sum = 0.0;
                    double sumw = 0.0;
                    for (size_t i = 0; i < length; ++i)
                    {
                        sum += weight_bisquare(medians[i]) * values[i];
                        sumw += weight_bisquare(medians[i]);
                    }

                    pThis->result[column] = sum / sumw;
                }
            }

            sqlite3_result_blob(context, pThis->result.empty() ? NULL : &pThis->result[0], pThis->result.size() * sizeof(double), SQLITE_TRANSIENT);

            delete pThis;
        }
    };

    void PrintDoubleArray(sqlite3_context* context, int numValues, sqlite3_value** values)
    {
        if (numValues != 1)
        {
            sqlite3_result_error(context, "[PRINT_DOUBLE_ARRAY] requires 1 double array argument", -1);
            return;
        }

        if (values[0] == NULL)
        {
            sqlite3_result_null(context);
            return;
        }

        const void* blob = sqlite3_value_blob(values[0]);
        int size = sqlite3_value_bytes(values[0]) / sizeof(double);

        char* str;
        int strSize;
        if (blob == NULL)
        {
            strSize = size * 2; // all 0s with commas between plus a null terminator
            str = (char*)sqlite3_malloc(strSize); // all 0s with commas between plus a null terminator
            for (size_t i = 0; i < size; ++i)
                str[i*2] = '0', str[i*2 + 1] = ',';
            str[size * 2 - 1] = 0;
        }
        else
        {
            strSize = size * 30;
            str = (char*)sqlite3_malloc(strSize);

            const double* blobArray = reinterpret_cast<const double*>(blob);

            int offset = 0;
            for (size_t i = 0; i < size; ++i)
                offset += sprintf(&str[offset], "%.5f,", blobArray[i]);
        }

        sqlite3_result_text(context, str, strSize, sqlite3_free);
    }

    struct GroupConcatEx
    {
        static void Step(sqlite3_context *context, int argc, sqlite3_value **argv)
        {
            const char *zVal;
            const char *zSep;
            int nVal, nSep;
            assert(argc == 1 || argc == 2);
            if (sqlite3_value_type(argv[0]) == SQLITE_NULL) return;

            void* aggContext = sqlite3_aggregate_context(context, sizeof(string*));
            if (aggContext == NULL)
                throw runtime_error(sqlite3_errmsg(sqlite3_context_db_handle(context)));

            string** ppAccum = static_cast<string**>(aggContext);
            string*& pAccum = *ppAccum;

            if (!pAccum)
                pAccum = new string();

            int firstTerm = pAccum->empty();
            if (!firstTerm){
                if (argc == 2){
                    zSep = (char*)sqlite3_value_text(argv[1]);
                    nSep = sqlite3_value_bytes(argv[1]);
                }
                else{
                    zSep = separator_.c_str();
                    nSep = separator_.length();
                }
                if (zSep) pAccum->append(zSep, zSep + nSep);
            }
            zVal = (char*)sqlite3_value_text(argv[0]);
            nVal = sqlite3_value_bytes(argv[0]);
            if (zVal) pAccum->append(zVal, zVal + nVal);
        }

        static void Final(sqlite3_context *context)
        {
            string **pAccum = (string**)sqlite3_aggregate_context(context, 0);
            if (pAccum && *pAccum)
            {
                sqlite3_result_text(context, (*pAccum)->c_str(), (*pAccum)->length(), SQLITE_TRANSIENT);
                delete *pAccum;
            }
        }

        static string separator_;
    };

    string GroupConcatEx::separator_ = ",";

    void SortUnmappedLast(sqlite3_context* context, int numValues, sqlite3_value** values)
    {
        if (numValues != 1)
        {
            sqlite3_result_error(context, "[SORT_UNMAPPED_LAST] requires 1 text argument", -1);
            return;
        }

        if (values[0] == NULL)
        {
            sqlite3_result_null(context);
            return;
        }

        const unsigned char* text = sqlite3_value_text(values[0]);
        auto textRange = boost::make_iterator_range(text, text + sqlite3_value_bytes(values[0]));

        if (textRange.size() < 3 || std::find(textRange.begin(), textRange.end(), GroupConcatEx::separator_[0]) == textRange.end())
        {
            sqlite3_result_text(context, (const char*) text, textRange.size(), NULL);
            return;
        }

        vector<string> tokens;
        bal::split(tokens, textRange, bal::is_any_of(GroupConcatEx::separator_));

        std::sort(tokens.begin(), tokens.end(), [&](const string& lhs, const string& rhs)
        {
            bool lhsUnmapped = bal::starts_with(lhs, "Unmapped_");
            bool rhsUnmapped = bal::starts_with(rhs, "Unmapped_");
            if (lhsUnmapped && rhsUnmapped)
                return lhs < rhs;
            if (lhsUnmapped)
                return false;
            if (rhsUnmapped)
                return true;
            return lhs < rhs;
        });

        char* result = (char*)sqlite3_malloc(textRange.size()+1);
        size_t offset = 0;
        for (const string& token : tokens)
        {
            std::copy(token.begin(), token.end(), result + offset);
            offset += token.length() + 1;
            result[offset-1] = GroupConcatEx::separator_[0];
        }
        result[textRange.size()] = 0;

        sqlite3_result_text(context, result, textRange.size()+1, sqlite3_free);
    };

    /// Automatically choose monoisotopic or average mass error based on the following logic:
    /// if the absolute value of monoisotopic error is less than absolute value of average error
    /// or if the monoisotopic error is nearly a multiple of a neutron mass,
    /// then return the monoisotopic error.
    void GetSmallerMassError(sqlite3_context* context, int numValues, sqlite3_value** values)
    {
        if (numValues != 2 || values[0] == NULL || values[1] == NULL)
        {
            sqlite3_result_error(context, "[GET_SMALLER_MASS_ERROR] requires 2 numeric arguments", -1);
            return;
        }

        double mono = sqlite3_value_double(values[0]);
        double avg = sqlite3_value_double(values[1]);

        bool monoisotopic = fabs(mono) < fabs(avg) || fmod(fabs(mono), pwiz::chemistry::Neutron) < fabs(avg);
        sqlite3_result_double(context, monoisotopic ? mono : avg);
    }

    /// Same as GetSmallerMassError(), but when monoisotopic error is used because it is nearly a multiple of a neutron mass,
    /// the returned error is adjusted to factor out the neutron contribution; the result is the true monoisotopic error.
    void GetSmallerMassErrorAdjusted(sqlite3_context* context, int numValues, sqlite3_value** values)
    {
        if (numValues != 2 || values[0] == NULL || values[1] == NULL)
        {
            sqlite3_result_error(context, "[GET_SMALLER_MASS_ERROR_ADJUSTED] requires 2 numeric arguments", -1);
            return;
        }

        double mono = sqlite3_value_double(values[0]);
        double avg = sqlite3_value_double(values[1]);

        if (fabs(mono) < fabs(avg))
            sqlite3_result_double(context, mono);
        else
        {
            double monoModNeutron = fmod(mono, pwiz::chemistry::Neutron);
            bool monoisotopic = fabs(monoModNeutron) < fabs(avg);
            sqlite3_result_double(context, monoisotopic ? monoModNeutron : avg);
        }
    }

    // WITHIN_MASS_TOLERANCE_MZ(observed, expected, tolerance)
    void WithinMassToleranceMZ(sqlite3_context* context, int numValues, sqlite3_value** values)
    {
        if (numValues != 3 || values[0] == NULL || values[1] == NULL || values[2] == NULL)
        {
            sqlite3_result_error(context, "[WITHIN_MASS_TOLERANCE_MZ] requires 3 numeric arguments", -1);
            return;
        }

        double observed = sqlite3_value_double(values[0]);
        double expected = sqlite3_value_double(values[1]);
        double tolerance = sqlite3_value_double(values[2]);
        double lower_bound = expected - tolerance;
        double upper_bound = expected + tolerance;

        sqlite3_result_int(context, (observed > lower_bound && observed < upper_bound) ? 1 : 0);
    }

    // WITHIN_MASS_TOLERANCE_PPM(observed, expected, tolerance)
    void WithinMassTolerancePPM(sqlite3_context* context, int numValues, sqlite3_value** values)
    {
        if (numValues != 3 || values[0] == NULL || values[1] == NULL || values[2] == NULL)
        {
            sqlite3_result_error(context, "[WITHIN_MASS_TOLERANCE_PPM] requires 3 numeric arguments", -1);
            return;
        }

        double observed = sqlite3_value_double(values[0]);
        double expected = sqlite3_value_double(values[1]);
        double tolerance = sqlite3_value_double(values[2]);
        double ppmDelta = fabs(expected) * tolerance * 1e-6;
        double lower_bound = expected - ppmDelta;
        double upper_bound = expected + ppmDelta;

        sqlite3_result_int(context, (observed > lower_bound && observed < upper_bound) ? 1 : 0);
    }

} // namespace


// no need to rename IDPicker namespace for these global functions
namespace IDPicker {

PWIZ_API_DECL void setGroupConcatSeparator(const std::string& separator) { GroupConcatEx::separator_ = separator; }

PWIZ_API_DECL const std::string& getGroupConcatSeparator() { return GroupConcatEx::separator_; }

} // IDPicker


extern "C" {

PWIZ_API_DECL int sqlite3_idpsqlextensions_init(sqlite3 *idpDbConnection, char **pzErrMsg, const sqlite3_api_routines *pApi)
{
    int rc = SQLITE_OK;
    SQLITE_EXTENSION_INIT2(pApi);

    rc += sqlite3_create_function(idpDbConnection, "distinct_double_array_sum", -1, SQLITE_ANY, 0, NULL, &DistinctDoubleArraySum::Step, &DistinctDoubleArraySum::Final);
    rc += sqlite3_create_function(idpDbConnection, "distinct_double_array_mean", -1, SQLITE_ANY, 0, NULL, &DistinctDoubleArrayMean::Step, &DistinctDoubleArrayMean::Final);

    rc += sqlite3_create_function(idpDbConnection, "distinct_double_array_tukey_biweight_average", -1, SQLITE_ANY, 0, NULL, &DistinctTukeyBiweightAverage::Step, &DistinctTukeyBiweightAverage::Final);

    rc += sqlite3_create_function(idpDbConnection, "distinct_double_array_tukey_biweight_log_average", -1, SQLITE_ANY, 0, NULL, &DistinctTukeyBiweightAverage::Step, &DistinctTukeyBiweightAverage::FinalLog);

    rc += sqlite3_create_function(idpDbConnection, "print_double_array", 1, SQLITE_ANY, 0, &PrintDoubleArray, NULL, NULL);

    rc += sqlite3_create_function(idpDbConnection, "group_concat", -1, SQLITE_ANY, 0, NULL, &GroupConcatEx::Step, &GroupConcatEx::Final);
    rc += sqlite3_create_function(idpDbConnection, "group_concat_ex", -1, SQLITE_ANY, 0, NULL, &GroupConcatEx::Step, &GroupConcatEx::Final);
    
    rc += sqlite3_create_function(idpDbConnection, "sort_unmapped_last", 1, SQLITE_ANY, 0, &SortUnmappedLast, NULL, NULL);

    rc += sqlite3_create_function(idpDbConnection, "get_smaller_mass_error", 2, SQLITE_ANY, 0, &GetSmallerMassError, NULL, NULL);

    rc += sqlite3_create_function(idpDbConnection, "get_smaller_mass_error_adjusted", 2, SQLITE_ANY, 0, &GetSmallerMassErrorAdjusted, NULL, NULL);

    rc += sqlite3_create_function(idpDbConnection, "within_mass_tolerance_mz", 3, SQLITE_ANY, 0, &WithinMassToleranceMZ, NULL, NULL);

    rc += sqlite3_create_function(idpDbConnection, "within_mass_tolerance_ppm", 3, SQLITE_ANY, 0, &WithinMassTolerancePPM, NULL, NULL);

    return rc;
}

} // extern C
