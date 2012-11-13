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
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//


#include "SchemaUpdater.hpp"
#include "sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"


using namespace pwiz::util;
namespace sqlite = sqlite3pp;


BEGIN_IDPICKER_NAMESPACE

const int CURRENT_SCHEMA_REVISION = 6;

namespace SchemaUpdater {


namespace {

void update_5_to_6(sqlite::database& db, IterationListenerRegistry* ilr)
{
    // force the basic filters to be reapplied
    db.execute("DROP TABLE IF EXISTS FilteringCriteria");
}

void update_4_to_5(sqlite::database& db, IterationListenerRegistry* ilr)
{
    db.execute("UPDATE SpectrumSource SET QuantitationMethod = IFNULL(QuantitationMethod, 0),"
               "                          TotalSpectraMS1 = IFNULL(TotalSpectraMS1, 0),"
               "                          TotalSpectraMS2 = IFNULL(TotalSpectraMS2, 0),"
               "                          TotalIonCurrentMS1 = IFNULL(TotalIonCurrentMS1, 0),"
               "                          TotalIonCurrentMS2 = IFNULL(TotalIonCurrentMS2, 0)");

    update_5_to_6(db, ilr);
}

void update_3_to_4(sqlite::database& db, IterationListenerRegistry* ilr)
{
    try
    {
        db.execute("CREATE TABLE SpectrumSourceMetadata (Id INTEGER PRIMARY KEY, MsDataBytes BLOB);"
                   "INSERT INTO SpectrumSourceMetadata SELECT Id, MsDataBytes FROM SpectrumSource;"
                   "CREATE TABLE NewSpectrumSource (Id INTEGER PRIMARY KEY, Name TEXT, URL TEXT, Group_ INT, TotalSpectraMS1 INT, TotalIonCurrentMS1 NUMERIC, TotalSpectraMS2 INT, TotalIonCurrentMS2 NUMERIC, QuantitationMethod INT);"
                   "INSERT INTO NewSpectrumSource SELECT Id, Name, URL, Group_, TotalSpectraMS1, TotalIonCurrentMS1, TotalSpectraMS2, TotalIonCurrentMS2, QuantitationMethod FROM SpectrumSource;"
                   "DROP TABLE SpectrumSource;"
                   "ALTER TABLE NewSpectrumSource RENAME TO SpectrumSource;"
                   "DROP TABLE DistinctMatchQuantitation;"
                   "CREATE TABLE DistinctMatchQuantitation (Id TEXT PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);");
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "exists")) // table already exists
            throw runtime_error(e.what());
    }

    update_4_to_5(db, ilr);
}

void update_2_to_3(sqlite::database& db, IterationListenerRegistry* ilr)
{
    // add empty quantitation tables and quantitative columns to SpectrumSource
    db.execute("CREATE TABLE IF NOT EXISTS SpectrumQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "CREATE TABLE IF NOT EXISTS DistinctMatchQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "CREATE TABLE IF NOT EXISTS PeptideQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "CREATE TABLE IF NOT EXISTS ProteinQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "ALTER TABLE SpectrumSource ADD COLUMN TotalSpectraMS1 INT;"
               "ALTER TABLE SpectrumSource ADD COLUMN TotalIonCurrentMS1 NUMERIC;"
               "ALTER TABLE SpectrumSource ADD COLUMN TotalSpectraMS2 INT;"
               "ALTER TABLE SpectrumSource ADD COLUMN TotalIonCurrentMS2 NUMERIC;"
               "ALTER TABLE SpectrumSource ADD COLUMN QuantitationMethod INT;");

    // continue updating schema
    update_3_to_4(db, ilr);
}

void update_1_to_2(sqlite::database& db, IterationListenerRegistry* ilr)
{
    try
    {
        {
            sqlite::query q(db, "SELECT Id FROM UnfilteredSpectrum LIMIT 1");
            q.begin()->get<int>(0);
        }

        // if UnfilteredSpectrum exists, add an empty ScanTimeInSeconds column
        db.execute("CREATE TABLE NewSpectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC, ScanTimeInSeconds NUMERIC);"
                   "INSERT INTO NewSpectrum SELECT Id, Source, Index_, NativeID, PrecursorMZ, 0 FROM UnfilteredSpectrum;"
                   "DROP TABLE UnfilteredSpectrum;"
                   "ALTER TABLE NewSpectrum RENAME TO UnfilteredSpectrum;");
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // column or table
            throw runtime_error(e.what());
    }

    // add an empty ScanTimeInSeconds column
    db.execute("CREATE TABLE NewSpectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC, ScanTimeInSeconds NUMERIC);"
               "INSERT INTO NewSpectrum SELECT Id, Source, Index_, NativeID, PrecursorMZ, 0 FROM Spectrum;"
               "DROP TABLE Spectrum;"
               "ALTER TABLE NewSpectrum RENAME TO Spectrum;");

    // continue updating schema
    update_2_to_3(db, ilr);
}

void update_0_to_1(sqlite::database& db, IterationListenerRegistry* ilr)
{
    db.execute("CREATE TABLE About (Id INTEGER PRIMARY KEY, SoftwareName TEXT, SoftwareVersion TEXT, StartTime DATETIME, SchemaRevision INT);"
               "INSERT INTO About VALUES (1, 'IDPicker', '3.0', datetime('now'), " + lexical_cast<string>(CURRENT_SCHEMA_REVISION) + ");");

    try
    {
        {
            sqlite::query q(db, "SELECT Id FROM UnfilteredProtein LIMIT 1");
            q.begin()->get<int>(0);
        }

        // if UnfilteredProtein exists but UnfilteredSpectrum does not, create the filtered Spectrum table
        db.execute("ALTER TABLE Spectrum RENAME TO UnfilteredSpectrum;"
                   "CREATE TABLE Spectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC);"
                   "INSERT INTO Spectrum SELECT * FROM UnfilteredSpectrum WHERE Id IN (SELECT Spectrum FROM PeptideSpectrumMatch);");
        
        // if UnfilteredProtein exists, replace the UnfilteredPeptideSpectrumMatch's MonoisotopicMass/MolecularWeight columns with a single ObservedNeutralMass column
        try
        {
            sqlite::query q(db, "SELECT ObservedNeutralMass FROM UnfilteredPeptideSpectrumMatch LIMIT 1");
            q.begin()->get<double>(0);
        }
        catch (sqlite::database_error& e)
        {
            if (!bal::contains(e.what(), "no such")) // column or table
                throw runtime_error(e.what());

            db.execute("CREATE TABLE NewPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);"
                       "INSERT INTO NewPeptideSpectrumMatch SELECT Id, Spectrum, Analysis, Peptide, QValue, MonoisotopicMass, MonoisotopicMassError, MolecularWeightError, Rank, Charge FROM UnfilteredPeptideSpectrumMatch;"
                       "DROP TABLE UnfilteredPeptideSpectrumMatch;"
                       "ALTER TABLE NewPeptideSpectrumMatch RENAME TO UnfilteredPeptideSpectrumMatch;");
        }
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // column or table
            throw runtime_error(e.what());
    }

    // replace PeptideSpectrumMatch's MonoisotopicMass/MolecularWeight columns with a single ObservedNeutralMass column
    try
    {
        sqlite::query q(db, "SELECT ObservedNeutralMass FROM PeptideSpectrumMatch LIMIT 1");
        q.begin()->get<double>(0);
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // column or table
            throw runtime_error(e.what());

        db.execute("CREATE TABLE NewPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);"
                   "INSERT INTO NewPeptideSpectrumMatch SELECT Id, Spectrum, Analysis, Peptide, QValue, MonoisotopicMass, MonoisotopicMassError, MolecularWeightError, Rank, Charge FROM PeptideSpectrumMatch;"
                   "DROP TABLE PeptideSpectrumMatch;"
                   "ALTER TABLE NewPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch;");
    }

    // continue updating schema
    update_1_to_2(db, ilr);
}

} // namespace


bool update(const string& idpDbFilepath, IterationListenerRegistry* ilr)
{
    int schemaRevision;
    sqlite::database db(idpDbFilepath);

    try
    {
        sqlite::query q(db, "SELECT SchemaRevision FROM About");
        schemaRevision = q.begin()->get<int>(0);
    }
    catch (sqlite::database_error&)
    {
        schemaRevision = 0;
    }

    if (schemaRevision == 0)
        update_0_to_1(db, ilr);
    else if (schemaRevision == 1)
        update_1_to_2(db, ilr);
    else if (schemaRevision == 2)
        update_2_to_3(db, ilr);
    else if (schemaRevision == 3)
        update_3_to_4(db, ilr);
    else if (schemaRevision == 4)
        update_4_to_5(db, ilr);
    else if (schemaRevision == 5)
        update_5_to_6(db, ilr);
    else if (schemaRevision > CURRENT_SCHEMA_REVISION)
        throw runtime_error("[SchemaUpdater::update] unable to update schema revision " +
                            lexical_cast<string>(schemaRevision) +
                            "; the latest compatible revision is " +
                            lexical_cast<string>(CURRENT_SCHEMA_REVISION));
    else
        return false; // no update needed

    // update the schema revision
    db.execute("UPDATE About SET SchemaRevision = " + lexical_cast<string>(CURRENT_SCHEMA_REVISION));

    return true; // an update was done
}


} // namespace SchemaUpdater
END_IDPICKER_NAMESPACE
