using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using pwiz.CLI.msdata;
using Extensions;

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
	where T1 : new()
	where T2 : new()
{
	public RefPair()
	{
		this.first = new T1();
		this.second = new T2();
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

	public enum RetentionTimeUnits
	{
		Seconds = 0,
		Minutes,
		Hours
	}

	public enum MassToleranceUnits
	{
		Daltons = 0,
		PPM,
		ResolvingPower
	}

	public abstract class GraphItem
	{
		protected string id;
		public string Id { get { return id; } }

		protected PointList pointList;
		public PointList PointList { get { return pointList; } }

		protected double totalIntegratedArea;
		public double TotalIntegratedArea { get { return totalIntegratedArea; } set { totalIntegratedArea = value; } }

		public bool IsChromatogram { get { return this is Chromatogram; } }
		public bool IsMassSpectrum { get { return this is MassSpectrum; } }
	}

	public class Chromatogram : GraphItem
	{
		public Chromatogram( DataSource source, pwiz.CLI.msdata.Chromatogram chromatogram )
		{
			this.source = source;
			this.element = chromatogram;
			id = element.nativeID;
			this.pointList = new PointList( new ZedGraph.PointPairList() );
		}

		private DataSource source;
		public DataSource Source { get { return source; } }

		private pwiz.CLI.msdata.Chromatogram element;
		public pwiz.CLI.msdata.Chromatogram Element { get { return element; } }

		public new PointList PointList
		{
			get
			{
				//if( scan.IsCentroided == source.MSDataFile.getCentroiding(scan.MsLevel) &&
				//	pointList.FullCount > 0 )
				//	return pointList;

				element = source.MSDataFile.run.chromatogramList.chromatogram( element.index, false );
				pwiz.CLI.msdata.Chromatogram elementWithData = source.MSDataFile.run.chromatogramList.chromatogram( element.index, true );
				Map<double, double> sortedFullPointList = new Map<double, double>();
				TimeIntensityPairList pairs = null;
				elementWithData.getTimeIntensityPairs( ref pairs );
				for( int i = 0; i < pairs.Count; ++i )
					sortedFullPointList[pairs[i].time] = pairs[i].intensity;
				pointList = new PointList( new ZedGraph.PointPairList(
					new List<double>( sortedFullPointList.Keys ).ToArray(),
					new List<double>( sortedFullPointList.Values ).ToArray() ) );
				return pointList;
			}
		}

		public string ComboBoxView
		{
			get
			{
				return String.Format( "{0,-20}{1,-10}", id, totalIntegratedArea );
			}
		}
	}

	public class MassSpectrum : GraphItem
	{
		public MassSpectrum( DataSource source, pwiz.CLI.msdata.Spectrum spectrum )
		{
			this.source = source;
			this.element = spectrum;
			id = element.nativeID;
			this.pointList = new PointList( new ZedGraph.PointPairList() );

			retentionTimeUnit = RetentionTimeUnits.Minutes;
		}

		private DataSource source;
		public DataSource Source { get { return source; } }

		private pwiz.CLI.msdata.Spectrum element;
		public pwiz.CLI.msdata.Spectrum Element { get { return element; } }

		public new PointList PointList
		{
			get
			{
				//if( scan.IsCentroided == source.MSDataFile.getCentroiding(scan.MsLevel) &&
					//pointList.FullCount > 0 )
					//return pointList;

				pwiz.CLI.msdata.SpectrumList spectrumList = source.MSDataFile.run.spectrumList;
				if( source.DoCentroiding )
					spectrumList = new SpectrumList_NativeCentroider( spectrumList, new List<int>( new int[] { (int) element.cvParam( CVID.MS_ms_level ).value } ) );
				element = spectrumList.spectrum( element.index, false );
				pwiz.CLI.msdata.Spectrum elementWithData = spectrumList.spectrum( element.index, true );
				Map<double, double> sortedFullPointList = new Map<double, double>();
				MZIntensityPairList pairs = null;
				elementWithData.getMZIntensityPairs( ref pairs );
				for( int i = 0; i < pairs.Count; ++i )
					sortedFullPointList[pairs[i].mz] = pairs[i].intensity;
				pointList = new PointList( new ZedGraph.PointPairList(
					new List<double>( sortedFullPointList.Keys ).ToArray(),
					new List<double>( sortedFullPointList.Values ).ToArray() ) );
				return pointList;
			}
		}

		private RetentionTimeUnits retentionTimeUnit;
		public RetentionTimeUnits RetentionTimeUnit
		{
			get { return retentionTimeUnit; }
			set { retentionTimeUnit = value; }
		}

		public double RetentionTime
		{
			get
			{
				double time = element.spectrumDescription.scan.cvParam( CVID.MS_scan_time ).timeInSeconds();
				switch( retentionTimeUnit )
				{
					default:
					case RetentionTimeUnits.Seconds:
						return time;
					case RetentionTimeUnits.Minutes:
						return time / 60.0;
					case RetentionTimeUnits.Hours:
						return time / 3600.0;
				}
			}
		}

		public string ComboBoxView
		{
			get
			{
				string precursorMz = "n/a";
				PrecursorList precursors = element.spectrumDescription.precursors;
				if( precursors.Count > 0 && precursors[0].selectedIons.Count > 0 )
					precursorMz = ( (double) precursors[0].selectedIons[0].cvParam( CVID.MS_m_z ).value ).ToString( "f3" );
				return String.Format( "{0,-8}{1,-10}{2,-5}{3,-15}{4,-15}",
					id, RetentionTime.ToString( "f3" ),
					element.cvParam( CVID.MS_ms_level ).value,
					( (double) element.spectrumDescription.cvParam( CVID.MS_total_ion_current ).value ).ToString( "e3" ),
					precursorMz );
			}
		}
	}

	public class PointList : ZedGraph.IPointList, IEnumerable<ZedGraph.PointPair>
	{
		private ZedGraph.PointPairList fullPointList;
		private ZedGraph.PointPairList scaledPointList;
		private List<int> scaledMaxIndexList;
		private int scaledWidth;
		private double scaledMin;
		private double scaledMax;
		private double scaleRange;
		private double scaleFactor;
		private int scaledMinIndex;
		private int scaledMaxIndex;

		public PointList( ZedGraph.IPointList sourcePointList )
		{
			fullPointList = new ZedGraph.PointPairList( sourcePointList );
			scaledPointList = new ZedGraph.PointPairList();
			scaledMaxIndexList = new List<int>();
		}

		public void SetScale( int bins, double min, double max )
		{
			if( scaledWidth == bins && scaledMin == min && scaledMax == max )
				return;

			scaledWidth = bins;
			scaledMin = min;
			scaledMax = max;
			scaleRange = max - min;
			if( scaleRange == 0 )
				return;
			scaleFactor = bins / scaleRange;
			scaledPointList.Clear();
			scaledPointList.Capacity = bins * 4; // store 4 points for each bin (entry, min, max, exit)
			scaledMaxIndexList.Clear();
			scaledMaxIndexList.Capacity = bins; // store just the index of the max point for each bin
			int lastBin = -1;
			int curBinEntryIndex = -1;
			int curBinMinIndex = -1;
			int curBinMaxIndex = -1;
			int curBinExitIndex = -1;
			for( int i = 0; i < fullPointList.Count; ++i )
			{
				ZedGraph.PointPair point = fullPointList[i];

				if( point.X < min )
					continue;

				if( point.X > max )
					break;

				int curBin = (int) Math.Round( scaleFactor * ( point.X - min ) );
				if( curBin > lastBin ) // new bin, insert points of last bin
				{
					if( lastBin > -1 )
					{
						scaledMinIndex = curBinMinIndex;
						scaledPointList.Add( fullPointList[curBinEntryIndex] );
						if( curBinEntryIndex != curBinMinIndex )
							scaledPointList.Add( fullPointList[curBinMinIndex] );
						if( curBinEntryIndex != curBinMaxIndex &&
							curBinMinIndex != curBinMaxIndex )
							scaledPointList.Add( fullPointList[curBinMaxIndex] );
						if( curBinEntryIndex != curBinMaxIndex &&
							curBinMinIndex != curBinMaxIndex &&
							curBinMaxIndex != curBinExitIndex )
							scaledPointList.Add( fullPointList[curBinExitIndex] );
						if( fullPointList[curBinMaxIndex].Y != 0 )
							scaledMaxIndexList.Add( curBinMaxIndex );
					}
					lastBin = curBin;
					curBinEntryIndex = i;
					curBinMinIndex = i;
					curBinMaxIndex = i;
				} else // same bin, set exit point
				{
					curBinExitIndex = i;
					if( point.Y > fullPointList[curBinMaxIndex].Y )
						scaledMaxIndex = curBinMaxIndex = i;
					else if( point.Y < fullPointList[curBinMaxIndex].Y )
						curBinMinIndex = i;
				}
			}

			if( lastBin > -1 )
			{
				scaledMinIndex = curBinMinIndex;
				scaledPointList.Add( fullPointList[curBinEntryIndex] );
				if( curBinEntryIndex != curBinMinIndex )
					scaledPointList.Add( fullPointList[curBinMinIndex] );
				if( curBinEntryIndex != curBinMaxIndex &&
					curBinMinIndex != curBinMaxIndex )
					scaledPointList.Add( fullPointList[curBinMaxIndex] );
				if( curBinEntryIndex != curBinMaxIndex &&
					curBinMinIndex != curBinMaxIndex &&
					curBinMaxIndex != curBinExitIndex )
					scaledPointList.Add( fullPointList[curBinExitIndex] );
				if( fullPointList[curBinMaxIndex].Y != 0 )
					scaledMaxIndexList.Add( curBinMaxIndex );
			}
		}

		public int Count
		{
			get
			{
				if( ScaledCount > 0 )
					return ScaledCount;
				else
					return FullCount;
			}
		}

		public int FullCount
		{
			get { return fullPointList.Count; }
		}

		public int ScaledCount
		{
			get { return scaledPointList.Count; }
		}

		public int MaxCount
		{
			get { return scaledMaxIndexList.Count; }
		}

		public ZedGraph.PointPairList FullList { get { return fullPointList; } }
		public ZedGraph.PointPairList ScaledList { get { return scaledPointList; } }
		public List<int> ScaledMaxIndexList { get { return scaledMaxIndexList; } }

		public ZedGraph.PointPair GetPointAtIndex( int index )
		{
			return fullPointList[index];
		}

		public int LowerBound( double x )
		{
			int min = 0;
			int max = scaledPointList.Count;
			int best = max - 1;
			while( true )
			{
				int i = (max + min) / 2;
				if( scaledPointList[i].X < x )
				{
					if( min == i )
						return ( max == scaledPointList.Count ? -1 : max );
					min = i;
				} else
				{
					best = i;
					max = i;
					if( i == 0 )
						break;
				}
			}
			return best;
		}

		public ZedGraph.PointPair this[int index]
		{
			get
			{
				if( ScaledCount > 0 )
					return scaledPointList[index];
				else
					return fullPointList[index];
			}
		}

		public object Clone()
		{
			throw new Exception( "The method or operation is not implemented." );
		}

		public IEnumerator<ZedGraph.PointPair> GetEnumerator()
		{
			if( ScaledCount > 0 )
				return scaledPointList.GetEnumerator();
			else
				return fullPointList.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			if( ScaledCount > 0 )
				return scaledPointList.GetEnumerator();
			else
				return fullPointList.GetEnumerator();
		}
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

	public class ChromatogramAnnotationSettings
	{
		public ChromatogramAnnotationSettings()
		{
			showPointTimes = Properties.Settings.Default.ShowChromatogramTimeLabels;
			showPointIntensities = Properties.Settings.Default.ShowChromatogramIntensityLabels;
			showMatchedAnnotations = Properties.Settings.Default.ShowChromatogramMatchedAnnotations;
			showUnmatchedAnnotations = Properties.Settings.Default.ShowChromatogramUnmatchedAnnotations;
			matchTolerance = Properties.Settings.Default.TimeMatchTolerance;
			matchToleranceOverride = Properties.Settings.Default.ChromatogramMatchToleranceOverride;

			labelToAliasAndColorMap = new Map<string, Pair<string, Color>>();
			pointAnnotations = new PointDataMap<SeemsPointAnnotation>();

		}

		private bool showPointTimes;
		public bool ShowPointTimes
		{
			get { return showPointTimes; }
			set { showPointTimes = value; }
		}

		private bool showPointIntensities;
		public bool ShowPointIntensities
		{
			get { return showPointIntensities; }
			set { showPointIntensities = value; }
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

		private RetentionTimeUnits matchToleranceUnit;
		public RetentionTimeUnits MatchToleranceUnit
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

	public class ScanAnnotationSettings
	{
		public ScanAnnotationSettings()
		{
			showPointMZs = Properties.Settings.Default.ShowScanMzLabels;
			showPointIntensities = Properties.Settings.Default.ShowScanIntensityLabels;
			showMatchedAnnotations = Properties.Settings.Default.ShowScanMatchedAnnotations;
			showUnmatchedAnnotations = Properties.Settings.Default.ShowScanUnmatchedAnnotations;
			matchTolerance = Properties.Settings.Default.MzMatchTolerance;
			matchToleranceOverride = Properties.Settings.Default.ScanMatchToleranceOverride;

			// ms-product-label -> (label alias, known color)
			labelToAliasAndColorMap = new Map<string, Pair<string, Color>>();
			labelToAliasAndColorMap["y"] = new Pair<string, Color>( "y", Color.Blue );
			labelToAliasAndColorMap["b"] = new Pair<string, Color>( "b", Color.Red );
			labelToAliasAndColorMap["y-NH3"] = new Pair<string, Color>( "y^", Color.Green );
			labelToAliasAndColorMap["y-H2O"] = new Pair<string, Color>( "y*", Color.Cyan );
			labelToAliasAndColorMap["b-NH3"] = new Pair<string, Color>( "b^", Color.Orange );
			labelToAliasAndColorMap["b-H2O"] = new Pair<string, Color>( "b*", Color.Violet );

			pointAnnotations = new PointDataMap<SeemsPointAnnotation>();
		}

		public void setScanLabels( Map<string, List<Pair<double, int>>> ionLabelToMzAndChargeMap )
		{
			foreach( Map<string, List<Pair<double, int>>>.MapPair ionListItr in ionLabelToMzAndChargeMap )
			{
				string[] labelNameIndexPair = ionListItr.Key.Split( " ".ToCharArray() );
				foreach( Pair<double, int> ionMzChargePair in ionListItr.Value )
				{
					SeemsPointAnnotation annotation = pointAnnotations[ionMzChargePair.first];
					if( labelToAliasAndColorMap.Contains( labelNameIndexPair[0] ) )
					{
						Pair<string, Color> labelColorPair = labelToAliasAndColorMap[labelNameIndexPair[0]];
						if( ionMzChargePair.second > 1 )
							annotation.Label = String.Format( "{0}{1} (+{2})", labelColorPair.first, labelNameIndexPair[1], ionMzChargePair.second );
						else
							annotation.Label = String.Format( "{0}{1}", labelColorPair.first, labelNameIndexPair[1] );

						annotation.Color = labelColorPair.second;
					} else
					{
						annotation.Label = ionListItr.Key;
						annotation.Color = Color.Blue;
					}
					annotation.Point = ionMzChargePair.first;
					annotation.Width = 2;
				}
			}
		}

		private bool showPointMZs;
		public bool ShowPointMZs
		{
			get { return showPointMZs; }
			set { showPointMZs = value; }
		}

		private bool showPointIntensities;
		public bool ShowPointIntensities
		{
			get { return showPointIntensities; }
			set { showPointIntensities = value; }
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

		private MassToleranceUnits matchToleranceUnit;
		public MassToleranceUnits MatchToleranceUnit
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
