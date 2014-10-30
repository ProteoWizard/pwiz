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

using System.IO;
using System.Linq;

namespace SkylineTool
{
    /// <summary>
    /// IToolService is the main interface for interactive tools to communicate
    /// with the instance of Skyline that started the tool.
    /// </summary>
    public interface IToolService
    {
        string GetReport(string toolReportName);
        void Select(string link);
        string GetDocumentPath();
        Version GetVersion();
        void AddDocumentChangeReceiver(string receiverName);
        void RemoveDocumentChangeReceiver(string receiverName);
    }

    public interface IDocumentChangeReceiver
    {
        void DocumentChanged();
    }

    public class Version : IStreamable
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Build { get; set; }
        public int Revision { get; set; }

        public void Read(BinaryReader reader)
        {
            Major = reader.ReadInt32();
            Minor = reader.ReadInt32();
            Build = reader.ReadInt32();
            Revision = reader.ReadInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Major);
            writer.Write(Minor);
            writer.Write(Build);
            writer.Write(Revision);
        }

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
            if (obj.GetType() != this.GetType()) return false;
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

    public class Chromatogram : IStreamable
    {
        public double Mz { get; set; }
        public float[] Times { get; set; }
        public float[] Intensities { get; set; }

        public void Read(BinaryReader reader)
        {
            Mz = reader.ReadDouble();
            int length = reader.ReadInt32();
            Times = new float[length];
            for (int i = 0; i < length; i++)
                Times[i] = reader.ReadSingle();
            Intensities = new float[length];
            for (int i = 0; i < length; i++)
                Intensities[i] = reader.ReadSingle();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Mz);
            writer.Write(Times.Length);
            foreach (var time in Times)
                writer.Write(time);
            foreach (var intensity in Intensities)
                writer.Write(intensity);
        }

        private bool Equals(Chromatogram other)
        {
            return Mz.Equals(other.Mz) && Times.SequenceEqual(other.Times) && Intensities.SequenceEqual(other.Intensities);
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
                int hashCode = Mz.GetHashCode();
                hashCode = (hashCode * 397) ^ Times.GetHashCode();
                hashCode = (hashCode * 397) ^ Intensities.GetHashCode();
                return hashCode;
            }
        }
    }
}
