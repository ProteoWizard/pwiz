using System;
using System.Collections.Generic;
using System.Linq;

namespace Pwiz.Data.MsData.MzPeak;

/// <summary>
/// Decoders for the mzPeak "chunked" point-buffer layout (mzPeak.NET's compressed variant), where
/// each parquet row holds one m/z chunk — a start value, an encoded list of values, an encoding
/// CURIE, and the parallel intensity list — instead of one row per point.
///
/// This is a faithful port of mzPeak.NET's <c>DeltaCodec</c>/<c>NoCompressionCodec</c> +
/// <c>NullInterpolation.FillNullsWithModel</c> (MZPeakNet/Compute.cs). The m/z axis is delta-encoded
/// (cumulative sum from the chunk start) and may carry nulls at chunk seams; those nulls are filled
/// from a per-spectrum spacing-interpolation model (a polynomial whose coefficients are stored in the
/// spectrum's <c>mz_delta_model</c> metadata column) or, for wider gaps, the local median spacing.
/// Intensities are stored verbatim with nulls meaning zero.
/// </summary>
internal static class MzPeakChunkCodec
{
    public const string DeltaCurie = "MS:1003089";          // delta encoding
    public const string NoCompressionCurie = "MS:1000576";  // no compression
    public const string NullInterpolateCurie = "MS:1003901"; // nulls filled from the spacing model
    public const string NullZeroCurie = "MS:1003902";        // nulls mean zero

    /// <summary>Decode one chunk's m/z list to absolute values (which may still contain nulls at the
    /// seam where a chunk restarts from an absolute value). <paramref name="encoding"/> is the chunk's
    /// <c>chunk_encoding</c> CURIE.</summary>
    public static double?[] DecodeMz(string? encoding, double start, double?[] values)
    {
        return encoding switch
        {
            DeltaCurie => DecodeDelta(start, values),
            NoCompressionCurie or null => DecodeNoCompression(start, values),
            _ => throw new NotSupportedException(
                $"mzPeak chunk encoding '{encoding}' is not supported (only delta {DeltaCurie} and " +
                $"no-compression {NoCompressionCurie}; numpress-encoded chunks are not yet implemented)."),
        };
    }

    private static double?[] DecodeNoCompression(double start, double?[] values)
    {
        var outv = new List<double?>(values.Length + 1) { start };
        outv.AddRange(values);
        return outv.ToArray();
    }

    private static double?[] DecodeDelta(double start, double?[] values)
    {
        var outv = new List<double?>(values.Length + 1);
        double? last = start;
        if (values.Length > 0 && values[0] is null)
        {
            // The chunk restarts from an absolute value carried in values[1]; the leading slot is a
            // null seam to be interpolated later. (When values[1] is also null the start is real.)
            if (values.Length > 1 && values[1] is null) outv.Add(start);
            last = null;
        }
        else
        {
            outv.Add(start);
        }

        foreach (var v in values)
        {
            if (v is double d)
            {
                if (last is null) { last = d; outv.Add(d); }
                else { last += d; outv.Add(last); }
            }
            else
            {
                last = null;
                outv.Add(null);
            }
        }
        return outv.ToArray();
    }

    /// <summary>Replace the null seams in a decoded m/z segment using the spacing model (polynomial
    /// <paramref name="coef"/>) for narrow gaps and the local median spacing for wider ones.</summary>
    public static double[] FillNullsWithModel(double?[] values, double[] coef)
    {
        var outList = new List<double>(values.Length);
        foreach (var (startIdx, endIdx) in FindNullBounds(values))
        {
            // mzPeak.NET slices [startIdx, endIdx] inclusive; the trailing +1 is clamped to the array.
            int len = Math.Min(endIdx - startIdx + 1, values.Length - startIdx);
            var chunk = new double?[len];
            Array.Copy(values, startIdx, chunk, 0, len);

            int n = chunk.Length;
            int nHasReal = n - chunk.Count(x => x is null);

            if (nHasReal == 1)
            {
                if (n == 2)
                {
                    if (chunk[0] is null)
                    {
                        double vAt = chunk[1]!.Value;
                        outList.Add(vAt - Predict(coef, vAt));
                        outList.Add(vAt);
                    }
                    else
                    {
                        double vAt = chunk[0]!.Value;
                        outList.Add(vAt);
                        outList.Add(vAt + Predict(coef, vAt));
                    }
                }
                else if (n == 3)
                {
                    double vAt = chunk[1]!.Value;
                    outList.Add(vAt - Predict(coef, vAt));
                    outList.Add(vAt);
                    outList.Add(vAt + Predict(coef, vAt));
                }
                else throw new InvalidOperationException("unreachable null-bound shape");
            }
            else
            {
                double delta = LocalMedianDelta(chunk);
                if (chunk[0] is null) outList.Add(chunk[1]!.Value - delta);
                else outList.Add(chunk[0]!.Value);

                for (int j = 1; j <= chunk.Length - 2; j++)
                {
                    if (chunk[j] is not double mid) throw new InvalidOperationException("interior null in chunk");
                    outList.Add(mid);
                }

                if (chunk[^1] is null) outList.Add(chunk[^2]!.Value + delta);
                else outList.Add(chunk[^1]!.Value);
            }
        }
        return outList.ToArray();
    }

    /// <summary>Decoded m/z with no nulls (or after a fill) → plain double[]; any residual null → NaN.</summary>
    public static double[] ToDense(double?[] values)
    {
        var outv = new double[values.Length];
        for (int i = 0; i < values.Length; i++) outv[i] = values[i] ?? double.NaN;
        return outv;
    }

    /// <summary>Intensity list → dense float[]; nulls mean zero (the NullZero transform).</summary>
    public static float[] IntensityToDense(float?[] values)
    {
        var outv = new float[values.Length];
        for (int i = 0; i < values.Length; i++) outv[i] = values[i] ?? 0f;
        return outv;
    }

    // Polynomial spacing model: coef[0] + coef[1]*x + coef[2]*x^2 + ...
    private static double Predict(double[] coef, double value)
    {
        double acc = coef[0];
        for (int i = 1; i < coef.Length; i++)
        {
            double x = value;
            for (int j = 1; j < i; j++) x *= value;
            acc += x * coef[i];
        }
        return acc;
    }

    private static double LocalMedianDelta(double?[] chunk)
    {
        var deltas = CollectDeltas(chunk);
        if (deltas.Count == 0) return 0.0;
        double median = SortedMedian(deltas);
        var below = deltas.Where(v => v <= median).ToList();
        return below.Count == 0 ? median : SortedMedian(below);
    }

    private static List<double> CollectDeltas(double?[] values)
    {
        var deltas = new List<double>();
        double last = 0.0;
        bool seen = false;
        foreach (var value in values)
        {
            if (value is not double v) continue;
            if (!seen) { last = v; seen = true; }
            else
            {
                double delta = v - last;
                if (delta < 0) throw new InvalidOperationException($"negative delta {delta} = {v} - {last}");
                deltas.Add(delta);
                last = v;
            }
        }
        deltas.Sort();
        return deltas;
    }

    private static double SortedMedian(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0.0;
        if (values.Count <= 2) return values[0];
        int mid = values.Count / 2;
        return values.Count % 2 == 0 ? values[mid] : (values[mid] + values[mid + 1]) / 2.0;
    }

    private static List<(int, int)> FindNullBounds(double?[] arr)
    {
        var bounds = new List<(int, int)>();
        if (arr.Length == 0) return bounds;

        var nullHere = new List<int>();
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] is null) nullHere.Add(i);

        if (nullHere.Count == 0) { bounds.Add((0, arr.Length)); return bounds; }

        if (nullHere[0] != 0) nullHere.Insert(0, 0);
        if (nullHere[^1] != arr.Length - 1) nullHere.Add(arr.Length);
        if (nullHere.Count % 2 != 0)
            throw new InvalidOperationException($"unpaired nulls in chunk ({nullHere.Count})");

        for (int i = 0; i < nullHere.Count; i += 2)
            bounds.Add((nullHere[i], nullHere[i + 1]));
        return bounds;
    }
}
