using System;
using System.Collections.Generic;
using System.Data.SQLite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Database;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib.BlibData;

namespace pwiz.SkylineTestUtil
{
    public class SpectralLibraryTestUtil
    {
        public static IList<DbRefSpectra> GetRefSpectraFromPath(string filename)
        {
            using (var connection = new SQLiteConnection(string.Format("Data Source='{0}';Version=3", filename)))
            {
                connection.Open();
                return GetRefSpectra(connection);
            }
        }

        private static double? ParseNullable(SQLiteDataReader reader, int ordinal)
        {
            if (ordinal < 0)
                return null;
            var str = reader[ordinal].ToString();
            if (string.IsNullOrEmpty(str))
                return null;
            return double.Parse(str);
        }

        public static IList<DbRefSpectra> GetRefSpectra(SQLiteConnection connection)
        {
            var list = new List<DbRefSpectra>();
            var hasAnnotations = SqliteOperations.TableExists(connection, @"RefSpectraPeakAnnotations");
            using var select = new SQLiteCommand(connection);
            select.CommandText = "SELECT * FROM RefSpectra";
            using (var reader = select.ExecuteReader())
            {
                var iAdduct = reader.GetOrdinal("precursorAdduct");
                var iIonMobility = reader.GetOrdinal("ionMobility");
                var iIonMobilityHighEnergyOffset = reader.GetOrdinal("ionMobilityHighEnergyOffset");
                var iCCS = reader.GetOrdinal("collisionalCrossSectionSqA");
                var noMoleculeDetails =
                    reader.GetOrdinal("moleculeName") <
                    0; // Also a cue for presence of chemicalFormula, inchiKey, and otherKeys
                while (reader.Read())
                {
                    var refSpectrum = new DbRefSpectra
                    {
                        PeptideSeq = reader["peptideSeq"].ToString(),
                        PeptideModSeq = reader["peptideModSeq"].ToString(),
                        PrecursorCharge = int.Parse(reader["precursorCharge"].ToString()),
                        PrecursorAdduct = iAdduct < 0 ? string.Empty : reader[iAdduct].ToString(),
                        PrecursorMZ = double.Parse(reader["precursorMZ"].ToString()),
                        RetentionTime = double.Parse(reader["retentionTime"].ToString()),
                        IonMobility = ParseNullable(reader, iIonMobility),
                        IonMobilityHighEnergyOffset = ParseNullable(reader, iIonMobilityHighEnergyOffset),
                        CollisionalCrossSectionSqA = ParseNullable(reader, iCCS),
                        MoleculeName = noMoleculeDetails ? string.Empty : reader["moleculeName"].ToString(),
                        ChemicalFormula = noMoleculeDetails ? string.Empty : reader["chemicalFormula"].ToString(),
                        InChiKey = noMoleculeDetails ? string.Empty : reader["inchiKey"].ToString(),
                        OtherKeys = noMoleculeDetails ? string.Empty : reader["otherKeys"].ToString(),
                        NumPeaks = ushort.Parse(reader["numPeaks"].ToString())
                    };
                    if (hasAnnotations)
                    {
                        var id = int.Parse(reader["id"].ToString());
                        var annotations = GetRefSpectraPeakAnnotations(connection, refSpectrum, id);
                        refSpectrum.PeakAnnotations = annotations;
                    }

                    list.Add(refSpectrum);
                }

                return list;
            }
        }

        private static IList<DbRefSpectraPeakAnnotations> GetRefSpectraPeakAnnotations(SQLiteConnection connection,
            DbRefSpectra refSpectrum, int refSpectraId)
        {
            var list = new List<DbRefSpectraPeakAnnotations>();
            using var select = new SQLiteCommand(connection);
            select.CommandText = "SELECT * FROM RefSpectraPeakAnnotations WHERE RefSpectraId = " + refSpectraId;
            using (var reader = select.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new DbRefSpectraPeakAnnotations
                    {
                        RefSpectra = refSpectrum,
                        Id = int.Parse(reader["id"].ToString()),
                        PeakIndex = int.Parse(reader["peakIndex"].ToString()),
                        Charge = int.Parse(reader["charge"].ToString()),
                        Adduct = reader["adduct"].ToString(),
                        mzTheoretical = double.Parse(reader["mzTheoretical"].ToString()),
                        mzObserved = double.Parse(reader["mzObserved"].ToString()),
                        Name = reader["name"].ToString(),
                        Formula = reader["formula"].ToString(),
                        InchiKey = reader["inchiKey"].ToString(),
                        OtherKeys = reader["otherKeys"].ToString(),
                        Comment = reader["comment"].ToString(),
                    });
                }

                return list;
            }
        }

        public static void CheckRefSpectra(IList<DbRefSpectra> spectra, string peptideSeq, string peptideModSeq,
            int precursorCharge, double precursorMz, ushort numPeaks, double rT, IonMobilityAndCCS im = null)
        {
            for (var i = 0; i < spectra.Count; i++)
            {
                var spectrum = spectra[i];
                if (spectrum.PeptideSeq.Equals(peptideSeq) &&
                    spectrum.PeptideModSeq.Equals(peptideModSeq) &&
                    spectrum.PrecursorCharge.Equals(precursorCharge) &&
                    Math.Abs((spectrum.RetentionTime ?? 0) - rT) < 0.001 &&
                    Math.Abs(spectrum.PrecursorMZ - precursorMz) < 0.001 &&
                    spectrum.NumPeaks.Equals(numPeaks))
                {
                    spectra.RemoveAt(i);
                    return;
                }
            }

            Assert.Fail("{0} [{1}], precursor charge {2}, precursor m/z {3}, RT {4} with {5} peaks not found",
                peptideSeq, peptideModSeq, precursorCharge, precursorMz, rT, numPeaks);
        }
    }
}