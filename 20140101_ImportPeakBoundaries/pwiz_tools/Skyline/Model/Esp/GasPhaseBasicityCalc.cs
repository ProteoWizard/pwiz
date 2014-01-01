/*
 * Translator: Brendan MacLean <brendanx .at. u.washington.edu>,
 *             MacCoss Lab, Department of Genome Sciences, UW
 *
 * Translated to C# from the MatLab implementation in the "Supplementary
 * Source Code" for the ESP paper:
 * 
 * Prediction of high-responding peptides for targeted protein assays by mass spectrometry
 * Vincent A. Fusaro, D. R. Mani, Jill P. Mesirov & Steven A. Carr
 * Nature Biotechnology (2009) 27:190-198.
 * 
 * http://www.nature.com/nbt/journal/v27/n2/extref/nbt.1524-S4.zip
 */

namespace pwiz.Skyline.Model.Esp
{
    public class GasPhaseBasicityCalc
    {
        private static readonly double[][] GB_LOOKUP = new double[26][];

        static GasPhaseBasicityCalc()
        {

            // Not L10N: Amino acids
            Init('A', 881.82, 0);
            Init('R', 882.98, 6.28);
            Init('N', 881.18, 1.56);
            Init('D', 880.02, -.63);
            Init('C', 881.15, -.69);
            Init('Q', 881.5, 4.1);
            Init('E', 880.1, -.39);
            Init('G', 881.17, .92);
            Init('H', 881.27, -.19);
            Init('I', 880.99, -1.17);
            Init('L', 881.88, -.09);
            Init('K', 880.06, -.71);
            Init('M', 881.38, .3);
            Init('F', 881.08, .03);
            Init('P', 881.25, 11.75);
            Init('S', 881.08, .98);
            Init('T', 881.14, 1.21);
            Init('W', 881.31, .1);
            Init('Y', 881.2, -.38);
            Init('V', 881.17, -.9);
        }

        private static void Init(char aa, double left, double right)
        {
            GB_LOOKUP[AminoAcid.ToIndex(aa)] = new[] { left, right };
        }

        /// <summary>
        /// this function looks up the left and right GB values for the amide bond
        /// according to Zhang's paper.
        /// </summary>
        private static double GBLookUp(char aa, bool left)
        {
            return GB_LOOKUP[AminoAcid.ToIndex(aa)][left ? 0 : 1];
        }

        /// <summary>
        /// Returns the gas phase basicity values based on Zhang's analytical
        /// chemistry paper. 
        /// </summary>
        public static double[] Calculate(string sequence)
        {
            var values = new double[sequence.Length + 1];
            if (sequence.Length > 0)
            {
                values[0] = 916.84 + GBLookUp(sequence[0], false); // N-terminal
                values[sequence.Length] = GBLookUp(sequence[sequence.Length - 1], true) - 95.82; // C-terminal
                for (int i = 1; i < sequence.Length; i++)
                {
                    values[i] = GBLookUp(sequence[i - 1], true) + GBLookUp(sequence[i], false);
                }
            }
            return values;
        }
    }
}
