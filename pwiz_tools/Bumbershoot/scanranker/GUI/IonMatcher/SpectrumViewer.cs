//
// $Id: Types.cs 1599 2009-12-04 01:35:39Z brendanx $
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.CLI.msdata;
using pwiz.CLI.proteome;
using System.Text;

namespace IonMatcher
{
    #region spectrum cache
    /// <summary>
    /// This class stores the most recently used objects.
    /// http://www.informit.com/guides/content.aspx?g=dotnet&seqNum=626
    /// </summary>
    public class MRU
    {
        Dictionary<object, object> cache = new Dictionary<object, object>();

        /// <summary>
        /// Keeps up with the most recently read items.
        /// Items at the end of the list were read last. 
        /// Items at the front of the list have been the most idle.
        /// Items at the front are removed if the cache capacity is reached.
        /// </summary>
        List<object> priority = new List<object>();
        public Type Type { get; set; }
        public MRU(Type type)
        {
            this.Type = type;
        }
    
        public object this[object key]
        {
            get
            {
                lock (this)
                {
                    if (!cache.ContainsKey(key)) return null;
                    //move the item to the end of the list                    
                    priority.Remove(key);
                    priority.Add(key);
                    return cache[key];
                }
            }
            set
            {
                lock (this)
                {
                    if (Capacity > 0 && cache.Count == Capacity)
                    {
                        cache.Remove(priority[0]);
                        priority.RemoveAt(0);
                    }
                    cache[key] = value;
                    priority.Remove(key);
                    priority.Add(key);

                    if (priority.Count != cache.Count)
                        throw new Exception("Capacity mismatch.");
                }
            }
        }
        public int Count { get { return cache.Count; } }
        public int Capacity { get; set; }

        public void Clear()
        {
            lock (this)
            {
                priority.Clear();
                cache.Clear();
            }
        }
    }

    /// <summary>
    /// This class maps a datasource name to the datasource object.
    /// It keeps 30 most used datasources in cache, preventing pwiz
    /// to re-read each datasource index every time it's accessed.
    /// </summary>
    public static class DataSourceCache
    {
        public static MRU cache;

        static DataSourceCache()
        {
            cache = new MRU(typeof(string));
            cache.Capacity = 30;
        }
        
        /// <summary>
        /// This function checks the exisiting cache for the data source.
        /// It will create a new one, inserts it into the cache before
        /// returning it.
        /// </summary>
        /// <param name="name">Name of the datasource</param>
        /// <returns>SeeMS ManagedDataSource</returns>
        public static ManagedDataSource getDataSource(string name)
        {
            try {
                ManagedDataSource src = (ManagedDataSource) cache[name];
                if(src == null)
                {
                    src = new ManagedDataSource(new SpectrumSource(name));
                    cache[name] = src;
                }
                return src;
            } catch(Exception e)
            {
                MessageBox.Show(e.ToString() + "\n" + e.StackTrace);
            }
            return null;
        }
    }

    /// <summary>
    /// This class maps a spectrum name to the MassSpectrum object.
    /// It keeps 1000 most used spectra in cache, preventing pwiz
    /// to re-read each spectrum every time it's accessed.
    /// </summary>
    public static class SpectrumCache
    {
        public static MRU cache;

        static SpectrumCache()
        {
            cache = new MRU(typeof(string));
            cache.Capacity = 1000;
        }

        /// <summary>
        /// This function checks the exisiting cache for the spectrum.
        /// It will create a new one, inserts it into the cache before
        /// returning it.
        /// </summary>
        /// <param name="name">Name of the datasource</param>
        /// <param name="index">index of the spectrum</param>
        /// <returns>SeeMS MassSpectrum</returns>
        public static MassSpectrum GetMassSpectrum(string name, object index)
        {
            try {
                var spectrum = (MassSpectrum)cache[name+index.ToString()];
                if (spectrum == null)
                {
                    // Get the datasource and retrieve the spectrum.
                    var dataSource = DataSourceCache.getDataSource(name);
                    if(dataSource == null)
                        return null;
                    spectrum = dataSource.GetMassSpectrum(index);
                    cache[name+index.ToString()] = spectrum; 
                }
                return spectrum;
            } catch(Exception e)
            {
                MessageBox.Show(e.StackTrace + "\n" + e.ToString());
            }
            return null;
        }
    }
    #endregion spectrum cache
     
    /// <summary>
    /// This class holds the mass spectrum and its corresponding annotation objects.
    /// It also defines the call back that refreshes the viewer when ever any changes
    /// are made to the annotation.
    /// </summary>
    public class SpectrumViewer
    {
        // Mass Spectrum and its corresponding annotation
        MassSpectrum spectrum;
        PeptideFragmentationAnnotation annotation;
        // Graph control that shows the spectrum
        MSGraphControl graph;
        // Panels that house the spectrum viewer components
        public Panel spectrumPanel;
        public Panel annotationPanel;
        public Panel fragmentationPanel;

        public SpectrumViewer(string filename, object index, string interpretation)
        {
            // Prepare the annotation
            annotation = new PeptideFragmentationAnnotation(interpretation, 1, 2, false, true, false, false, true, false, false, true, false, true);
            annotation.OptionsPanel.Dock = DockStyle.None;
            annotation.OptionsPanel.Dock = DockStyle.Fill;

            // Get the mass spectrum
            spectrum = SpectrumCache.GetMassSpectrum(filename,index);
            if(spectrum == null)
                return;

            // Add annotation to the mass spectrum and get a new graph control
            spectrum.AnnotationList.Clear();
            spectrum.AnnotationList.Add((IAnnotation)annotation);
            graph = new MSGraphControl();
            graph.AddGraphItem(graph.GraphPane, spectrum);
            graph.Dock = DockStyle.Fill;
  

            // Create new panels and add the graph and annotations
            spectrumPanel = new Panel();
            spectrumPanel.Controls.Add(graph);
            spectrumPanel.Dock = DockStyle.None;
            spectrumPanel.Dock = DockStyle.Fill;
            annotationPanel = new Panel();
            annotationPanel.Controls.Add(annotation.OptionsPanel);
            annotationPanel.Dock = DockStyle.None;
            annotationPanel.Dock = DockStyle.Fill;
            fragmentationPanel = new Panel();
            annotation.FragmentInfoGridView.Location = new Point(0,0);
            annotation.FragmentInfoGridView.ScrollBars = ScrollBars.Both;
            annotation.FragmentInfoGridView.Dock = DockStyle.None;
            annotation.FragmentInfoGridView.Dock = DockStyle.Fill;
            annotation.FragmentInfoGridView.BorderStyle = BorderStyle.FixedSingle;
            fragmentationPanel.Controls.Add(annotation.FragmentInfoGridView);
            fragmentationPanel.Dock = DockStyle.None;
            fragmentationPanel.Dock = DockStyle.Fill;

            // Add the call back for refreshing
            annotation.OptionsChanged += new EventHandler(OnOptionsChanged);
        }

        public void setSecondarySequence(string interp)
        {
            annotation.enableSecondarySequenceDisplay(interp);
        }

        // Call back function that refreshes the panels when annotations 
        // are changed.
        public void OnOptionsChanged(object sender, EventArgs e)
        {
            spectrumPanel.Refresh();
            annotationPanel.Refresh();
        }

        public void deltaScoreTest()
        {
            Spectrum rawSpectrum = spectrum.Element;
            // Work around to have a sort function in List.
            ZedGraph.IPointList peaks = spectrum.Points;
            List<ScoringUtils.Peak> rawPeaks = new List<ScoringUtils.Peak>();
            for (int i = 0; i < peaks.Count; ++i)
                rawPeaks.Add(new ScoringUtils.Peak(peaks[i].X, peaks[i].Y));
            // Remove the precursor and associated neutral loss peaks
            double precursorMZ = rawSpectrum.precursors[0].selectedIons[0].cvParam(pwiz.CLI.cv.CVID.MS_selected_ion_m_z).value;
            ScoringUtils.erasePrecursorIons(precursorMZ, ref rawPeaks);

            // Filter the peaks and rank them based on increasing 
            // order of intensity: i.e. intense peaks get higher ranks 
            rawPeaks = ScoringUtils.filterByPeakCount(rawPeaks, 100);
            Set<ScoringUtils.Peak> rankedPeaks = new Set<ScoringUtils.Peak>(rawPeaks);
            //Set<Peak> rankedPeaks = rankPeaks(rawPeaks);

            //string alt = "";
            //InputBox("Alternative", "Enter Alt peptide", ref alt);
            Peptide alternative;
            try
            {
                alternative = new Peptide(annotation.SecondarySequence,
                    pwiz.CLI.proteome.ModificationParsing.ModificationParsing_Auto,
                    pwiz.CLI.proteome.ModificationDelimiter.ModificationDelimiter_Brackets);
            }
            catch (Exception) { return; }

            Set<double> primaryMatchFragMasses = new Set<double>();
            ScoringUtils.calculateSequenceIons(annotation.Peptide, 2, ref primaryMatchFragMasses);
            Set<double> secondaryMatchFragMasses = new Set<double>();
            ScoringUtils.calculateSequenceIons(alternative, 2, ref secondaryMatchFragMasses);

            Set<ScoringUtils.Peak> primarySeqMatchedIons = new Set<ScoringUtils.Peak>();
            double primarySeqMatchedIntens = 0.0;
            foreach (var peak in primaryMatchFragMasses)
            {
                ScoringUtils.Peak match = ScoringUtils.findNear(rankedPeaks, peak, 0.5);
                if (match != null)
                {
                    primarySeqMatchedIons.Add(match);
                    primarySeqMatchedIntens += match.rankOrIntensity;
                }
            }
            Set<ScoringUtils.Peak> secondarySeqMatchedIons = new Set<ScoringUtils.Peak>();
            double secondarySeqMatchedIntens = 0;
            foreach (var peak in secondaryMatchFragMasses)
            {
                ScoringUtils.Peak match = ScoringUtils.findNear(rankedPeaks, peak, 0.5);
                if (match != null)
                {
                    secondarySeqMatchedIons.Add(match);
                    secondarySeqMatchedIntens += match.rankOrIntensity;
                }
            }
            double TIC = 0.0;
            foreach (var peak in rankedPeaks)
                TIC += peak.rankOrIntensity;
            MessageBox.Show("PRS:" + primarySeqMatchedIntens + " SRS:" + secondarySeqMatchedIntens + " TIC:" + TIC);
        }
    }
}
