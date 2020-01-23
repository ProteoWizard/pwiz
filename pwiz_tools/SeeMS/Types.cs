//
// $Id$
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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.Common.Collections;

public class Pair<T1, T2>
{
	public Pair()
	{
		this.first = default(T1);
		this.second = default(T2);
	}

	public Pair( T1 first, T2 second )
	{
		this.first = first;
		this.second = second;
	}

	public T1 first;
	public T2 second;
}

public class RefPair<T1, T2>
	//where T1 : new()
	//where T2 : new()
{
	/*public RefPair()
	{
		this.first = new T1();
		this.second = new T2();
	}*/

	public RefPair()
	{
		this.first = default(T1);
		this.second = default(T2);
	}

	public RefPair( T1 first, T2 second )
	{
		this.first = first;
		this.second = second;
	}

	public T1 first;
	public T2 second;
}

namespace seems
{
	public class PointDataMap<T> : Map< double, T >
		where T: new()
	{
        public Enumerator FindNear( double x, double tolerance )
		{
			Enumerator cur, min, max;

			min = LowerBound( x - tolerance );
			max = LowerBound( x + tolerance );

			if( !min.IsValid || ( max.IsValid && min.Current == max.Current ) )
				return null; // no peaks

			MutableKeyValuePair<double, T> best = min.Current;
            Enumerator bestEnum = (Enumerator) min.Clone();

			// find the peak closest to the desired mz
			double minDiff = Math.Abs( x - best.Key );
			cur = min;
			while( true )
			{
				double curDiff = Math.Abs( x - cur.Current.Key );
				if( curDiff < minDiff )
				{
					minDiff = curDiff;
					best = cur.Current;
                    bestEnum = (Enumerator) cur.Clone();
				}
				cur.MoveNext();
				if( !cur.IsValid || (max.IsValid && cur.Current == max.Current) )
					break;
			}

            return bestEnum;
			//return new MapPair( best.Key, best.Value );
		}
	}

    public class PointMap : PointDataMap<double>
    {
        public PointMap()
		{
		}

        public PointMap( ZedGraph.IPointList pointList )
        {
            for( int i = 0; i < pointList.Count; ++i )
                Add( pointList[i].X, pointList[i].Y );
        }
    }

    public enum MatchToleranceUnits
	{
		Seconds = 0,
		Minutes,
		Hours,
		Daltons,
		PPM,
		ResolvingPower
	}

    public interface IDataView
    {
        IList<ManagedDataSource> Sources { get; }
        IList<GraphItem> DataItems { get; }
    }

	public abstract class GraphItem : IComparable<GraphItem>, pwiz.MSGraph.IMSGraphItemInfo
	{
	    protected GraphItem()
        {
            dataProcessing = new DataProcessing();
            annotationList = new List<IAnnotation>();
            processingList = new List<IProcessing>();
            //processingList = new ObservableCollection<IProcessing>();
            //(processingList as ObservableCollection<IProcessing>).CollectionChanged += OnCollectionChanged;
            AddSourceToId = false;
        }

        /*private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
        }*/

        protected string id;
        public string Id { get { return id; } }

        protected int index;
        public int Index { get { return index; } }

        protected ManagedDataSource source;
        public ManagedDataSource Source { get { return source; } }

        protected DataProcessing dataProcessing;
        public DataProcessing DataProcessing { get { return dataProcessing; } set { dataProcessing = value; } }

        protected IList<IProcessing> processingList;
        public IList<IProcessing> ProcessingList { get { return processingList; } set { processingList = value; } }

        protected IList<IAnnotation> annotationList;
        public IList<IAnnotation> AnnotationList { get { return annotationList; } set { annotationList = value; } }

        protected double totalIntegratedArea;
        public double TotalIntegratedArea { get { return totalIntegratedArea; } set { totalIntegratedArea = value; } }

        public bool IsChromatogram { get { return this is Chromatogram; } }
        public bool IsMassSpectrum { get { return this is MassSpectrum; } }

        public int CompareTo( GraphItem other )
        {
            return String.Compare( id, other.id, StringComparison.Ordinal );
        }

        public override string ToString()
        {
            return String.Format("{0}/{1}", source.Source.Name, Id);
        }

        public object Tag;

        public virtual pwiz.MSGraph.MSGraphItemType GraphItemType
        {
            get
            {
                if( IsChromatogram )
                    return pwiz.MSGraph.MSGraphItemType.chromatogram;
                else
                    return pwiz.MSGraph.MSGraphItemType.spectrum;
            }
        }

        public virtual string Title { get { return AddSourceToId ? ToString() : Id; } }
        public virtual Color Color { get { return Color.Gray; } }
        public virtual float LineWidth { get { return ZedGraph.LineBase.Default.Width; } }
	    public bool AddSourceToId { get; set; }

	    public virtual void CustomizeXAxis( ZedGraph.Axis axis )
        {
            axis.Title.FontSpec.Family = "Arial";
            axis.Title.FontSpec.Size = 14;
            axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
            axis.Title.FontSpec.Border.IsVisible = false;

            if( IsChromatogram )
            {
                axis.Title.Text = "Time";
            } else
            {
                axis.Title.Text = "m/z";
            }
        }

        public virtual void CustomizeYAxis( ZedGraph.Axis axis )
        {
            axis.Title.FontSpec.Family = "Arial";
            axis.Title.FontSpec.Size = 14;
            axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
            axis.Title.FontSpec.Border.IsVisible = false;

            if( IsChromatogram )
            {
                axis.Title.Text = "Total Intensity";
            } else
            {
                axis.Title.Text = "Intensity";
            }
        }

        public virtual pwiz.MSGraph.MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return pwiz.MSGraph.MSGraphItemDrawMethod.stick; }
        }

        private AnnotationSettings annotationSettings;
        public AnnotationSettings AnnotationSettings
        {
            get { return annotationSettings; }
            set { annotationSettings = value; }
        }

        public virtual pwiz.MSGraph.PointAnnotation AnnotatePoint( ZedGraph.PointPair point )
        {
            if( annotationSettings != null )
            {
                Map<double, SeemsPointAnnotation>.Enumerator itr = annotationSettings.PointAnnotations.Find( point.X );
                if( itr.IsValid && itr.Current.Value != null )
                    return new pwiz.MSGraph.PointAnnotation( itr.Current.Value.Label );

                return annotationSettings.AnnotatePoint( point );
            }

            return null;
        }

        public virtual void AddAnnotations( pwiz.MSGraph.MSGraphPane graphPane, Graphics g, pwiz.MSGraph.MSPointList pointList, ZedGraph.GraphObjList annotations )
        {
            PointMap points = new PointMap( Points );
            foreach( IAnnotation annotation in annotationList )
                annotation.Update( this, pointList, annotations );
        }

        public virtual ZedGraph.IPointList Points { get { return null; } }
    }

	public class Chromatogram : GraphItem
	{
		public Chromatogram( ManagedDataSource source, int index, ChromatogramList chromatogramList )
		{
            this.source = source;
            this.chromatogramList = chromatogramList;
            this.index = index;
			//element = chromatogram;
			id = Element.id;
            //index = element.index;
		}

        public Chromatogram( Chromatogram metaChromatogram, pwiz.CLI.msdata.Chromatogram chromatogram )
        {
            source = metaChromatogram.source;
            this.chromatogramList = metaChromatogram.chromatogramList;
            this.index = chromatogram.index;
            Tag = metaChromatogram.Tag;
            AnnotationSettings = metaChromatogram.AnnotationSettings;
            //element = chromatogram;
            id = Element.id;
        }

        private ChromatogramList chromatogramList;
        public ChromatogramList ChromatogramList
        {
            get { return chromatogramList; }
            set { chromatogramList = value; }
        }

        // cache the most recently accessed element and the chromatogram list used to get it
        // note: this breaks if you access Element from a "using" block (but why?)
        private static pwiz.CLI.msdata.ChromatogramList lastSpectrumListUsed = null;
        private static pwiz.CLI.msdata.Chromatogram lastElementAccessed = null;

        public pwiz.CLI.msdata.Chromatogram Element
        {
            get
            {
                // retrieve cached element if it's still valid
                if( lastSpectrumListUsed == null ||
                    lastElementAccessed == null ||
                    !ReferenceEquals( chromatogramList, lastSpectrumListUsed ) ||
                    index != lastElementAccessed.index )
                {
                    lastSpectrumListUsed = chromatogramList;
                    lastElementAccessed = chromatogramList.chromatogram( index );
                }
                return lastElementAccessed;
            }
        }

        public ChromatogramListForm OwningListForm { get { return source.ChromatogramListForm; } }

        public override void AddAnnotations( pwiz.MSGraph.MSGraphPane graphPane, Graphics g, pwiz.MSGraph.MSPointList pointList, ZedGraph.GraphObjList annotations )
        {
            base.AddAnnotations( graphPane, g, pointList, annotations );
        }

		public override ZedGraph.IPointList Points
		{
            get
            {
                using( pwiz.CLI.msdata.Chromatogram element = chromatogramList.chromatogram( index, true ) )
                {
                    return new ZedGraph.PointPairList( element.binaryDataArrays[0].data, element.binaryDataArrays[1].data );
                }
            }
		}

        public override pwiz.MSGraph.MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return pwiz.MSGraph.MSGraphItemDrawMethod.line; }
        }
	}

	public class MassSpectrum : GraphItem
	{
        public MassSpectrum( ManagedDataSource source, int index, SpectrumList spectrumList )
        {
            this.source = source;
            this.spectrumList = spectrumList;
            this.index = index;
            //element = spectrum;
            //using( Spectrum element = Element )
            {
                id = Element.id;
                AbbreviatedId = pwiz.CLI.msdata.id.abbreviate(id);
            }
        }

        public MassSpectrum( MassSpectrum metaSpectrum, pwiz.CLI.msdata.Spectrum spectrum )
        {
            source = metaSpectrum.source;
            spectrumList = metaSpectrum.spectrumList;
            index = metaSpectrum.index;
            Tag = metaSpectrum.Tag;
            AnnotationSettings = metaSpectrum.AnnotationSettings;
            //element = spectrum;
            id = metaSpectrum.id;
            AbbreviatedId = metaSpectrum.AbbreviatedId;
        }

	    public string AbbreviatedId { get; private set; }
        public override string Title { get { return AddSourceToId ? ToString() : AbbreviatedId; } }
        public override string ToString() { return String.Format("{0}/{1}", source.Source.Name, AbbreviatedId); }

        private SpectrumList spectrumList;
        public SpectrumList SpectrumList
        {
            get { return spectrumList; }
            set { spectrumList = value; }
        }

        // cache the most recently accessed element and the spectrum list used to get it
        // note: this breaks if you access Element from a "using" block (but why?)
        private static pwiz.CLI.msdata.SpectrumList lastSpectrumListUsed = null;
		private static pwiz.CLI.msdata.Spectrum lastElementAccessed = null;

        public pwiz.CLI.msdata.Spectrum Element
        {
            get
            {
                // retrieve cached element if it's still valid
                if( lastSpectrumListUsed == null ||
                    lastElementAccessed == null ||
                    !ReferenceEquals( spectrumList, lastSpectrumListUsed ) ||
                    index != lastElementAccessed.index )
                {
                    lastSpectrumListUsed = spectrumList;
                    lastElementAccessed = spectrumList.spectrum( index, DetailLevel.FullMetadata );
                }
                return lastElementAccessed;
            }
        }

        public SpectrumListForm OwningListForm { get { return source.SpectrumListForm; } }

        /// <summary>
        /// add precursor and non-matched annotations
        /// </summary>
        public override void AddAnnotations( pwiz.MSGraph.MSGraphPane graphPane, Graphics g, pwiz.MSGraph.MSPointList pointList, ZedGraph.GraphObjList annotations )
        {
            base.AddAnnotations( graphPane, g, pointList, annotations );
            //using( Spectrum element = Element )
            {
                foreach( Precursor p in Element.precursors )
                    foreach( SelectedIon si in p.selectedIons )
                    {
                        double precursorMz = (double) si.cvParam( CVID.MS_selected_ion_m_z ).value;
                        int precursorCharge = 0;
                        CVParam precursorChargeParam = si.cvParam( CVID.MS_charge_state );
                        if( precursorChargeParam.empty() )
                            precursorChargeParam = si.cvParam( CVID.MS_possible_charge_state );
                        if( !precursorChargeParam.empty() )
                            precursorCharge = (int) precursorChargeParam.value;


                        double stickLength = 0.1;
                        ZedGraph.LineObj stickOverlay = new ZedGraph.LineObj( precursorMz, 1, precursorMz, stickLength );
                        stickOverlay.Location.CoordinateFrame = ZedGraph.CoordType.XScaleYChartFraction;
                        stickOverlay.Line.Width = 3;
                        stickOverlay.Line.Style = System.Drawing.Drawing2D.DashStyle.Dot;
                        stickOverlay.Line.Color = Color.Green;

                        annotations.Add( stickOverlay );

                        // Create a text label from the X data value
                        string precursorLabel;
                        if( precursorCharge > 0 )
                            precursorLabel = String.Format( "{0}\n(+{1} precursor)", precursorMz.ToString( "f3" ), precursorCharge );
                        else
                            precursorLabel = String.Format( "{0}\n(precursor of unknown charge)", precursorMz.ToString( "f3" ) );
                        ZedGraph.TextObj text = new ZedGraph.TextObj( precursorLabel, precursorMz, stickLength,
                            ZedGraph.CoordType.XScaleYChartFraction, ZedGraph.AlignH.Center, ZedGraph.AlignV.Bottom );
                        text.ZOrder = ZedGraph.ZOrder.A_InFront;
                        text.FontSpec.FontColor = stickOverlay.Line.Color;
                        text.FontSpec.Border.IsVisible = false;
                        text.FontSpec.Fill.IsVisible = false;
                        //text.FontSpec.Fill = new Fill( Color.FromArgb( 100, Color.White ) );
                        text.FontSpec.Angle = 0;
                        annotations.Add( text );
                    }
            }
        }

		public override ZedGraph.IPointList Points
		{
            get
            {
                using( Spectrum element = spectrumList.spectrum( index, true ) )
                {
                    if (element.defaultArrayLength == 0)
                        return new ZedGraph.PointPairList();

                    IList<double> mzArray = element.getMZArray().data.Storage();
                    IList<double> intensityArray = element.getIntensityArray().data.Storage();

                    // only sort centroid spectra; profile spectra are assumed to already be sorted
                    if (element.hasCVParam(CVID.MS_centroid_spectrum) || element.id.StartsWith("merged="))
                    {
                        mzArray.Sort(intensityArray);

                        if (element.id.StartsWith("merged="))
                        {
                            var uniqueMz = new List<double>(mzArray.Count);
                            var summedIntensity = new List<double>(mzArray.Count);
                            uniqueMz.Add(mzArray[0]);
                            summedIntensity.Add(intensityArray[0]);
                            for (int i = 1; i < mzArray.Count; ++i)
                            {
                                if (mzArray[i] == uniqueMz[uniqueMz.Count - 1])
                                    summedIntensity[uniqueMz.Count - 1] += intensityArray[i];
                                else
                                {
                                    uniqueMz.Add(mzArray[i]);
                                    summedIntensity.Add(intensityArray[i]);
                                }
                            }

                            mzArray = uniqueMz;
                            intensityArray = summedIntensity;
                        }
                    }

                    return new ZedGraph.PointPairList(mzArray, intensityArray);
                }
            }
		}

        public override pwiz.MSGraph.MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get
            {
                CVParam representation = Element.cvParamChild(CVID.MS_spectrum_representation);
                if( !representation.empty() && representation.cvid == CVID.MS_profile_spectrum )
                    return pwiz.MSGraph.MSGraphItemDrawMethod.line;
                else
                    return pwiz.MSGraph.MSGraphItemDrawMethod.stick;
            }
        }

        /*private TimeSpan retentionTime;
        public TimeSpan RetentionTime
		{
            get { return retentionTime; }
		}*/
	}

	public class SeemsPointAnnotation
	{
		public SeemsPointAnnotation()
		{
			point = 0;
			label = String.Empty;
			color = Color.Gray;
			width = 1;
		}

		public SeemsPointAnnotation( double point, string label, Color color, int width )
		{
			this.point = point;
			this.label = label;
			this.color = color;
			this.width = width;
		}

		private double point;
		public double Point
		{
			get { return point; }
			set { point = value; }
		}

		private string label;
		public string Label
		{
			get { return label; }
			set { label = value; }
		}

		private Color color;
		public Color Color
		{
			get { return color; }
			set { color = value; }
		}

		private int width;
		public int Width
		{
			get { return width; }
			set { width = value; }
		}
	}

    public class AnnotationSettings
    {
        public AnnotationSettings()
        {
            labelToAliasAndColorMap = new Map<string, Pair<string, Color>>();
            pointAnnotations = new PointDataMap<SeemsPointAnnotation>();
            pointFontSpec = new ZedGraph.FontSpec( "Arial", 10, Color.Gray, false, false, false );
            pointFontSpec.Border.IsVisible = false;
        }

        private ZedGraph.FontSpec pointFontSpec;
        public pwiz.MSGraph.PointAnnotation AnnotatePoint( ZedGraph.PointPair point )
        {
            string label = null;
            if( ShowXValues && ShowYValues )
                label = String.Format( "{0]\n{1}", point.X.ToString("f" + Properties.Settings.Default.DefaultDecimalPlaces), point.Y.ToString("f" + Properties.Settings.Default.DefaultDecimalPlaces));
            else if( ShowXValues )
                label = String.Format( "{0}", point.X.ToString("f" + Properties.Settings.Default.DefaultDecimalPlaces));
            else if( ShowYValues )
                label = String.Format( "{0}", point.Y.ToString("f" + Properties.Settings.Default.DefaultDecimalPlaces));

            if( label != null )
                return new pwiz.MSGraph.PointAnnotation( label, pointFontSpec );
            return null;
        }

        private bool showXValues;
        public bool ShowXValues
        {
            get { return showXValues; }
            set { showXValues = value; }
        }

        private bool showYValues;
        public bool ShowYValues
        {
            get { return showYValues; }
            set { showYValues = value; }
        }

        private bool showMatchedAnnotations;
        public bool ShowMatchedAnnotations
        {
            get { return showMatchedAnnotations; }
            set { showMatchedAnnotations = value; }
        }

        private bool showUnmatchedAnnotations;
        public bool ShowUnmatchedAnnotations
        {
            get { return showUnmatchedAnnotations; }
            set { showUnmatchedAnnotations = value; }
        }

        private bool matchToleranceOverride;
        public bool MatchToleranceOverride
        {
            get { return matchToleranceOverride; }
            set { matchToleranceOverride = value; }
        }

        private double matchTolerance;
        public double MatchTolerance
        {
            get { return matchTolerance; }
            set { matchTolerance = value; }
        }

        private MatchToleranceUnits matchToleranceUnit;
        public MatchToleranceUnits MatchToleranceUnit
        {
            get { return matchToleranceUnit; }
            set { matchToleranceUnit = value; }
        }

        private Map<string, Pair<string, Color>> labelToAliasAndColorMap;
        public Map<string, Pair<string, Color>> LabelToAliasAndColorMap
        {
            get { return labelToAliasAndColorMap; }
        }

        private PointDataMap<SeemsPointAnnotation> pointAnnotations;
        public PointDataMap<SeemsPointAnnotation> PointAnnotations
        {
            get { return pointAnnotations; }
        }
    }
}
