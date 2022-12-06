/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Drawing;
using System.Linq;

namespace SkylineTool
{
    /// <summary>
    /// IToolService is the main interface for interactive tools to communicate
    /// with the instance of Skyline that started the tool.
    /// </summary>
    public interface IToolService
    {
        /// <summary>
        /// Returns a report as CSV
        /// </summary>
        /// <param name="toolName">Name of the tool requesting the report. This is used when displaying progress in the Skyline UI.</param>
        /// <param name="reportName">Name of the report being requested</param>
        /// <returns>The contents of the report as CSV text</returns>
        string GetReport(string toolName, string reportName);
        /// <summary>
        /// Returns a report as CSV
        /// </summary>
        /// <param name="reportDefinition">XML definition of the report. The XML is in the same format as an exported .skyr file. The root element of the report is "&lt;views>".</param>
        /// <returns>The contents of the report as CSV text</returns>
        string GetReportFromDefinition(string reportDefinition);

        [Obsolete ("Use GetSelectedElementLocator")]
        DocumentLocation GetDocumentLocation();
        [Obsolete]
        void SetDocumentLocation(DocumentLocation documentLocation);

        string GetDocumentLocationName();
        string GetReplicateName();

        [Obsolete("Use GetReportFromDefinition and query the Chromatogram field of Transition Results")]
        Chromatogram[] GetChromatograms(DocumentLocation documentLocation);
        /// <summary>
        /// Returns the path of the Skyline document or null if the document has not been saved.
        /// </summary>
        string GetDocumentPath();
        /// <summary>
        /// Returns the version of Skyline.
        /// </summary>
        Version GetVersion();

        void ImportFasta(string fasta);

        void InsertSmallMoleculeTransitionList(string csvText); // Accepts CSV data using headers as found in the Skyline UI's Insert Transition List dialog

        void AddSpectralLibrary(string libraryName, string libraryPath);

        void AddDocumentChangeReceiver(string receiverName, string toolName);
        void RemoveDocumentChangeReceiver(string receiverName);
        int GetProcessId();
        void DeleteElements(string[] elementLocators);
        void ImportProperties(string csvText);
        string GetSelectedElementLocator(string elementType);
    }

    public interface IDocumentChangeReceiver
    {
        void DocumentChanged();
        void SelectionChanged();
    }

    [Serializable]
    public class Version
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Build { get; set; }
        public int Revision { get; set; }

        public override string ToString()
        {
            return string.Format(@"{0},{1},{2},{3}", Major, Minor, Build, Revision);
        }

        private bool Equals(Version other)
        {
            return Major == other.Major && Minor == other.Minor && Build == other.Build && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Version)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Major;
                hashCode = (hashCode * 397) ^ Minor;
                hashCode = (hashCode * 397) ^ Build;
                hashCode = (hashCode * 397) ^ Revision;
                return hashCode;
            }
        }
    }

    [Serializable]
    [Obsolete]
    public class Chromatogram
    {
        public double PrecursorMz { get; set; }
        public double ProductMz { get; set; }
        public float[] Times { get; set; }
        public float[] Intensities { get; set; }
        public Color Color { get; set; }

        private bool Equals(Chromatogram other)
        {
            return 
                PrecursorMz.Equals(other.PrecursorMz) &&
                ProductMz.Equals(other.ProductMz) &&
                Times.SequenceEqual(other.Times) &&
                Intensities.SequenceEqual(other.Intensities) &&
                Color.Equals(other.Color);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Chromatogram)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = PrecursorMz.GetHashCode();
                hashCode = (hashCode * 397) ^ ProductMz.GetHashCode();
                hashCode = (hashCode * 397) ^ Times.GetHashCode();
                hashCode = (hashCode * 397) ^ Intensities.GetHashCode();
                hashCode = (hashCode * 397) ^ Color.GetHashCode();
                return hashCode;
            }
        }
    }
}
