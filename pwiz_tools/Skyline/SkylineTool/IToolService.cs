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
        string GetReport(string toolName, string reportName);
        string GetReportFromDefinition(string reportDefinition);

        DocumentLocation GetDocumentLocation();
        void SetDocumentLocation(DocumentLocation documentLocation);

        string GetDocumentLocationName();
        string GetReplicateName();

        Chromatogram[] GetChromatograms(DocumentLocation documentLocation);
        string GetDocumentPath();
        Version GetVersion();

        void ImportFasta(string fasta);

        void AddSpectralLibrary(string libraryName, string libraryPath);

        void AddDocumentChangeReceiver(string receiverName, string toolName);
        void RemoveDocumentChangeReceiver(string receiverName);
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
            return string.Format("{0},{1},{2},{3}", Major, Minor, Build, Revision); // Not L10N
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
