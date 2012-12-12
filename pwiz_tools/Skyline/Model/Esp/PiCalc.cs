/*
 * Translator: Brendan MacLean <brendanx .at. u.washington.edu>,
 *             MacCoss Lab, Department of Genome Sciences, UW
 *
 * Translated to C# from the C implementation in the Trans Proteomic Pipeline:
 * 
 * http://sashimi.svn.sourceforge.net/viewvc/sashimi/trunk/trans_proteomic_pipeline/src/util/calc_pI.cpp?revision=5807&view=markup
 */

/***************************************************************************
*
*  FILENAME      :   pi.c
*
*  DESCRIPTION   :   Given a protein sequence, these functions return its pI.
* 
*  ARGUMENTS     :   char * seq; string containing the sequence, 
*                    all upper case letters, no leading or trailing blanks.
*        
*  AUTHORS       :   ROA from Amos' BASIC procedure
*
*  VERSION       :   1.6
*  DATE          :   1/25/95
*  
*  Copyright 1993 by Melanie/UIN/HCUG. All rights reserved.
*
***************************************************************************/

using System;
using System.Linq;

namespace pwiz.Skyline.Model.Esp
{
    public class PiCalc
    {
        private const double PH_MIN = 0;       /* minimum pH value */
        private const double PH_MAX = 14;    /* maximum pH value */
        private const double MAXLOOP = 2000;    /* maximum number of iterations */
        private const double EPSI = 0.0001;  /* desired precision */

        // Not L10N: Amino Acids
        /* the 7 amino acid which matter */
        private static readonly int R = AminoAcid.ToIndex('R');
        private static readonly int H = AminoAcid.ToIndex('H');
        private static readonly int K = AminoAcid.ToIndex('K');
        private static readonly int D = AminoAcid.ToIndex('D');
        private static readonly int E = AminoAcid.ToIndex('E');
        private static readonly int C = AminoAcid.ToIndex('C');
        private static readonly int Y = AminoAcid.ToIndex('Y');

        /*
         *  table of pk values : 
         *  Note: the current algorithm does not use the last two columns. Each 
         *  row corresponds to an amino acid starting with Ala. J, O and U are 
         *  inexistant, but here only in order to have the complete alphabet.
         *
         *          Ct    Nt   Sm     Sc     Sn
         */

        private static readonly double[,] PK = {
                                                /* A */    {3.55, 7.59, 0.0, 0.0, 0.0},
                                                /* B */    {3.55, 7.50, 0.0, 0.0, 0.0},
                                                /* C */    {3.55, 7.50, 9.00, 9.00, 9.00},
                                                /* D */    {4.55, 7.50, 4.05, 4.05, 4.05},
                                                /* E */    {4.75, 7.70, 4.45, 4.45, 4.45},
                                                /* F */    {3.55, 7.50, 0.0, 0.0, 0.0},
                                                /* G */    {3.55, 7.50, 0.0, 0.0, 0.0},
                                                /* H */    {3.55, 7.50, 5.98, 5.98, 5.98},
                                                /* I */    {3.55, 7.50, 0.0, 0.0, 0.0},
                                                /* J */    {0.00, 0.00, 0.0, 0.0, 0.0},
                                                /* K */    {3.55, 7.50, 10.00, 10.00, 10.00},
                                                /* L */    {3.55, 7.50, 0.0, 0.0, 0.0},
                                                /* M */    {3.55, 7.00, 0.0, 0.0, 0.0},
                                                /* N */    {3.55, 7.50, 0.0, 0.0, 0.0},
                                                /* O */    {0.00, 0.00, 0.0, 0.0, 0.0},
                                                /* P */    {3.55, 8.36, 0.0, 0.0, 0.0},
                                                /* Q */    {3.55, 7.50, 0.0, 0.0, 0.0},
                                                /* R */    {3.55, 7.50, 12.0, 12.0, 12.0},
                                                /* S */    {3.55, 6.93, 0.0, 0.0, 0.0},
                                                /* T */    {3.55, 6.82, 0.0, 0.0, 0.0},
                                                /* U */    {0.00, 0.00, 0.0, 0.0, 0.0},
                                                /* V */    {3.55, 7.44, 0.0, 0.0, 0.0},
                                                /* W */    {3.55, 7.50, 0.0, 0.0, 0.0},
                                                /* X */    {3.55, 7.50, 0.0, 0.0, 0.0},
                                                /* Y */    {3.55, 7.50, 10.00, 10.00, 10.00},
                                                /* Z */    {3.55, 7.50, 0.0, 0.0, 0.0}
                                               };

        private static double Exp10(double value)
        {
            return Math.Pow(10.0, value);
        }

        /// <summary>
        /// The original had support for a chargeIncrement variable, which
        /// I did not understand, but reviewing a calculate_pi program written by Jimmy Eng,
        /// I found that he used zero for this variable:
        /// http://sashimi.svn.sourceforge.net/viewvc/sashimi/trunk/trans_proteomic_pipeline/src/util/calculate_pi.cpp?revision=5807&amp;view=markup
        /// The results of using zero are different from using 1, and they match up perfectly
        /// with this web-based pI calculator:
        /// http://expasy.org/tools/pi_tool.html
        /// I have left the chargeIncrement parameter for historical reasons, but suggest
        /// using the default, unless you understand this variable better than I do.
        /// </summary>
        /// <param name="seq">The peptide sequence for which the isoelectric point (pI) is desired</param>
        /// <param name="chargeIncrement">Meaning unknown</param>
        /// <returns>The calculated pI for the peptide</returns>
        public static double Calculate(string seq, int chargeIncrement = 0)
        {
            int[] comp = new int[26];    /* Amino acid composition of the protein */

            foreach (char aa in seq)
                comp[AminoAcid.ToIndex(aa)]++;

            int ntermRes = AminoAcid.ToIndex(seq.First());
            int ctermRes = AminoAcid.ToIndex(seq.Last());

            double phMin = PH_MIN;
            double phMax = PH_MAX;

            int i;
            double phMid = 0;
            for (i = 0; i < MAXLOOP && (phMax - phMin) > EPSI; i++)
            {
                phMid = phMin + (phMax - phMin) / 2.0;

                double cter = Exp10(-PK[ctermRes, 0]) / (Exp10(-PK[ctermRes, 0]) + Exp10(-phMid));
                double nter = Exp10(-phMid) / (Exp10(-PK[ntermRes, 1]) + Exp10(-phMid));

                double carg = comp[R] * Exp10(-phMid) / (Exp10(-PK[R, 2]) + Exp10(-phMid));
                double chis = comp[H] * Exp10(-phMid) / (Exp10(-PK[H, 2]) + Exp10(-phMid));
                double clys = comp[K] * Exp10(-phMid) / (Exp10(-PK[K, 2]) + Exp10(-phMid));

                double casp = comp[D] * Exp10(-PK[D, 2]) / (Exp10(-PK[D, 2]) + Exp10(-phMid));
                double cglu = comp[E] * Exp10(-PK[E, 2]) / (Exp10(-PK[E, 2]) + Exp10(-phMid));

                double ccys = comp[C] * Exp10(-PK[C, 2]) / (Exp10(-PK[C, 2]) + Exp10(-phMid));
                double ctyr = comp[Y] * Exp10(-PK[Y, 2]) / (Exp10(-PK[Y, 2]) + Exp10(-phMid));

                double charge = carg + clys + chis + nter + chargeIncrement
                                - (casp + cglu + ctyr + ccys + cter);

                if (charge > 0.0)
                {
                    phMin = phMid;
                }
                else
                {
                    phMax = phMid;
                }
            }

            return (phMid);
        }
    }
}
