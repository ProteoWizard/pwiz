using System;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Isotope distribution calculation from exact elemental composition.
    /// Port of osprey-core/src/isotope.rs. Uses the polynomial expansion
    /// method with natural isotope abundances for C, H, N, O, S.
    ///
    /// NOTE: This should eventually be replaced with Shared/CommonUtil/Chemistry
    /// (Molecule, AminoAcidFormulas, MassDistribution) so OspreySharp computes
    /// isotope distributions identically to Skyline. For now, this matches Rust
    /// for cross-implementation parity.
    /// </summary>
    public static class IsotopeDistribution
    {
        // Natural isotope abundances (IUPAC 2016, except 13C from IDCalc)
        private const double C13 = 0.01084;
        private const double H2 = 0.000115;
        private const double N15 = 0.00364;
        private const double O16 = 0.99757;
        private const double O17 = 0.00038;
        private const double O18 = 0.00205;
        private const double S32 = 0.9499;
        private const double S33 = 0.0075;
        private const double S34 = 0.0425;
        private const double S36 = 0.0001;

        /// <summary>
        /// Calculate isotope cosine score for a peptide sequence.
        /// Returns cosine similarity (0-1) between observed and theoretical
        /// isotope envelopes, or -1 if calculation fails.
        /// </summary>
        public static double PeptideIsotopeCosine(string sequence, double[] observed)
        {
            int c, h, n, o, s;
            if (!PeptideComposition(sequence, out c, out h, out n, out o, out s))
                return -1.0;

            double[] theoretical = CalculateDistribution(c, h, n, o, s);
            return IsotopeCosineScore(observed, theoretical);
        }

        /// <summary>
        /// Calculate elemental composition for a peptide sequence.
        /// Accounts for peptide bonds and terminal H2O.
        /// </summary>
        public static bool PeptideComposition(string sequence,
            out int c, out int h, out int n, out int o, out int s)
        {
            c = 0; h = 0; n = 0; o = 0; s = 0;

            foreach (char ch in sequence)
            {
                if (ch == '[' || ch == ']' || ch == '(' || ch == ')'
                    || char.IsDigit(ch) || ch == '+' || ch == '-' || ch == '.')
                    continue;

                int ac, ah, an, ao, as2;
                if (!AminoAcidComposition(char.ToUpperInvariant(ch),
                    out ac, out ah, out an, out ao, out as2))
                {
                    if (char.IsLetter(ch))
                        return false;
                    continue;
                }
                c += ac; h += ah; n += an; o += ao; s += as2;
            }

            // Add terminal H2O
            h += 2;
            o += 1;
            return true;
        }

        /// <summary>
        /// Calculate isotope distribution for an elemental composition.
        /// Returns normalized [M+0, M+1, M+2, M+3, M+4].
        /// </summary>
        public static double[] CalculateDistribution(int c, int h, int n, int o, int s)
        {
            double[] dist = { 1.0, 0.0, 0.0, 0.0, 0.0 };

            if (c > 0) dist = ConvolveBinomial(dist, c, C13);
            if (h > 0) dist = ConvolveBinomial(dist, h, H2);
            if (n > 0) dist = ConvolveBinomial(dist, n, N15);
            if (o > 0) dist = ConvolveOxygen(dist, o);
            if (s > 0) dist = ConvolveSulfur(dist, s);

            // Normalize to sum = 1
            double sum = 0;
            for (int i = 0; i < 5; i++) sum += dist[i];
            if (sum > 0)
                for (int i = 0; i < 5; i++) dist[i] /= sum;

            return dist;
        }

        /// <summary>
        /// Cosine similarity between observed [M-1, M+0, M+1, M+2, M+3] and
        /// theoretical [M+0, M+1, M+2, M+3, M+4] isotope distributions.
        /// Theoretical is aligned to [0, M+0, M+1, M+2, M+3] for comparison.
        /// </summary>
        public static double IsotopeCosineScore(double[] observed, double[] theoretical)
        {
            if (observed == null || observed.Length < 5 || theoretical == null || theoretical.Length < 5)
                return -1.0;

            // Align theoretical: [M-1=0, M+0, M+1, M+2, M+3]
            double[] theo = {
                0.0,            // M-1
                theoretical[0], // M+0
                theoretical[1], // M+1
                theoretical[2], // M+2
                theoretical[3]  // M+3
            };

            double dot = 0, obsNormSq = 0, theoNormSq = 0;
            for (int i = 0; i < 5; i++)
            {
                dot += observed[i] * theo[i];
                obsNormSq += observed[i] * observed[i];
                theoNormSq += theo[i] * theo[i];
            }

            double obsNorm = Math.Sqrt(obsNormSq);
            double theoNorm = Math.Sqrt(theoNormSq);

            if (obsNorm < 1e-10 || theoNorm < 1e-10)
                return -1.0;

            double cosine = dot / (obsNorm * theoNorm);
            return Math.Max(0.0, Math.Min(1.0, cosine));
        }

        private static bool AminoAcidComposition(char aa,
            out int c, out int h, out int n, out int o, out int s)
        {
            c = 0; h = 0; n = 0; o = 0; s = 0;
            switch (aa)
            {
                case 'A': c=3;  h=5;  n=1; o=1; s=0; return true;
                case 'C': c=3;  h=5;  n=1; o=1; s=1; return true;
                case 'D': c=4;  h=5;  n=1; o=3; s=0; return true;
                case 'E': c=5;  h=7;  n=1; o=3; s=0; return true;
                case 'F': c=9;  h=9;  n=1; o=1; s=0; return true;
                case 'G': c=2;  h=3;  n=1; o=1; s=0; return true;
                case 'H': c=6;  h=7;  n=3; o=1; s=0; return true;
                case 'I': c=6;  h=11; n=1; o=1; s=0; return true;
                case 'K': c=6;  h=12; n=2; o=1; s=0; return true;
                case 'L': c=6;  h=11; n=1; o=1; s=0; return true;
                case 'M': c=5;  h=9;  n=1; o=1; s=1; return true;
                case 'N': c=4;  h=6;  n=2; o=2; s=0; return true;
                case 'P': c=5;  h=7;  n=1; o=1; s=0; return true;
                case 'Q': c=5;  h=8;  n=2; o=2; s=0; return true;
                case 'R': c=6;  h=12; n=4; o=1; s=0; return true;
                case 'S': c=3;  h=5;  n=1; o=2; s=0; return true;
                case 'T': c=4;  h=7;  n=1; o=2; s=0; return true;
                case 'V': c=5;  h=9;  n=1; o=1; s=0; return true;
                case 'W': c=11; h=10; n=2; o=1; s=0; return true;
                case 'Y': c=9;  h=9;  n=1; o=2; s=0; return true;
                case 'U': c=3;  h=5;  n=1; o=1; s=0; return true; // Selenocysteine approx
                default: return false;
            }
        }

        private static double[] ConvolveBinomial(double[] dist, int n, double heavyProb)
        {
            double lightProb = 1.0 - heavyProb;
            double[] result = new double[5];

            int maxK = Math.Min(4, n);
            for (int k = 0; k <= maxK; k++)
            {
                double prob = BinomialCoefficient(n, k)
                    * Math.Pow(heavyProb, k)
                    * Math.Pow(lightProb, n - k);

                for (int i = 0; i < 5; i++)
                    if (i + k < 5)
                        result[i + k] += dist[i] * prob;
            }
            return result;
        }

        private static double[] ConvolveOxygen(double[] dist, int n)
        {
            double[] result = new double[5];
            int max17 = Math.Min(n, 4);
            for (int n17 = 0; n17 <= max17; n17++)
            {
                int max18 = Math.Min(n - n17, 4);
                for (int n18 = 0; n18 <= max18; n18++)
                {
                    int n16 = n - n17 - n18;
                    int shift = n17 + 2 * n18;
                    if (shift > 4) continue;

                    double prob = MultinomialProb(n,
                        new[] { n16, n17, n18 },
                        new[] { O16, O17, O18 });

                    for (int i = 0; i < 5; i++)
                        if (i + shift < 5)
                            result[i + shift] += dist[i] * prob;
                }
            }
            return result;
        }

        private static double[] ConvolveSulfur(double[] dist, int n)
        {
            double[] result = new double[5];
            int max33 = Math.Min(n, 4);
            for (int n33 = 0; n33 <= max33; n33++)
            {
                int max34 = Math.Min(n - n33, 4);
                for (int n34 = 0; n34 <= max34; n34++)
                {
                    int max36 = Math.Min(n - n33 - n34, 1);
                    for (int n36 = 0; n36 <= max36; n36++)
                    {
                        int n32 = n - n33 - n34 - n36;
                        int shift = n33 + 2 * n34 + 4 * n36;
                        if (shift > 4) continue;

                        double prob = MultinomialProb(n,
                            new[] { n32, n33, n34, n36 },
                            new[] { S32, S33, S34, S36 });

                        for (int i = 0; i < 5; i++)
                            if (i + shift < 5)
                                result[i + shift] += dist[i] * prob;
                    }
                }
            }
            return result;
        }

        private static double BinomialCoefficient(int n, int k)
        {
            if (k > n) return 0.0;
            if (k == 0 || k == n) return 1.0;
            k = Math.Min(k, n - k);
            double result = 1.0;
            for (int i = 0; i < k; i++)
                result *= (double)(n - i) / (i + 1);
            return result;
        }

        private static double MultinomialProb(int n, int[] counts, double[] probs)
        {
            int sum = 0;
            for (int i = 0; i < counts.Length; i++) sum += counts[i];
            if (sum != n) return 0.0;

            double coeff = 1.0;
            int remaining = n;
            for (int i = 0; i < counts.Length; i++)
            {
                coeff *= BinomialCoefficient(remaining, counts[i]);
                remaining -= counts[i];
            }

            double prob = coeff;
            for (int i = 0; i < counts.Length; i++)
                prob *= Math.Pow(probs[i], counts[i]);

            return prob;
        }
    }
}
