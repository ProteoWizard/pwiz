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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2013 Vanderbilt University
//
// Contributor(s):
//


#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "boost/foreach_field.hpp"
#include "boost/range/algorithm/remove_if.hpp"
#include "boost/range/adaptor/map.hpp"
#include "boost/variant.hpp"

#include "SchemaUpdater.hpp"
#include "IdpSqlExtensions.hpp"
#include "Parser.hpp"
#include "Embedder.hpp"
#include "sqlite3pp.h"
#include <iomanip>
#include "CoreVersion.hpp"
#include "idpQueryVersion.hpp"


using namespace IDPicker;
namespace sqlite = sqlite3pp;
using std::setw;
using std::setfill;
using boost::format;


BEGIN_IDPICKER_NAMESPACE


BOOST_ENUM(GroupBy,
    (Invalid)
    (Protein)
    (ProteinGroup)
    (Cluster)
    (Gene)
    (GeneGroup)
    (DistinctMatch)
    (Peptide)
    (PeptideGroup)
    (Spectrum)
    /*(PeptideSpectrumMatch)
    (SpectrumSourceAndGroup)*/
    (Modification)
    (DeltaMass)
    (ModifiedSite)
    (ProteinSite)
);

typedef pair<string, int> SqlColumn;

#define SHARED_QUANTATITIVE_COLUMNS \
    (DistinctPeptides) \
    (DistinctMatches) \
    (FilteredSpectra) \
    (PrecursorIntensity) \
    (PrecursorArea) \
    (PrecursorBestSNR) \
    (PrecursorMeanSNR) \
    (iTRAQ4plex) \
    (iTRAQ8plex) \
    (TMT2plex) \
    (TMT6plex) \
    (TMT10plex) \
    (TMT11plex) \
    (TMTpro16plex) \
    (PivotMatchesByGroup) \
    (PivotMatchesBySource) \
    (PivotPeptidesByGroup) \
    (PivotPeptidesBySource) \
    (PivotSpectraByGroup) \
    (PivotSpectraBySource) \
    (PivotPrecursorIntensityByGroup) \
    (PivotPrecursorIntensityBySource) \
    (PivotPrecursorAreaByGroup) \
    (PivotPrecursorAreaBySource) \
    (PivotPrecursorBestSNRByGroup) \
    (PivotPrecursorBestSNRBySource) \
    (PivotPrecursorMeanSNRByGroup) \
    (PivotPrecursorMeanSNRBySource) \
    (PivotITRAQByGroup) \
    (PivotITRAQBySource) \
    (PivotTMTByGroup) \
    (PivotTMTBySource)

BOOST_ENUM_VALUES(QuantitationRollupMethod, const char*, \
        (Invalid)("") \
        (Sum)("DISTINCT_DOUBLE_ARRAY_SUM") \
        (Mean)("DISTINCT_DOUBLE_ARRAY_MEAN") \
        (Median)("DISTINCT_DOUBLE_ARRAY_MEDIAN") \
        (Tukey)("DISTINCT_DOUBLE_ARRAY_TUKEY_BIWEIGHT_AVERAGE") \
        (TukeyLog)("DISTINCT_DOUBLE_ARRAY_TUKEY_BIWEIGHT_LOG_AVERAGE") \
    );

#define SHARED_QUANTITATIVE_SQLCOLUMNS(ns) \
    case ns::DistinctPeptides: return make_pair("COUNT(DISTINCT pi.Peptide)", SQLITE_INTEGER); \
    case ns::DistinctMatches: return make_pair("COUNT(DISTINCT dm.DistinctMatchId)", SQLITE_INTEGER); \
    case ns::FilteredSpectra: return make_pair("COUNT(DISTINCT psm.Spectrum)", SQLITE_INTEGER); \
    case ns::PrecursorIntensity: return make_pair("IFNULL(SUM(DISTINCT xic.PeakIntensity), 0)", SQLITE_FLOAT); \
    case ns::PrecursorArea: return make_pair("IFNULL(SUM(DISTINCT xic.PeakArea), 0)", SQLITE_FLOAT); \
    case ns::PrecursorBestSNR: return make_pair("IFNULL(MAX(xic.PeakSNR), 0)", SQLITE_FLOAT); \
    case ns::PrecursorMeanSNR: return make_pair("IFNULL(AVG(DISTINCT xic.PeakSNR), 0)", SQLITE_FLOAT); \
    case ns::iTRAQ4plex: return make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.iTRAQ_ReporterIonIntensities), 0)", SQLITE_BLOB); \
    case ns::iTRAQ8plex: return make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.iTRAQ_ReporterIonIntensities), 0)", SQLITE_BLOB); \
    case ns::TMT2plex: return make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities), 0)", SQLITE_BLOB); \
    case ns::TMT6plex: return make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities), 0)", SQLITE_BLOB); \
    case ns::TMT10plex: return make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities), 0)", SQLITE_BLOB); \
    case ns::TMT11plex: return make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities), 0)", SQLITE_BLOB); \
    case ns::TMTpro16plex: return make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities), 0)", SQLITE_BLOB); \
    case ns::PivotMatchesByGroup: return make_pair("0", SQLITE_INTEGER); \
    case ns::PivotMatchesBySource: return make_pair("0", SQLITE_INTEGER); \
    case ns::PivotPeptidesByGroup: return make_pair("0", SQLITE_INTEGER); \
    case ns::PivotPeptidesBySource: return make_pair("0", SQLITE_INTEGER); \
    case ns::PivotSpectraByGroup: return make_pair("0", SQLITE_INTEGER); \
    case ns::PivotSpectraBySource: return make_pair("0", SQLITE_INTEGER); \
    case ns::PivotPrecursorIntensityByGroup: return make_pair("0", SQLITE_FLOAT); \
    case ns::PivotPrecursorIntensityBySource: return make_pair("0", SQLITE_FLOAT); \
    case ns::PivotPrecursorAreaByGroup: return make_pair("0", SQLITE_FLOAT); \
    case ns::PivotPrecursorAreaBySource: return make_pair("0", SQLITE_FLOAT); \
    case ns::PivotPrecursorBestSNRByGroup: return make_pair("0", SQLITE_FLOAT); \
    case ns::PivotPrecursorBestSNRBySource: return make_pair("0", SQLITE_FLOAT); \
    case ns::PivotPrecursorMeanSNRByGroup: return make_pair("0", SQLITE_FLOAT); \
    case ns::PivotPrecursorMeanSNRBySource: return make_pair("0", SQLITE_FLOAT); \
    case ns::PivotITRAQByGroup: return make_pair("0", SQLITE_BLOB); \
    case ns::PivotITRAQBySource: return make_pair("0", SQLITE_BLOB); \
    case ns::PivotTMTByGroup: return make_pair("0", SQLITE_BLOB); \
    case ns::PivotTMTBySource: return make_pair("0", SQLITE_BLOB);


BOOST_ENUM(ProteinColumn,
    (Invalid)
    (Accession)
    (GeneId)
    (GeneGroup)
    (IsDecoy)
    (Cluster)
    (ProteinGroup)
    (Length)
    (PercentCoverage)
    (Sequence)
    (Description)
    (TaxonomyId)
    (GeneName)
    (GeneFamily)
    (Chromosome)
    (GeneDescription)
    (PeptideGroups)
    (PeptideSequences)
    SHARED_QUANTATITIVE_COLUMNS
);

SqlColumn getSqlColumn(ProteinColumn column)
{
    switch (column.index())
    {
        default: throw runtime_error(string("[getSqlColumn] BUG: no case for ProteinColumn::") + column.str());

        case ProteinColumn::Invalid: return make_pair("", 0);
        case ProteinColumn::Accession: return make_pair("GROUP_CONCAT(DISTINCT pro.Accession)", SQLITE_TEXT);
        case ProteinColumn::GeneId: return make_pair("SORT_UNMAPPED_LAST(GROUP_CONCAT(DISTINCT pro.GeneId))", SQLITE_TEXT);
        case ProteinColumn::GeneGroup: return make_pair("pro.GeneGroup", SQLITE_INTEGER);
        case ProteinColumn::IsDecoy: return make_pair("pro.IsDecoy", SQLITE_INTEGER);
        case ProteinColumn::Cluster: return make_pair("pro.Cluster", SQLITE_INTEGER);
        case ProteinColumn::ProteinGroup: return make_pair("pro.ProteinGroup", SQLITE_INTEGER);
        case ProteinColumn::Length: return make_pair("pro.Length", SQLITE_INTEGER);
        case ProteinColumn::PercentCoverage: return make_pair("ROUND(pc.Coverage, 2)", SQLITE_FLOAT);
        case ProteinColumn::Sequence: return make_pair("pd.Sequence", SQLITE_TEXT);
        case ProteinColumn::Description: return make_pair("pmd.Description", SQLITE_TEXT);
        case ProteinColumn::TaxonomyId: return make_pair("pmd.TaxonomyId", SQLITE_INTEGER);
        case ProteinColumn::GeneName: return make_pair("pmd.GeneName", SQLITE_TEXT);
        case ProteinColumn::GeneFamily: return make_pair("pmd.GeneFamily", SQLITE_TEXT);
        case ProteinColumn::Chromosome: return make_pair("pmd.Chromosome", SQLITE_TEXT);
        case ProteinColumn::GeneDescription: return make_pair("pmd.GeneDescription", SQLITE_TEXT);
        case ProteinColumn::PeptideGroups: return make_pair("GROUP_CONCAT(DISTINCT pep.PeptideGroup)", SQLITE_TEXT);
        case ProteinColumn::PeptideSequences: return make_pair("GROUP_CONCAT(DISTINCT (SELECT IFNULL(SUBSTR(pd.Sequence, pi.Offset+1, pi.Length), pep.DecoySequence) FROM PeptideInstance pi LEFT JOIN ProteinData pd ON pi.Protein=pd.Id WHERE pep.Id=pi.Peptide))", SQLITE_TEXT);
        SHARED_QUANTITATIVE_SQLCOLUMNS(ProteinColumn)
    }
}


BOOST_ENUM(PeptideColumn,
    (Invalid)
    (MonoisotopicMass)
    (MolecularWeight)
    (ProteinCount)
    (ProteinGroupCount)
    (ProteinAccessions)
    (GeneIds)
    (GeneGroups)
    (IsDecoy)
    (Cluster)
    (ProteinGroups)
    (Length)
    (ProteinDescriptions)
    (TaxonomyIds)
    (GeneNames)
    (GeneFamilies)
    (Chromosomes)
    (GeneDescriptions)
    (PeptideGroup)
    (Sequence)
    (Instances)
    (Modifications)
    (Charges)
    (SpectrumId)
    SHARED_QUANTATITIVE_COLUMNS
);

SqlColumn getSqlColumn(PeptideColumn column)
{
    switch (column.index())
    {
        default: throw runtime_error(string("[getSqlColumn] BUG: no case for PeptideColumn::") + column.str());

        case PeptideColumn::Invalid: return make_pair("", 0);
        case PeptideColumn::MonoisotopicMass: return make_pair("pep.MonoisotopicMass", SQLITE_FLOAT);
        case PeptideColumn::MolecularWeight: return make_pair("pep.MolecularWeight", SQLITE_FLOAT);
        case PeptideColumn::ProteinCount: return make_pair("COUNT(DISTINCT pro.Id)", SQLITE_INTEGER);
        case PeptideColumn::ProteinGroupCount: return make_pair("COUNT(DISTINCT pro.ProteinGroup)", SQLITE_INTEGER);
        case PeptideColumn::ProteinAccessions: return make_pair("GROUP_CONCAT(DISTINCT pro.Accession)", SQLITE_TEXT);
        case PeptideColumn::GeneIds: return make_pair("SORT_UNMAPPED_LAST(GROUP_CONCAT(DISTINCT pro.GeneId))", SQLITE_TEXT);
        case PeptideColumn::GeneGroups: return make_pair("GROUP_CONCAT(DISTINCT pro.GeneGroup)", SQLITE_TEXT);
        case PeptideColumn::IsDecoy: return make_pair("pro.IsDecoy", SQLITE_INTEGER);
        case PeptideColumn::Cluster: return make_pair("pro.Cluster", SQLITE_INTEGER);
        case PeptideColumn::ProteinGroups: return make_pair("GROUP_CONCAT(DISTINCT pro.ProteinGroup)", SQLITE_TEXT);
        case PeptideColumn::Length: return make_pair("pi.Length", SQLITE_INTEGER);
        case PeptideColumn::ProteinDescriptions: return make_pair("GROUP_CONCAT(DISTINCT pmd.Description)", SQLITE_TEXT);
        case PeptideColumn::TaxonomyIds: return make_pair("GROUP_CONCAT(DISTINCT pmd.TaxonomyId)", SQLITE_TEXT);
        case PeptideColumn::GeneNames: return make_pair("GROUP_CONCAT(DISTINCT pmd.GeneName)", SQLITE_TEXT);
        case PeptideColumn::GeneFamilies: return make_pair("GROUP_CONCAT(DISTINCT pmd.GeneFamily)", SQLITE_TEXT);
        case PeptideColumn::Chromosomes: return make_pair("GROUP_CONCAT(DISTINCT pmd.Chromosome)", SQLITE_TEXT);
        case PeptideColumn::GeneDescriptions: return make_pair("GROUP_CONCAT(DISTINCT pmd.GeneDescription)", SQLITE_TEXT);
        case PeptideColumn::PeptideGroup: return make_pair("pep.PeptideGroup", SQLITE_INTEGER);
        case PeptideColumn::Sequence: return make_pair("(SELECT IFNULL(SUBSTR(pd.Sequence, pi.Offset+1, pi.Length), pep.DecoySequence) FROM PeptideInstance pi LEFT JOIN ProteinData pd ON pi.Protein=pd.Id WHERE pep.Id=pi.Peptide)", SQLITE_TEXT);
        case PeptideColumn::Instances: return make_pair("(SELECT GROUP_CONCAT(DISTINCT pro.Accession || '@' || (pi.Offset+1)) "
                                                        "FROM PeptideInstance pi_ "
                                                        "LEFT JOIN ProteinData pro ON pi.Protein = pro.Id "
                                                        "WHERE psm.Peptide = pi_.Peptide)", SQLITE_TEXT);
        case PeptideColumn::Modifications: return make_pair("IFNULL((SELECT GROUP_CONCAT(DISTINCT (ROUND(mod_.MonoMassDelta/{ModificationMassRoundToNearest})*{ModificationMassRoundToNearest}) || '@' || pm_.Site || (pm_.Offset+1)) "
                                                            "        FROM PeptideSpectrumMatch psm_, Peptide pep_, DistinctMatch dm_ "
                                                            "        LEFT JOIN PeptideModification pm_ ON psm_.Id = pm_.PeptideSpectrumMatch "
                                                            "        LEFT JOIN Modification mod_ ON pm_.Modification = mod_.Id "
                                                            "        WHERE {GroupBy} = {SubGroupBy} AND psm_.Peptide=pep_.Id AND psm_.Id=dm_.PsmId "
                                                            "        GROUP BY {SubGroupBy}), '')", SQLITE_TEXT);
        case PeptideColumn::Charges: return make_pair("GROUP_CONCAT(DISTINCT psm.Charge)", SQLITE_TEXT);
        case PeptideColumn::SpectrumId: return make_pair("GROUP_CONCAT(DISTINCT ssg.Name || '/' || ss.Name || '/' || s.NativeID)", SQLITE_TEXT);
        SHARED_QUANTITATIVE_SQLCOLUMNS(PeptideColumn)
    }
}


BOOST_ENUM(ModificationColumn,
    (Invalid)
    (MonoDeltaMass)
    (AvgDeltaMass)
    (ProteinOffsets)
    (ModifiedSite)
    (ProteinSite)
    (PeptideSequences)
    SHARED_QUANTATITIVE_COLUMNS
);

SqlColumn getSqlColumn(ModificationColumn column)
{
    switch (column.index())
    {
        default: throw runtime_error(string("[getSqlColumn] BUG: no case for ModificationColumn::") + column.str());

        case ModificationColumn::Invalid: return make_pair("", 0);
        case ModificationColumn::MonoDeltaMass: return make_pair("GROUP_CONCAT(DISTINCT ROUND(mod.MonoMassDelta/{ModificationMassRoundToNearest})*{ModificationMassRoundToNearest})", SQLITE_FLOAT);
        case ModificationColumn::AvgDeltaMass: return make_pair("GROUP_CONCAT(DISTINCT ROUND(mod.AvgMassDelta/{ModificationMassRoundToNearest})*{ModificationMassRoundToNearest})", SQLITE_FLOAT);
        case ModificationColumn::ProteinOffsets: return make_pair("(SELECT GROUP_CONCAT(DISTINCT (pro.Accession || '@' || (pi.Offset+(CASE WHEN pm.Offset<0 THEN 0 WHEN pm.Offset>=pi.Length THEN (pi.Length-1) ELSE pm.Offset END)+1))) "
                                                                  "FROM PeptideInstance pi_ "
                                                                  "LEFT JOIN ProteinData pro ON pi.Protein = pro.Id "
                                                                  "JOIN PeptideModification pm_ ON psm.Id = pm_.PeptideSpectrumMatch "
                                                                  "JOIN Modification mod_ ON pm_.Modification = mod_.Id "
                                                                  "WHERE psm.Peptide = pi_.Peptide)", SQLITE_TEXT);
        case ModificationColumn::ModifiedSite: return make_pair("GROUP_CONCAT(DISTINCT pm.Site)", SQLITE_TEXT);
        case ModificationColumn::ProteinSite: return make_pair("(pro.Accession || '@' || (pi.Offset+(CASE WHEN pm.Offset<0 THEN 0 WHEN pm.Offset>=pi.Length THEN (pi.Length-1) ELSE pm.Offset END)+1))", SQLITE_TEXT);
        case ModificationColumn::PeptideSequences: return make_pair("GROUP_CONCAT(DISTINCT (SELECT IFNULL(SUBSTR(pd.Sequence, pi.Offset+1, pi.Length), pep.DecoySequence) FROM PeptideInstance pi LEFT JOIN ProteinData pd ON pi.Protein=pd.Id WHERE pep.Id=pi.Peptide))", SQLITE_TEXT);
        SHARED_QUANTITATIVE_SQLCOLUMNS(ModificationColumn)
    }
}

/*PeptideSpectrumMatch, Spectrum, SpectrumSourceAndGroup
Key
DistinctPeptides
DistinctMatches
FilteredSpectra
DistinctAnalyses
DistinctCharges
ProteinGroups
PrecursorMz
ScanTime
ObservedMass
ExactMass
MassError
Analysis
Charge
QValue
Sequence
iTRAQ
TMT
*/

/*Modification, MonoDeltaMass, ModifiedSite
ModifiedSite
MonoDeltaMass
AvgDeltaMass
DistinctPeptides
DistinctMatches
FilteredSpectra
Description
*/


struct ReporterIon
{
    const char* name;
    size_t index;
    bool empty;
    bool reference;
};

ReporterIon iTRAQ_ions[8] =
{
    { "113", 0, false, false },
    { "114", 1, false, false },
    { "115", 2, false, false },
    { "116", 3, false, false },
    { "117", 4, false, false },
    { "118", 5, false, false },
    { "119", 6, false, false },
    { "121", 7, false, false }
};
ReporterIon itraq4plexIons[4] = { iTRAQ_ions[1], iTRAQ_ions[2], iTRAQ_ions[3], iTRAQ_ions[4] };
ReporterIon itraq8plexIons[8] = { iTRAQ_ions[0], iTRAQ_ions[1], iTRAQ_ions[2], iTRAQ_ions[3], iTRAQ_ions[4], iTRAQ_ions[5], iTRAQ_ions[6], iTRAQ_ions[7] };

ReporterIon TMT_ions[16] =
{
    { "126", 0, false, false },
    { "127N", 1, false, false },
    { "127C", 2, false, false },
    { "128N", 3, false, false },
    { "128C", 4, false, false },
    { "129N", 5, false, false },
    { "129C", 6, false, false },
    { "130N", 7, false, false },
    { "130C", 8, false, false },
    { "131N", 9, false, false },
    { "131C", 10, false, false },
    { "132N", 11, false, false },
    { "132C", 12, false, false },
    { "133N", 13, false, false },
    { "133C", 14, false, false },
    { "134", 15, false, false }
};
ReporterIon tmt2plexIons[2] = { TMT_ions[0], TMT_ions[2] };
ReporterIon tmt6plexIons[6] = { TMT_ions[0], TMT_ions[1], TMT_ions[4], TMT_ions[5], TMT_ions[8], TMT_ions[9] };
ReporterIon tmt10plexIons[10] = { TMT_ions[0], TMT_ions[1], TMT_ions[2], TMT_ions[3], TMT_ions[4], TMT_ions[5], TMT_ions[6], TMT_ions[7], TMT_ions[8], TMT_ions[9] };
ReporterIon tmt11plexIons[11] = { TMT_ions[0], TMT_ions[1], TMT_ions[2], TMT_ions[3], TMT_ions[4], TMT_ions[5], TMT_ions[6], TMT_ions[7], TMT_ions[8], TMT_ions[9], TMT_ions[10] };
ReporterIon tmt16plexIons[16] = { TMT_ions[0], TMT_ions[1], TMT_ions[2], TMT_ions[3], TMT_ions[4], TMT_ions[5], TMT_ions[6], TMT_ions[7], TMT_ions[8], TMT_ions[9], TMT_ions[10], TMT_ions[11], TMT_ions[12], TMT_ions[13], TMT_ions[14], TMT_ions[15] };


template <typename ArrayType>
void writeBlobArray(const void* blob, ostream& os, const vector<ReporterIon>& arrayInfo)
{
    if (arrayInfo.empty())
        return;

    if (blob == NULL)
    {
        for (size_t i = 0; i < arrayInfo.size(); ++i)
            if (!arrayInfo[i].empty && !arrayInfo[i].reference)
                os << "0\t";
        os.seekp(-1, ios::cur);
        return;
    }

    const ArrayType* blobArray = reinterpret_cast<const ArrayType*>(blob);

    ArrayType referenceValue = 1;
    for (size_t i = 0; i < arrayInfo.size(); ++i)
        if (arrayInfo[i].reference)
        {
            referenceValue = blobArray[arrayInfo[i].index];
            break;
        }

    if (referenceValue == 0)
        referenceValue = 1; // don't divide by zero

    ios::fmtflags oldFlags = os.flags(ios::fixed);
    for (size_t i = 0; i < arrayInfo.size(); ++i)
        if (!arrayInfo[i].empty && !arrayInfo[i].reference)
            os << blobArray[arrayInfo[i].index] / referenceValue << '\t';
    os.seekp(-1, ios::cur);
    os.flags(oldFlags);
}


string getGroupByString(GroupBy groupBy, double ModificationMassRoundToNearest)
{
    switch (groupBy.index())
    {
        case GroupBy::Protein:          return "pro.Id";
        case GroupBy::ProteinGroup:     return "pro.ProteinGroup";
        case GroupBy::Cluster:          return "pro.Cluster";
        case GroupBy::Gene:             return "pro.GeneId";
        case GroupBy::GeneGroup:        return "pro.GeneGroup";

        case GroupBy::DistinctMatch:    return "dm.DistinctMatchId";
        case GroupBy::Peptide:          return "psm.Peptide";
        case GroupBy::PeptideGroup:     return "pep.PeptideGroup";
        case GroupBy::Spectrum:         return "s.Id || '/' || psm.Peptide";

        case GroupBy::Modification:     return "pm.Modification";
        case GroupBy::DeltaMass:        return bal::replace_all_copy(string("ROUND(mod.MonoMassDelta/{ModificationMassRoundToNearest})*{ModificationMassRoundToNearest}"), string("{ModificationMassRoundToNearest}"), lexical_cast<string>(ModificationMassRoundToNearest));
        case GroupBy::ModifiedSite:     return "pm.Site";
        case GroupBy::ProteinSite:      return "(pro.Id*10000+pi.Offset+pm.Offset)";

        default: throw runtime_error("[getGroupByString] invalid groupBy value");
    }
}


typedef boost::variant<int, double, const void*> PivotDataType;
typedef map<boost::int64_t, map<boost::int64_t, PivotDataType> > PivotDataMap;
typedef map<size_t, PivotDataMap> PivotDataByColumnMap;
void pivotData(sqlite::database& idpDB, GroupBy groupBy, const string& pivotMode, PivotDataMap& pivotDataMap, double ModificationMassRoundToNearest, QuantitationRollupMethod rollupMethod)
{
    string groupByString = getGroupByString(groupBy, ModificationMassRoundToNearest);

    string pivotColumn;
    if (bal::ends_with(pivotMode, "Source"))
        pivotColumn = "ss.Id";
    else
        pivotColumn = "ssgl.Group_";
    
    string countColumn;

    if (bal::contains(pivotMode, "Matches"))
        countColumn = "COUNT(DISTINCT dm.DistinctMatchId)";
    else if (bal::contains(pivotMode, "Peptides"))
        countColumn = "COUNT(DISTINCT psm.Peptide)";
    else if (bal::contains(pivotMode, "Spectra"))
        countColumn = "COUNT(DISTINCT s.Id)";
    else
    {
        string whereConstraint;
        if (bal::contains(pivotMode, "ITRAQ"))
        {
            countColumn = "DISTINCT_DOUBLE_ARRAY_SUM(sq.iTRAQ_ReporterIonIntensities)";
            whereConstraint = "WHERE QuantitationMethod BETWEEN 2 AND 3 ";
        }
        else if (bal::contains(pivotMode, "TMT"))
        {
            countColumn = "DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities)";
            whereConstraint = "WHERE QuantitationMethod BETWEEN 4 AND 8 ";
        }
        else if (bal::contains(pivotMode, "PrecursorIntensity"))
            countColumn = "IFNULL(SUM(DISTINCT xic.PeakIntensity), 0)";
        else if (bal::contains(pivotMode, "PrecursorArea"))
            countColumn = "IFNULL(SUM(DISTINCT xic.PeakArea), 0)";
        else if (bal::contains(pivotMode, "BestSNR"))
            countColumn = "IFNULL(MAX(xic.PeakSNR), 0)";
        else if (bal::contains(pivotMode, "MeanSNR"))
            countColumn = "IFNULL(AVG(DISTINCT xic.PeakSNR), 0)";
        else
            throw runtime_error("unsupported pivot mode");

        // build SQL query with quantitation
        string sql = "SELECT " + groupByString + ", " + pivotColumn + ", " + countColumn + " "
                     "FROM PeptideSpectrumMatch psm "
                     "JOIN DistinctMatch dm ON psm.Id=dm.PsmId "
                     "JOIN Spectrum s ON psm.Spectrum=s.Id "
                     "JOIN SpectrumSource ss ON s.Source=ss.Id "
                     "JOIN SpectrumSourceGroupLink ssgl ON ss.Id=ssgl.Source "
                     "JOIN SpectrumSourceGroup ssg ON ssgl.Group_=ssg.Id "
                     "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
                     "JOIN Peptide pep ON pi.Peptide=pep.Id "
                     "JOIN Protein pro ON pi.Protein=pro.Id "
                     "LEFT JOIN PeptideModification pm ON psm.Id = pm.PeptideSpectrumMatch "
                     "LEFT JOIN Modification mod ON pm.Modification = mod.Id "
                     "LEFT JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id "
                     "LEFT JOIN XICMetrics xic ON dm.DistinctMatchId=xic.DistinctMatch AND s.Source=xic.SpectrumSource " +
                     whereConstraint +
                     "GROUP BY " + groupByString + ", " + pivotColumn;
        if (rollupMethod != QuantitationRollupMethod::Sum) bal::replace_all(sql, "DISTINCT_DOUBLE_ARRAY_SUM", rollupMethod.value());
        cout << sql << endl;
        sqlite::query q(idpDB, sql.c_str());

        if (bal::contains(countColumn, "ARRAY_SUM"))
            for(sqlite::query::rows row : q)
            {
                int blobBytes = row.column_bytes(2);
                if (blobBytes == 0)
                    continue;
                char* blobCopy = new char[blobBytes];
                memcpy(blobCopy, (const char*) row.get<const void*>(2), blobBytes);
                pivotDataMap[row.get<sqlite_int64>(0)][row.get<sqlite_int64>(1)] = blobCopy;
            }
        else
            for(sqlite::query::rows row : q)
                pivotDataMap[row.get<sqlite_int64>(0)][row.get<sqlite_int64>(1)] = row.get<double>(2);
        return;
    }

    // build SQL query without quantitation
    string sql = "SELECT " + groupByString + ", " + pivotColumn + ", " + countColumn + " "
                 "FROM PeptideSpectrumMatch psm "
                 "JOIN DistinctMatch dm ON psm.Id=dm.PsmId "
                 "JOIN Spectrum s ON psm.Spectrum=s.Id "
                 "JOIN SpectrumSource ss ON s.Source=ss.Id "
                 "JOIN SpectrumSourceGroupLink ssgl ON ss.Id=ssgl.Source "
                 "JOIN SpectrumSourceGroup ssg ON ssgl.Group_=ssg.Id "
                 "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
                 "JOIN Peptide pep ON pi.Peptide=pep.Id "
                 "JOIN Protein pro ON pi.Protein=pro.Id "
                 "LEFT JOIN PeptideModification pm ON psm.Id = pm.PeptideSpectrumMatch "
                 "LEFT JOIN Modification mod ON pm.Modification = mod.Id "
                 "GROUP BY " + groupByString + ", " + pivotColumn;
    if (rollupMethod != QuantitationRollupMethod::Sum) bal::replace_all(sql, "DISTINCT_DOUBLE_ARRAY_SUM", rollupMethod.value());
    cout << sql << endl;
    sqlite::query q(idpDB, sql.c_str());
    for(sqlite::query::rows row : q)
        pivotDataMap[row.get<sqlite_int64>(0)][row.get<sqlite_int64>(1)] = row.get<int>(2);
}


template <typename ColumnType>
int parseColumns(const vector<string>& tokens, vector<ColumnType>& enumColumns, vector<SqlColumn>& sqlColumns)
{
    vector<string> invalidTokens;

    for (size_t i = 0; i < tokens.size(); ++i)
    {
        ColumnType newColumn = ColumnType::get_by_name_case_insensitive(tokens[i].c_str()).get_value_or(ColumnType::Invalid);
        if (newColumn == ColumnType::Invalid)
            invalidTokens.push_back(tokens[i]);
        else
        {
            enumColumns.push_back(newColumn);
            sqlColumns.push_back(getSqlColumn(newColumn));
        }
    }

    if (!invalidTokens.empty())
    {
        cerr << "Invalid column choice" << (invalidTokens.size() > 1 ? "s" : "") << ":";
        for (size_t i = 0; i < invalidTokens.size(); ++i)
            cerr << " \"" << invalidTokens[i] << "\"";
        cerr << "\nValid options are:" << endl;
        for (typename ColumnType::const_iterator itr = ColumnType::begin() + 1; itr < ColumnType::end(); ++itr)
            cerr << "  " << itr->str() << endl;
        return 1;
    }

    return 0;
}


// get the most inclusive set of reporter ion headers (i.e. if both TMT10 and 6 are used, write TMT10's headers)
vector<string> getReporterIonHeaders(const ReporterIon ions[], const set<QuantitationMethod>& quantitationMethods)
{
    vector<string> reporterIonColumnHeaders;

    if (quantitationMethods.empty() ||
        quantitationMethods.count(QuantitationMethod::None) + quantitationMethods.count(QuantitationMethod::LabelFree) == quantitationMethods.size())
        return reporterIonColumnHeaders;

    if (&ions[0] == &iTRAQ_ions[0])
    {
        if (quantitationMethods.count(QuantitationMethod::ITRAQ8plex) > 0)
            for (size_t i = 0, end = sizeof(itraq8plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("iTRAQ-") + itraq8plexIons[i].name);
        else if (quantitationMethods.count(QuantitationMethod::ITRAQ4plex) > 0)
            for (size_t i = 0, end = sizeof(itraq4plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("iTRAQ-") + itraq4plexIons[i].name);
    }
    else if (&ions[0] == &TMT_ions[0])
    {
        if (quantitationMethods.count(QuantitationMethod::TMTpro16plex) > 0)
            for (size_t i = 0, end = sizeof(tmt16plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("TMT-") + tmt16plexIons[i].name);
        else if (quantitationMethods.count(QuantitationMethod::TMT11plex) > 0)
            for (size_t i = 0, end = sizeof(tmt11plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("TMT-") + tmt11plexIons[i].name);
        else if (quantitationMethods.count(QuantitationMethod::TMT10plex) > 0)
            for (size_t i = 0, end = sizeof(tmt10plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("TMT-") + tmt10plexIons[i].name);
        else if (quantitationMethods.count(QuantitationMethod::TMT6plex) > 0)
            for (size_t i = 0, end = sizeof(tmt6plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("TMT-") + tmt6plexIons[i].name);
        else if (quantitationMethods.count(QuantitationMethod::TMT2plex) > 0)
            for (size_t i = 0, end = sizeof(tmt2plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("TMT-") + tmt2plexIons[i].name);
    }
    else
        throw runtime_error("[getReporterIonHeaders] unrecognized reporter ion array");

    return reporterIonColumnHeaders;
}

template <typename ColumnType>
int doQuery(GroupBy groupBy,
            const bfs::path& filepath,
            const vector<string>& tokens,
            double ModificationMassRoundToNearest,
            QuantitationRollupMethod rollupMethod)
{
    vector<ColumnType> enumColumns;
    vector<SqlColumn> selectedColumns;
    if (parseColumns<ColumnType>(tokens, enumColumns, selectedColumns))
        return 1;

    SchemaUpdater::update(filepath.string());
    sqlite::database idpDB(filepath.string());
    idpDB.load_extension("IdpSqlExtensions");
    //idpDB.execute("PRAGMA mmap_size=70368744177664; -- 2^46");

    try {sqlite::query q(idpDB, "SELECT Id FROM Protein LIMIT 1"); q.begin();}
    catch (sqlite::database_error&)
    {
        cerr << "Invalid idpDB file \"" << filepath << "\" - missing or corrupt Protein table." << endl;
        return 1;
    }

    try {sqlite::query q(idpDB, "SELECT Id FROM FilterHistory LIMIT 1"); q.begin();}
    catch (sqlite::database_error&)
    {
        cerr << "Invalid idpDB file \"" << filepath << "\" - the basic filters have not been run on it yet; apply them through the GUI (optionally in --headless mode)." << endl;
        return 1;
    }

    // open output file
    string outputFilepath = bfs::change_extension(filepath.string(), ".tsv").string();
    ofstream outputStream(outputFilepath.c_str(), ios::binary);

    string groupByString = getGroupByString(groupBy, ModificationMassRoundToNearest);

    map<string, vector<string> > isobaricSamplesByGroup = Embedder::getIsobaricSampleMapping(filepath.string());

    PivotDataByColumnMap pivotDataByColumn;
    PivotDataByColumnMap::const_iterator findAbstractColumnItr;
    PivotDataMap::const_iterator findIdItr;
    map<boost::int64_t, PivotDataType>::const_iterator findPivotColumnItr;
    map<size_t, vector<boost::int64_t> > pivotColumnIdsByAbstractColumn;

    bool hasSpectrumQuantitation = false;
    bool hasPrecursorQuantitation = false;

    sqlite::query quantitationMethodsQuery(idpDB, "SELECT DISTINCT QuantitationMethod FROM SpectrumSource");
    set<QuantitationMethod> quantitationMethods;
    for(sqlite::query::rows row : quantitationMethodsQuery)
        quantitationMethods.insert(QuantitationMethod::get_by_index(row.get<int>(0)).get());

    map<boost::int64_t, string> groupNameById;
    sqlite::query groupQuery(idpDB, "SELECT Id, Name FROM SpectrumSourceGroup");
    for(sqlite::query::rows row : groupQuery)
        groupNameById[row.get<sqlite_int64>(0)] = row.get<string>(1);


    vector<ReporterIon> itraqMethodIons, tmtMethodIons;
    if (quantitationMethods.count(QuantitationMethod::ITRAQ8plex) > 0)
        itraqMethodIons.assign(itraq8plexIons, itraq8plexIons + 8);
    else if (quantitationMethods.count(QuantitationMethod::ITRAQ4plex) > 0)
        itraqMethodIons.assign(itraq4plexIons, itraq4plexIons + 4);

    if (quantitationMethods.count(QuantitationMethod::TMTpro16plex) > 0)
        tmtMethodIons.assign(tmt16plexIons, tmt16plexIons + 16);
    else if (quantitationMethods.count(QuantitationMethod::TMT11plex) > 0)
        tmtMethodIons.assign(tmt11plexIons, tmt11plexIons + 11);
    else if (quantitationMethods.count(QuantitationMethod::TMT10plex) > 0)
        tmtMethodIons.assign(tmt10plexIons, tmt10plexIons + 10);
    else if (quantitationMethods.count(QuantitationMethod::TMT6plex) > 0)
        tmtMethodIons.assign(tmt6plexIons, tmt6plexIons + 6);
    else if (quantitationMethods.count(QuantitationMethod::TMT2plex) > 0)
        tmtMethodIons.assign(tmt2plexIons, tmt2plexIons + 2);

    bool hasITRAQ = false;
    bool hasTMT = false;
    vector<bool> visibleColumns(selectedColumns.size(), true);

    // write column headers
    for (size_t i=0; i < tokens.size(); ++i)
    {
        if ((bal::icontains(tokens[i], "itraq") && itraqMethodIons.empty()) ||
            (bal::icontains(tokens[i], "tmt") && tmtMethodIons.empty()))
            visibleColumns[i] = false; // this column will not be written to the output file

        if (bal::istarts_with(tokens[i], "iTRAQ") && QuantitationMethod::get_by_name_case_insensitive(tokens[i].c_str()) && !hasITRAQ)
        {
            hasITRAQ = true;
            hasSpectrumQuantitation = true;
            if (!itraqMethodIons.empty())
                BOOST_FOREACH(const string& header, getReporterIonHeaders(iTRAQ_ions, quantitationMethods))
                    outputStream << header << '\t';
        }
        else if (bal::istarts_with(tokens[i], "TMT") && QuantitationMethod::get_by_name_case_insensitive(tokens[i].c_str()) && !hasTMT)
        {
            hasTMT = true;
            hasSpectrumQuantitation = true;
            if (!tmtMethodIons.empty())
                BOOST_FOREACH(const string& header, getReporterIonHeaders(TMT_ions, quantitationMethods))
                    outputStream << header << '\t';
        }
        else if (bal::istarts_with(tokens[i], "Pivot"))
        {
            bool groupBySource = bal::ends_with(tokens[i], "Source");
            string sql = groupBySource ? "SELECT Name, Id FROM SpectrumSource ORDER BY Name"
                                       : "SELECT Name, Id FROM SpectrumSourceGroup ORDER BY Name";
            sqlite::query q(idpDB, sql.c_str());

            vector<boost::int64_t>& pivotColumnIds = pivotColumnIdsByAbstractColumn[i];
            string groupOrSourceName, sampleNamesString;
            vector<string> sampleNames;

            if (bal::icontains(tokens[i], "ITRAQ"))
            {
                if (!itraqMethodIons.empty())
                    for(sqlite::query::rows row : q)
                    {
                        groupOrSourceName = row.get<string>(0);

                        const vector<string>& sampleNames = isobaricSamplesByGroup[groupOrSourceName];

                        if (groupOrSourceName == "/" && sampleNames.empty())
                            continue;

                        if (sampleNames.empty())
                            BOOST_FOREACH(const string& header, getReporterIonHeaders(iTRAQ_ions, quantitationMethods))
                                outputStream << groupOrSourceName << " (" << header << ")\t";
                        else
                            for(const string& sample : sampleNames)
                                if (!bal::iequals(sample, "Reference") && !bal::iequals(sample, "Empty"))
                                    outputStream << sample << "\t";

                        pivotColumnIds.push_back(static_cast<boost::int64_t>(row.get<sqlite3_int64>(1)));
                    }
            }
            else if (bal::icontains(tokens[i], "TMT"))
            {
                if (!tmtMethodIons.empty())
                    for(sqlite::query::rows row : q)
                    {
                        groupOrSourceName = row.get<string>(0);

                        const vector<string>& sampleNames = isobaricSamplesByGroup[groupOrSourceName];

                        if (groupOrSourceName == "/" && sampleNames.empty())
                            continue;

                        if (sampleNames.empty())
                            BOOST_FOREACH(const string& header, getReporterIonHeaders(TMT_ions, quantitationMethods))
                                outputStream << groupOrSourceName << " (" << header << ")\t";
                        else
                            for(const string& sample : sampleNames)
                                if (!bal::iequals(sample, "Reference") && !bal::iequals(sample, "Empty"))
                                    outputStream << sample << "\t";

                        pivotColumnIds.push_back(static_cast<boost::int64_t>(row.get<sqlite3_int64>(1)));
                    }
            }
            else
            {
                bool includeColumnName = bal::contains(tokens[i], "Precursor");
                for(sqlite::query::rows row : q)
                {
                    outputStream << row.get<string>(0) << (includeColumnName ? " " + tokens[i] : "") << '\t';
                    pivotColumnIds.push_back(static_cast<boost::int64_t>(row.get<sqlite3_int64>(1)));
                }
            }

            if (pivotColumnIds.empty())
            {
                outputStream.close();
                bfs::remove(outputFilepath);
                throw runtime_error("no pivot columns available. Are sources assigned to source groups? Has quantitation data been embedded?");
            }
            pivotData(idpDB, groupBy, tokens[i], pivotDataByColumn[i], ModificationMassRoundToNearest, rollupMethod);
        }
        else
        {
            if (bal::icontains(tokens[i], "Precursor"))
                hasPrecursorQuantitation = true;
            outputStream << tokens[i] << '\t';
        }
    }
    outputStream.seekp(-1, std::ios_base::cur);
    outputStream << endl;

    // build SQL query
    string sql = "SELECT {GroupBy}, " + bal::join(selectedColumns | boost::adaptors::map_keys, ", ") + " "
                 "FROM Protein pro "
                 "LEFT JOIN ProteinMetadata pmd ON pro.Id=pmd.Id "
                 "LEFT JOIN ProteinData pd ON pro.Id=pd.Id "
                 "LEFT JOIN ProteinCoverage pc ON pro.Id=pc.Id "
                 "JOIN PeptideInstance pi ON pro.Id=pi.Protein "
                 "JOIN Peptide pep ON pi.Peptide=pep.Id "
                 "JOIN PeptideSpectrumMatch psm ON psm.Peptide=pi.Peptide "
                 "LEFT JOIN PeptideModification pm ON psm.Id = pm.PeptideSpectrumMatch "
                 "LEFT JOIN Modification mod ON pm.Modification = mod.Id "
                 "JOIN Spectrum s ON psm.Spectrum=s.Id "
                 "JOIN SpectrumSource ss ON s.Source=ss.Id "
                 "JOIN SpectrumSourceGroup ssg ON ss.Group_=ssg.Id "
                 "JOIN DistinctMatch dm ON psm.Id=dm.PsmId " +
                 string(hasSpectrumQuantitation ? "LEFT JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id " : "") +
                 string(hasPrecursorQuantitation ? "LEFT JOIN XICMetrics xic ON dm.DistinctMatchId=xic.DistinctMatch AND s.Source=xic.SpectrumSource " : "") +
                 "GROUP BY {GroupBy}";
    bal::replace_all(sql, "{ModificationMassRoundToNearest}", lexical_cast<string>(ModificationMassRoundToNearest));
    bal::replace_all(sql, "{GroupBy}", groupByString);
    bal::replace_all(sql, "{SubGroupBy}", bal::replace_all_copy(groupByString, ".", "_.")); // subgroup table aliases end with '_'
    if (rollupMethod != QuantitationRollupMethod::Sum) bal::replace_all(sql, "DISTINCT_DOUBLE_ARRAY_SUM", rollupMethod.value());
    cout << sql << endl;
    sqlite::query q(idpDB, sql.c_str());

    // write column values
    for(sqlite::query::rows row : q)
    {
        boost::int64_t id = static_cast<boost::int64_t>(row.get<sqlite_int64>(0));

        for (size_t i=0; i < selectedColumns.size(); ++i)
        {
            if (!visibleColumns[i])
                continue;

            if (i > 0)
                outputStream << '\t';

            findAbstractColumnItr = pivotDataByColumn.find(i);

            const SqlColumn& sqlColumn = selectedColumns[i];
            switch (sqlColumn.second)
            {
                case SQLITE_FLOAT:
                case SQLITE_INTEGER:
                    switch (enumColumns[i].index())
                    {
                        case ColumnType::PivotMatchesByGroup:
                        case ColumnType::PivotMatchesBySource:
                        case ColumnType::PivotPeptidesByGroup:
                        case ColumnType::PivotPeptidesBySource:
                        case ColumnType::PivotSpectraByGroup:
                        case ColumnType::PivotSpectraBySource:
                        case ColumnType::PivotPrecursorIntensityByGroup:
                        case ColumnType::PivotPrecursorIntensityBySource:
                        case ColumnType::PivotPrecursorAreaByGroup:
                        case ColumnType::PivotPrecursorAreaBySource:
                        case ColumnType::PivotPrecursorBestSNRByGroup:
                        case ColumnType::PivotPrecursorBestSNRBySource:
                        case ColumnType::PivotPrecursorMeanSNRByGroup:
                        case ColumnType::PivotPrecursorMeanSNRBySource:
                        {
                            if (findAbstractColumnItr == pivotDataByColumn.end())
                                throw runtime_error("unable to get pivot data for column " + lexical_cast<string>(i));

                            const vector<boost::int64_t>& pivotColumnIds = pivotColumnIdsByAbstractColumn[i];
                            const PivotDataMap& pivotDataMap = findAbstractColumnItr->second;
                            findIdItr = pivotDataMap.find(id);

                            // for the current protein/gene/cluster/whatever, look it up in the pivotDataMap by its id,
                            // then (even if it's not found): for every source or group, output a column for the value
                            // corresponding to that source or group (or 0 if there is no value for that source or group)
                            if (findIdItr == pivotDataMap.end())
                                for (size_t j=0; j < pivotColumnIds.size(); ++j)
                                {
                                    outputStream << 0;
                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                            else
                                for (size_t j=0; j < pivotColumnIds.size(); ++j)
                                {
                                    findPivotColumnItr = findIdItr->second.find(pivotColumnIds[j]);
                                    if (findPivotColumnItr == findIdItr->second.end())
                                        outputStream << 0;
                                    else
                                        outputStream << findPivotColumnItr->second;
                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                                break;
                        }

                        // non-pivot columns
                        default:
                            if (sqlColumn.second == SQLITE_FLOAT)
                                outputStream << row.get<double>(i+1);
                            else
                                outputStream << row.get<int>(i+1);
                            break;
                    }
                    break;
                case SQLITE_BLOB:
                    switch (enumColumns[i].index())
                    {
                        case ColumnType::iTRAQ4plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, itraqMethodIons); break;
                        case ColumnType::iTRAQ8plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, itraqMethodIons); break;
                        case ColumnType::TMT2plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, tmtMethodIons); break;
                        case ColumnType::TMT6plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, tmtMethodIons); break;
                        case ColumnType::TMT10plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, tmtMethodIons); break;
                        case ColumnType::TMT11plex: writeBlobArray<double>(row.get<const void*>(i + 1), outputStream, tmtMethodIons); break;
                        case ColumnType::TMTpro16plex: writeBlobArray<double>(row.get<const void*>(i + 1), outputStream, tmtMethodIons); break;
                            
                        case ColumnType::PivotITRAQByGroup:
                        case ColumnType::PivotITRAQBySource:
                        case ColumnType::PivotTMTByGroup:
                        case ColumnType::PivotTMTBySource:
                        {
                            if (itraqMethodIons.empty() && (enumColumns[i].index() == ColumnType::PivotITRAQByGroup || enumColumns[i].index() == ColumnType::PivotITRAQBySource) ||
                                tmtMethodIons.empty() && (enumColumns[i].index() == ColumnType::PivotTMTByGroup || enumColumns[i].index() == ColumnType::PivotTMTBySource))
                                break;

                            if (findAbstractColumnItr == pivotDataByColumn.end())
                                throw runtime_error("unable to get pivot data for column " + lexical_cast<string>(i));

                            const vector<boost::int64_t>& pivotColumnIds = pivotColumnIdsByAbstractColumn[i];
                            const PivotDataMap& pivotDataMap = findAbstractColumnItr->second;
                            findIdItr = pivotDataMap.find(id);

                            vector<ReporterIon>* reporterIonsPtr = &itraqMethodIons;
                            if (enumColumns[i].index() == ColumnType::PivotTMTByGroup || enumColumns[i].index() == ColumnType::PivotTMTBySource)
                                reporterIonsPtr = &tmtMethodIons;
                            vector<ReporterIon>& reporterIons = *reporterIonsPtr;

                            // reset reference/empty properties for reporter ions
                            if (enumColumns[i].index() == ColumnType::PivotITRAQByGroup || enumColumns[i].index() == ColumnType::PivotTMTByGroup)
                                for (size_t j=0; j < reporterIons.size(); ++j)
                                    reporterIons[j].reference = reporterIons[j].empty = false;

                            // for the current protein/gene/cluster/whatever, look it up in the pivotDataMap by its id,
                            // then (even if it's not found): for every source or group, output a column for the value
                            // corresponding to that source or group (or 0 if there is no value for that source or group)
                            if (findIdItr == pivotDataMap.end())
                                for (size_t j=0; j < pivotColumnIds.size(); ++j)
                                {
                                    const string& groupName = groupNameById[pivotColumnIds[j]];
                                    const vector<string>& sampleNames = isobaricSamplesByGroup[groupName];
                                    if (!sampleNames.empty())
                                    {
                                        if (reporterIons.size() != sampleNames.size())
                                            throw runtime_error("sample name count does not match number of reporter ions");
                                        for (size_t k=0; k < sampleNames.size(); ++k)
                                            if (bal::iequals(sampleNames[k], "Empty"))
                                                reporterIons[k].empty = true, reporterIons[k].reference = false;
                                            else if (bal::iequals(sampleNames[k], "Reference"))
                                                reporterIons[k].reference = true, reporterIons[k].empty = false;
                                            else
                                                reporterIons[k].reference = reporterIons[k].empty = false;
                                    }
                                    writeBlobArray<double>(NULL, outputStream, reporterIons);
                                    
                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                            else
                                for (size_t j=0; j < pivotColumnIds.size(); ++j)
                                {
                                    findPivotColumnItr = findIdItr->second.find(pivotColumnIds[j]);

                                    const string& groupName = groupNameById[pivotColumnIds[j]];
                                    const vector<string>& sampleNames = isobaricSamplesByGroup[groupName];
                                    if (!sampleNames.empty())
                                    {
                                        if (reporterIons.size() != sampleNames.size())
                                            throw runtime_error("sample name count does not match number of reporter ions");
                                        for (size_t k = 0; k < sampleNames.size(); ++k)
                                            if (bal::iequals(sampleNames[k], "Empty"))
                                                reporterIons[k].empty = true, reporterIons[k].reference = false;
                                            else if (bal::iequals(sampleNames[k], "Reference"))
                                                reporterIons[k].reference = true, reporterIons[k].empty = false;
                                            else
                                                reporterIons[k].reference = reporterIons[k].empty = false;
                                    }

                                    if (findPivotColumnItr == findIdItr->second.end())
                                        writeBlobArray<double>(NULL, outputStream, reporterIons);
                                    else
                                        writeBlobArray<double>(boost::get<const void*>(findPivotColumnItr->second), outputStream, reporterIons);

                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                                break;
                        }

                        default: throw runtime_error("unknown enum column type");
                    }
                    break;
                case SQLITE_TEXT:
                    {
                        const char* str = row.get<const char*>(i+1);
                        if (str != NULL) outputStream << str;
                    }
                    break;
                default:
                    throw runtime_error("unknown SQL column type");
            }
        }
        outputStream << '\n';
    }

    outputStream.close();

    return 0;
}


int query(GroupBy groupBy, const vector<string>& args, double ModificationMassRoundToNearest, QuantitationRollupMethod rollupMethod)
{
    vector<bfs::path> filepaths;
    for (size_t i = 3; i < args.size(); ++i)
    {
        size_t oldSize = filepaths.size();
        pwiz::util::expand_pathmask(args[i], filepaths);
        if (filepaths.size() == oldSize)
            cerr << "Filemask or idpDB file \"" << args[i] << "\" does not exist." << endl;
    }

    if (filepaths.empty())
    {
        cerr << "No idpDB files specified. Nothing to do." << endl;
        return 1;
    }

    vector<string> tokens;
    bal::split(tokens, args[2], bal::is_any_of(","));

    int result = 0;
    for(const bfs::path& filepath : filepaths)
    {
        switch (groupBy.index())
        {
            case GroupBy::Gene:
            case GroupBy::GeneGroup:
                if (!Embedder::hasGeneMetadata(filepath.string()))
                {
                    cerr << "Error: cannot group by gene or gene group because \"" << filepath.string() << "\" does not have embedded gene metadata; use idpQonvert to embed gene metadata." << endl;
                    ++result;
                    continue;
                }

            case GroupBy::Protein:
            case GroupBy::ProteinGroup:
            case GroupBy::Cluster:
                result += doQuery<ProteinColumn>(groupBy, filepath, tokens, ModificationMassRoundToNearest, rollupMethod);
                break;

            case GroupBy::DistinctMatch:
            case GroupBy::Peptide:
            case GroupBy::PeptideGroup:
            case GroupBy::Spectrum:
                result += doQuery<PeptideColumn>(groupBy, filepath, tokens, ModificationMassRoundToNearest, rollupMethod);
                break;

            /*(PeptideSpectrumMatch)
            (Spectrum)
            (SpectrumSourceAndGroup)*/

            case GroupBy::Modification:
            case GroupBy::DeltaMass:
            case GroupBy::ModifiedSite:
            case GroupBy::ProteinSite:
                result += doQuery<ModificationColumn>(groupBy, filepath, tokens, ModificationMassRoundToNearest, rollupMethod);
                break;

            default:
                throw runtime_error("unsupported group by mode");
        }
    }

    return result;
}

END_IDPICKER_NAMESPACE


int main(int argc, const char* argv[])
{
    cout << "IDPickerQuery " << idpQuery::Version::str() << " (" << idpQuery::Version::LastModified() << ")\n" << endl;

    string usage = "Usage: idpQuery <group by field> <comma-delimited export column fields> <idpDB filepath> [-ModificationMassRoundToNearest <real (0.0001)] [-QuantitationRollupMethod <Sum|Tukey (Sum)>]\n"
                   "\nExample: idpQuery ProteinGroup Accession,FilteredSpectra,PercentCoverage,iTRAQ4plex data.idpDB\n";
    
    usage += string("\nValid \"group by\" fields: ") + (GroupBy::begin()+1)->str();
    for (GroupBy::const_iterator itr = GroupBy::begin()+2; itr < GroupBy::end(); ++itr)
        usage += string(", ") + itr->str();
    usage += "\nValid \"export column\" fields depend on the \"group by\" field; run idpQuery with no arguments, or with only the group by argument, to see details.\n";

    GroupBy groupBy;
    vector<string> args;
    double ModificationMassRoundToNearest = 0.0001;
    QuantitationRollupMethod rollupMethod = QuantitationRollupMethod::Sum;
    IDPicker::setGroupConcatSeparator(";");
    
    if (argc > 1)
    {
        args.assign(argv, argv+argc);

        for (int i = 0; i < args.size(); ++i)
        {
            if (args[i] == "-ModificationMassRoundToNearest")
            {
                if (i + 1 == args.size())
                    throw pwiz::util::user_error("no value passed for ModificationMassRoundToNearest");
                try
                {
                    ModificationMassRoundToNearest = lexical_cast<double>(args[i + 1]);
                    args.erase(args.begin()+i);
                    args.erase(args.begin()+i);
                    --i;
                }
                catch (bad_lexical_cast&)
                {
                    throw pwiz::util::user_error("invalid value \"" + args[i+1] + "\" for ModificationMassRoundToNearest");
                }
            }
            else if (args[i] == "-QuantitationRollupMethod")
            {
                if (i + 1 == args.size())
                    throw pwiz::util::user_error("no value passed for QuantitationRollupMethod");
                rollupMethod = QuantitationRollupMethod::get_by_name(args[i + 1].c_str()).get_value_or(QuantitationRollupMethod::Invalid);

                if (rollupMethod == QuantitationRollupMethod::Invalid)
                {
                    cerr << "Invalid rollup method \"" << args[i+1] << "\". Valid options are:" << endl;
                    for (QuantitationRollupMethod::const_iterator itr = QuantitationRollupMethod::begin()+1; itr < QuantitationRollupMethod::end(); ++itr)
                        cerr << "  " << itr->str() << endl;
                    return 1;
                }
                args.erase(args.begin() + i);
                args.erase(args.begin() + i);
                --i;
            }
            else if (args[i] == "-GroupSeparator")
            {
                if (i + 1 == args.size())
                    throw pwiz::util::user_error("no value passed for GroupSeparator");

                IDPicker::setGroupConcatSeparator(args[i + 1]);

                args.erase(args.begin() + i);
                args.erase(args.begin() + i);
                --i;
            }
        }

        groupBy = GroupBy::get_by_name(args[1].c_str()).get_value_or(GroupBy::Invalid);
        if (groupBy == GroupBy::Invalid)
        {
            cerr << "Invalid grouping choice \"" << args[1] << "\". Valid options are:" << endl;
            for (GroupBy::const_iterator itr = GroupBy::begin()+1; itr < GroupBy::end(); ++itr)
                cerr << "  " << itr->str() << endl;
            return 1;
        }
        
        //if (groupBy >= GroupBy::Protein && groupBy <= GroupBy::Cluster)
        //usage += 
    }
    else 
    //    usage += "\n\nTo see what export columns are valid with each \"group by\" field, run idpQuery with only the group by field as an argument.";
    {
        usage += "\n\n"
                 "Protein, ProteinGroup, Cluster, Gene, GeneGroup\n"
                 "------------------------------\n";
        for (ProteinColumn::const_iterator itr = ProteinColumn::begin()+1; itr < ProteinColumn::end(); ++itr)
            usage += string("  ") + itr->str() + "\n";

        usage += "\n\n"
                 "DistinctMatch, Peptide, PeptideGroup, Spectrum\n"
                 "------------------------------------\n";
        for (PeptideColumn::const_iterator itr = PeptideColumn::begin()+1; itr < PeptideColumn::end(); ++itr)
            usage += string("  ") + itr->str() + "\n";

        /*usage += "\n\n"
                 "PeptideSpectrumMatch, Spectrum, SpectrumSourceAndGroup\n"
                 "------------------------------------------------------\n";
        for (SpectrumColumn::const_iterator itr = SpectrumColumn::begin()+1; itr < SpectrumColumn::end(); ++itr)
            usage += string("  ") + itr->str() + "\n";*/

        usage += "\n\n"
                 "Modification, DeltaMass, ModifiedSite, ProteinSite\n"
                 "-------------------------------------\n";
        for (ModificationColumn::const_iterator itr = ModificationColumn::begin()+1; itr < ModificationColumn::end(); ++itr)
            usage += string("  ") + itr->str() + "\n";
    }

    if (argc < 4)
    {
        cerr << "Not enough arguments.\n\n" <<
                usage << endl;
        return 1;
    }

    try
    {
        return query(groupBy, args, ModificationMassRoundToNearest, rollupMethod);
    }
    catch (exception& e)
    {
        cerr << "Unhandled exception: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unknown exception." << endl;
    }
    return 1;
}
