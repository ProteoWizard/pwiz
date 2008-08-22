using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using pwiz.CLI.msdata;

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
		public PointDataMap()
		{
		}

		public MapPair FindNear( double x, double tolerance )
		{
			Enumerator cur, min, max;

			min = LowerBound( x - tolerance );
			max = LowerBound( x + tolerance );

			if( !min.IsValid || ( max.IsValid && min.Current == max.Current ) )
				return null; // no peaks

			MutableKeyValuePair<double, T> best = min.Current;

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
				}
				cur.MoveNext();
				if( ( !max.IsValid && !cur.IsValid ) || cur.Current == max.Current )
					break;
			}

			return new MapPair( best.Key, best.Value );
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

	public abstract class GraphItem : IComparable<GraphItem>, MSGraph.IMSGraphItemInfo
	{
		protected string id;
		public string Id { get { return id; } }

        protected int index;
        public int Index { get { return index; } }

        protected ManagedDataSource source;
        public ManagedDataSource Source { get { return source; } }

		protected double totalIntegratedArea;
		public double TotalIntegratedArea { get { return totalIntegratedArea; } set { totalIntegratedArea = value; } }

		public bool IsChromatogram { get { return this is Chromatogram; } }
		public bool IsMassSpectrum { get { return this is MassSpectrum; } }

        public int CompareTo( GraphItem other )
        {
            return id.CompareTo( other.id );
        }

        public object Tag;

        public virtual MSGraph.MSGraphItemType GraphItemType
        {
            get
            {
                if( IsChromatogram )
                    return MSGraph.MSGraphItemType.Chromatogram;
                else
                    return MSGraph.MSGraphItemType.Spectrum;
            }
        }

        public virtual string Title { get { return Id; } }
        public virtual Color Color { get { return Color.Gray; } }

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

        public virtual MSGraph.MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return MSGraph.MSGraphItemDrawMethod.Stick; }
        }

        private AnnotationSettings annotationSettings;
        public AnnotationSettings AnnotationSettings
        {
            get { return annotationSettings; }
            set { annotationSettings = value; }
        }

        public virtual string AnnotatePoint( ZedGraph.PointPair point )
        {
            if( annotationSettings != null )
            {
                Map<double, SeemsPointAnnotation>.Enumerator itr = annotationSettings.PointAnnotations.Find( point.X );
                if( itr.IsValid && itr.Current.Value != null )
                    return itr.Current.Value.Label;

                MSGraph.PointAnnotationFormat defaultAnnotationFormat = annotationSettings.DefaultAnnotationFormat;
                if( defaultAnnotationFormat != null )
                    return defaultAnnotationFormat.AnnotatePoint( point );
            }

            return String.Empty;
        }

        public virtual ZedGraph.IPointList Points { get { return null; } }
    }

	public class Chromatogram : GraphItem
	{
		public Chromatogram( ManagedDataSource source, pwiz.CLI.msdata.Chromatogram chromatogram )
		{
			this.source = source;
			element = chromatogram;
			id = element.nativeID;
            index = element.index;
		}

        public Chromatogram( Chromatogram metaChromatogram, pwiz.CLI.msdata.Chromatogram chromatogram )
        {
            source = metaChromatogram.source;
            Tag = metaChromatogram.Tag;
            AnnotationSettings = metaChromatogram.AnnotationSettings;
            element = chromatogram;
            id = element.nativeID;
            index = element.index;
        }

		private pwiz.CLI.msdata.Chromatogram element;
		public pwiz.CLI.msdata.Chromatogram Element { get { return element; } }

		public override ZedGraph.IPointList Points
		{
            get
            {
                if( element.binaryDataArrays.Count >= 2 )
                {
                    Map<double, double> sortedFullPointList = new Map<double, double>();
                    IList<double> timeList = element.binaryDataArrays[0].data;
                    IList<double> intensityList = element.binaryDataArrays[1].data;
                    int arrayLength = timeList.Count;
                    for( int i = 0; i < arrayLength; ++i )
                        sortedFullPointList[timeList[i]] = intensityList[i];
                    return new ZedGraph.PointPairList(
                        new List<double>( sortedFullPointList.Keys ).ToArray(),
                        new List<double>( sortedFullPointList.Values ).ToArray() );
                } else
                    throw new Exception( "metachromatogram queried for data points" );
            }
		}

        public override MSGraph.MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return MSGraph.MSGraphItemDrawMethod.Line; }
        }
	}

	public class MassSpectrum : GraphItem
	{
        public MassSpectrum( ManagedDataSource source, pwiz.CLI.msdata.Spectrum spectrum )
        {
            this.source = source;
            element = spectrum;
            id = spectrum.nativeID;
            index = spectrum.index;
            //retentionTime = new TimeSpan( 0, 0, 0, 0, (int) Math.Round( spectrum.spectrumDescription.scan.cvParam( CVID.MS_scan_time ).timeInSeconds() * 1000 ) );
        }

        public MassSpectrum( MassSpectrum metaSpectrum, pwiz.CLI.msdata.Spectrum spectrum )
        {
            source = metaSpectrum.source;
            Tag = metaSpectrum.Tag;
            AnnotationSettings = metaSpectrum.AnnotationSettings;
            element = spectrum;
            id = element.nativeID;
            index = element.index;
        }

		private pwiz.CLI.msdata.Spectrum element;
        public pwiz.CLI.msdata.Spectrum Element
        {
            get
            {
                return element;
                //return source.MSDataFile.run.spectrumList.spectrum(index);
            }
        }

		public override ZedGraph.IPointList Points
		{
            get
            {
                if( element.binaryDataArrays.Count >= 2 )
                {
                    Map<double, double> sortedFullPointList = new Map<double, double>();
                    IList<double> mzList = element.getMZArray().data;
                    IList<double> intensityList = element.getIntensityArray().data;
                    int arrayLength = mzList.Count;
                    for( int i = 0; i < arrayLength; ++i )
                        sortedFullPointList[mzList[i]] = intensityList[i];
                    return new ZedGraph.PointPairList(
                        new List<double>( sortedFullPointList.Keys ).ToArray(),
                        new List<double>( sortedFullPointList.Values ).ToArray() );
                } else
                    throw new Exception( "metaspectrum queried for data points" );
            }
		}

        public override MSGraph.MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get
            {
                CVParam representation = element.spectrumDescription.cvParamChild(CVID.MS_spectrum_representation);
                if( !representation.empty() && representation.cvid == CVID.MS_profile_mass_spectrum )
                    return MSGraph.MSGraphItemDrawMethod.Line;
                else
                    return MSGraph.MSGraphItemDrawMethod.Stick;
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
        }

        public MSGraph.PointAnnotationFormat DefaultAnnotationFormat
        {
            get
            {
                if( ShowXValues && ShowYValues )
                    return new MSGraph.PointAnnotationFormat( "{x:f2}\n{y:f2}" );
                else if( ShowXValues )
                    return new MSGraph.PointAnnotationFormat( "{x:f2}" );
                else if( ShowYValues )
                    return new MSGraph.PointAnnotationFormat( "{y:f2}" );
                else
                    return null;
            }
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
