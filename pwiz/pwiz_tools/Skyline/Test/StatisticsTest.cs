/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using pwiz.Skyline.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{    
    /// <summary>
    /// This is a test class for the Statistics class and contains
    /// all Statistics Unit Tests.  All function return values have
    /// been verified against Excel.
    /// </summary>
    [TestClass]
    public class StatisticsTest : AbstractUnitTest
    {
        private readonly Statistics _xValues;
        private readonly Statistics _yValues;
        private readonly double _xMember;
        private readonly double _yMember;
        private readonly Statistics _interventionRequests;
        private readonly Statistics _interventionPercents;
        private readonly Statistics _controlRequests;
        private readonly Statistics _controlPercents;


        public StatisticsTest()
        {
            // These peptides were taken from the supporting information for the
            // Krokhin, Anal. Chem., 2006 paper (See SSRCalc3Test.cs)

            // This set of results contains the actual retention times as a third
            // column, for use in testing linear regression statistics.

            var peptideResults = new object[,]
            {
                {"LVEYR", 10.69, 31.0},
                {"EVQPGR", 3.92, 17.0},
                {"NQYLR", 10.39, 29.2},
                {"HREER", 1.95, 13.0},
                {"YQYQR", 5.68, 25.0},
                {"NNEVQR", 3.77, 16.0},
                {"NPFLFK", 27.33, 56.0},
                {"EDPEER", 2.79, 16.0},
                {"YETIEK", 8.39, 28.0},
                {"NEQQER", 0.99, 11.0},
                {"SSESQER", 1.34, 11.0},
                {"EGEEEER", 2.06, 15.5},
                {"EQIEELK", 14.34, 36.0},
                {"NSYNLER", 11.59, 32.0},
                {"QEDEEEK", 0.85, 13.0},
                {"RNPFLFK", 28.86, 55.2},
                {"REDPEER", 3.49, 19.8},
                {"FEAFDLAK", 29.13, 55.1},
                {"GNLELLGLK", 32.08, 61.1},
                {"QEGEKEEK", 0.88, 13.0},
                {"LFEITPEK", 24.20, 51.0},
                {"VLLEEQEK", 17.10, 36.0},
                {"EQIEELKK", 13.61, 34.2},
                {"EKEDEEEK", 1.20, 14.0},
                {"SHKPEYSNK", 6.08, 22.0},
                {"LFEITPEKK", 22.79, 49.0},
                {"EEDEEEGQR", 1.89, 17.0},
                {"AIVVLLVNEGK", 32.71, 61.8},
                {"QEDEEEKQK", 0.66, 15.0},
                {"NILEASYNTR", 20.09, 45.8},
                {"AILTVLSPNDR", 29.18, 56.0},
                {"QQGEETDAIVK", 12.18, 32.2},
                {"VLLEEQEKDR", 17.24, 37.9},
                {"HGEWRPSYEK", 16.50, 39.6},
                {"LVDLVIPVNGPGK", 31.14, 61.0},
                {"RQQGEETDAIVK", 13.14, 33.0},
                {"QSHFANAEPEQK", 11.27, 30.2},
                {"SDLFENLQNYR", 30.44, 62.7},
                {"SLPSEFEPINLR", 33.12, 63.1},
                {"RHGEWRPSYEK", 16.40, 40.0},
                {"ELTFPGSVQEINR", 28.46, 56.6},
                {"KSLPSEFEPINLR", 32.53, 60.0},
                {"RSDLFENLQNYR", 29.38, 61.0},
                {"EEDEEQVDEEWR", 20.02, 43.0},
                {"WEREEDEEQVDEEWR", 27.02, 50.0},
                {"NFLSGSDDNVISQIENPVK", 34.63, 68.2},
                {"LPAGTTSYLVNQDDEEDLR", 31.49, 53.9},
                {"HGEWRPSYEKQEDEEEK", 17.96, 41.0},
                {"HGEWRPSYEKEEDEEEGQR", 19.54, 42.7},
                {"AKPHTIFLPQHIDADLILVVLSGK", 51.49, 92.5},
                {"LPAGTTSYLVNQDDEEDLRLVDLVIPVNGPGK", 48.93, 82.0},
                {"LSPGDVVIIPAGHPVAITASSNLNLLGFGINAENNER", 48.29, 81.1},
                {"FDQR", 4.38, 17.0},
                {"LLEYK", 14.65, 37.0},
                {"ILENQK", 7.41, 22.0},
                {"QVQNYK", 4.12, 20.0},
                {"NSFNLER", 17.38, 40.1},
                {"DSFNLER", 17.40, 41.0},
                {"DDNEELR", 7.78, 23.1},
                {"GQIEELSK", 14.38, 34.3},
                {"VLLEEHEK", 16.50, 36.3},
                {"FFEITPEK", 26.34, 52.5},
                {"GDFELVGQR", 22.76, 46.8},
                {"NENQQEQR", 0.39, 13.1},
                {"GPIYSNEFGK", 21.85, 44.1},
                {"AIVIVTVNEGK", 25.07, 49.4},
                {"SDPQNPFIFK", 27.71, 57.1},
                {"IFENLQNYR", 24.28, 50.4},
                {"AILTVLKPDDR", 28.26, 55.0},
                {"LPAGTIAYLVNR", 29.86, 62.3},
                {"QQSQEENVIVK", 14.40, 34.2},
                {"SVSSESEPFNLR", 23.84, 49.0},
                {"SRGPIYSNEFGK", 21.20, 45.0},
                {"EGSLLLPHYNSR", 26.13, 52.8},
                {"QSHFADAQPQQR", 11.06, 30.8},
                {"ELAFPGSAQEVDR", 24.71, 50.2},
                {"RQQSQEENVIVK", 15.42, 35.0},
                {"KSVSSESEPFNLR", 23.77, 49.0},
                {"FQTLFENENGHIR", 28.50, 57.0},
                {"VLLEEHEKETQHR", 16.28, 39.0},
                {"NILEASFNTDYEEIEK", 35.62, 66.1},
                {"KEDDEEEEQGEEEINK", 11.09, 33.0},
                {"NPQLQDLDIFVNSVEIK", 42.27, 79.1},
                {"ASSNLDLLGFGINAENNQR", 37.00, 67.2},
                {"AILTVLKPDDRNSFNLER", 37.94, 64.0},
                {"NFLAGDEDNVISQVQRPVK", 33.85, 67.6},
                {"SKPHTIFLPQHTDADYILVVLSGK", 45.74, 82.2},
                {"FFEITPEKNPQLQDLDIFVNSVEIK", 51.59, 86.8},
                {"QVQLYR", 12.93, 37.0},
                {"NPIYSNK", 9.96, 28.0},
                {"DDNEDLR", 7.55, 22.0},
                {"EQIEELSK", 14.50, 35.0},
                {"SRNPIYSNK", 10.29, 30.5},
                {"AIVIVTVTEGK", 26.18, 53.1},
                {"SDQENPFIFK", 26.95, 56.1},
                {"LPAGTIAYLANR", 27.05, 56.0},
                {"SVSSESGPFNLR", 22.76, 49.9},
                {"QEINEENVIVK", 21.36, 43.0},
                {"EGSLLLPNYNSR", 26.40, 53.2},
                {"QSYFANAQPLQR", 23.73, 47.1},
                {"ELAFPGSSHEVDR", 22.94, 48.0},
                {"RQEINEENVIVK", 22.80, 43.2},
                {"FQTLYENENGHIR", 24.55, 48.0},
                {"VLLEQQEQEPQHR", 19.09, 38.2},
                {"NILEAAFNTNYEEIEK", 37.13, 70.2},
                {"NQQLQDLDIFVNSVDIK", 41.34, 77.0},
                {"LPAGTIAYLANRDDNEDLR", 33.20, 60.0},
                {"NFLAGEEDNVISQVERPVK", 34.14, 68.3},
                {"SKPHTLFLPQYTDADFILVVLSGK", 52.80, 94.7},
                {"VLDLAIPVNKPGQLQSFLLSGTQNQPSLLSGFSK", 51.34, 86.0},
                {"LSPGDVFVIPAGHPVAINASSDLNLIGFGINAENNER", 48.61, 83.0},
                {"SFLPSK", 17.38, 39.0},
                {"EGLTFR", 17.83, 42.0},
                {"TILFLK", 30.69, 60.0},
                {"NLFEGGIK", 24.01, 51.0},
                {"DKPWWPK", 24.74, 52.0},
                {"DENFGHLK", 15.61, 38.0},
                {"FTPPHVIR", 23.05, 48.0},
                {"DSSSPYGLR", 14.92, 35.0},
                {"SSDFLAYGIK", 28.65, 56.4},
                {"NNDPSLYHR", 14.24, 33.0},
                {"QLSVVHPINK", 21.28, 44.0},
                {"ENPHWTSDSK", 10.92, 30.0},
                {"NDSELQHWWK", 27.18, 58.0},
                {"SYLPSETPSPLVK", 28.38, 52.0},
                {"EIFRTDGEQVLK", 26.50, 53.0},
                {"SNLDPAEYGDHTSK", 14.78, 38.0},
                {"SLTLEDVPNHGTIR", 26.63, 52.0},
                {"LPLDVISTLSPLPVVK", 44.43, 77.3},
                {"DPNSEKPATETYVPR", 16.41, 38.0},
                {"VGPVQLPYTLLHPSSK", 33.89, 67.3},
                {"FQTLIDLSVIEILSR", 56.36, 94.0},
                {"YWVFTDQALPNDLIK", 40.64, 75.0},
                {"KDPNSEKPATETYVPR", 15.78, 38.2},
                {"LFILDYHDTFIPFLR", 53.07, 89.0},
                {"VILPADEGVESTIWLLAK", 44.06, 83.0},
                {"SLSDR", 4.42, 17.0},
                {"ATLQR", 5.84, 20.0},
                {"YRDR", 2.75, 15.0},
                {"HIVDR", 8.12, 23.0},
                {"FLVPAR", 20.89, 43.8},
                {"SNNPFK", 9.30, 30.1},
                {"FSYVAFK", 25.59, 52.5},
                {"LDALEPDNR", 18.08, 37.1},
                {"LSAEHGSLHK", 10.95, 30.5},
                {"GEEEEEDKK", 1.31, 16.0},
                {"GGLSIISPPEK", 24.34, 50.9},
                {"QEEDEDEEK", 1.39, 16.0},
                {"TVTSLDLPVLR", 31.92, 64.9},
                {"ALTVPQNYAVAAK", 22.30, 47.0},
                {"QEEEEDEDEER", 4.30, 22.0},
                {"QEEDEDEEKQPR", 3.67, 23.0},
                {"EQPQQNECQLER", 10.01, 30.0},
                {"QEQENEGNNIFSGFK", 24.49, 57.0},
                {"IESEGGLIETWNPNNK", 30.54, 57.0},
                {"QEEEEDEDEERQPR", 5.81, 26.5},
                {"LNIGPSSSPDIYNPEAGR", 26.82, 53.3},
                {"LAGTSSVINNLPLDVVAATFNLQR", 44.90, 91.8},
                {"FYLAGNHEQEFLQYQHQQGGK", 32.37, 58.0},
                {"RFYLAGNHEQEFLQYQHQQGGK", 32.44, 58.0},
                {"IEKEDVR", 7.69, 25.0},
                {"VDEVFER", 18.12, 38.8},
                {"GIIGLVAEDR", 28.64, 57.3},
                {"QYDEEDKR", 3.82, 22.2},
                {"EVAFDIAAEK", 27.09, 50.0},
                {"SLWPFGGPFK", 35.79, 75.1},
                {"FNLEEGDIMR", 28.00, 49.0},
                {"GELETVLDEQK", 23.20, 52.8},
                {"KSLWPFGGPFK", 35.46, 72.0},
                {"KPESVLNTFSSK", 23.26, 50.0},
                {"KSSISYHNINAK", 15.73, 37.0},
                {"FGSLFEVGPSQEK", 29.86, 59.2},
                {"NIENYGLAVLEIK", 35.30, 72.1},
                {"EEFFFPYDNEER", 32.62, 63.0},
                {"SPFNIFSNNPAFSNK", 32.81, 68.9},
                {"KEEFFFPYDNEER", 32.72, 61.3},
                {"EVAFDIAAEKVDEVFER", 44.39, 77.0},
                {"ANAFLSPHHYDSEAILFNIK", 42.20, 72.1},
                {"LYIAAFHMPPSSGSAPVNLEPFFESAGR", 44.37, 78.0},
                {"EHEEEEEQEQEEDENPYVFEDNDFETK", 29.16, 56.0},
                {"HKEHEEEEEQEQEEDENPYVFEDNDFETK", 26.50, 56.0},
                {"QHEPR", 2.44, 14.0},
                {"SPQDER", 1.80, 15.0},
                {"RQQQQR", 1.77, 11.0},
                {"IVNSEGNK", 5.04, 17.0},
                {"HSQVAQIK", 10.92, 28.0},
                {"LRSPQDER", 6.02, 26.0},
                {"GDLYNSGAGR", 12.19, 30.0},
                {"LSAEYVLLYR", 32.50, 64.0},
                {"AAVSHVNQVFR", 23.14, 44.4},
                {"ATPGEVLANAFGLR", 33.49, 76.8},
                {"ISTVNSLTLPILR", 37.05, 71.0},
                {"KEEEEEEQEQR", 4.03, 21.3},
                {"HSEKEEEDEDEPR", 5.94, 25.0},
                {"KEDEDEDEEEEEER", 6.39, 26.0},
                {"GVLGLAVPGCPETYEEPR", 33.41, 63.0},
                {"VFYLGGNPEIEFPETQQK", 37.06, 66.0},
                {"VESEAGLTETWNPNHPELK", 31.39, 54.7},
                {"VEDGLHIISPELQEEEEQSHSQR", 28.77, 58.4},
                {"TIDPNGLHLPSYSPSPQLIFIIQGK", 45.07, 80.0},
                {"GGQQQEEESEEQNEGNSVLSGFNVEFLAHSLNTK", 37.57, 78.0},
                {"RGGQQQEEESEEQNEGNSVLSGFNVEFLAHSLNTK", 36.99, 78.0},
                {"ALEAFK", 16.38, 39.0},
                {"TFLWGR", 26.93, 56.0},
                {"NEPWWPK", 25.98, 55.3},
                {"LLYPHYR", 22.29, 46.0},
                {"SDYVYLPR", 25.01, 49.0},
                {"EEELNNLR", 15.37, 38.0},
                {"GSAEFEELVK", 26.15, 52.0},
                {"SSDFLTYGLK", 29.89, 58.0},
                {"ELVEVGHGDKK", 14.09, 35.0},
                {"DNPNWTSDKR", 11.67, 31.8},
                {"HASDELYLGER", 21.11, 46.0},
                {"LPTNILSQISPLPVLK", 43.30, 80.3},
                {"NWVFTEQALPADLIK", 40.97, 75.0},
                {"FQTLIDLSVIEILSR", 56.36, 94.0},
                {"EHLEPNLEGLTVEEAIQNK", 36.57, 64.8},
                {"ATFLEGIISSLPTLGAGQSAFK", 52.05, 94.0},
                {"IFFANQTYLPSETPAPLVHYR", 43.17, 70.0},
                {"IYDYDVYNDLGNPDSGENHARPVLGGSETYPYPR", 36.67, 64.0},
                {"SQIVR", 8.97, 22.0},
                {"VEGGLR", 8.67, 24.0},
                {"SEFDR", 7.50, 25.0},
                {"HSYPVGR", 10.87, 27.9},
                {"EQSHSHSHR", -0.82, 13.0},
                {"TANSLTLPVLR", 29.66, 61.0},
                {"AAVSHVQQVLR", 23.22, 44.0},
                {"ENIADAAGADLYNPR", 27.31, 51.0},
                {"EEEEEEEEDEEKQR", 5.84, 28.0},
                {"IRENIADAAGADLYNPR", 28.95, 53.0},
                {"VESEAGLTETWNPNNPELK", 31.91, 54.9},
                {"VFYLGGNPETEFPETQEEQQGR", 32.30, 61.1},
                {"TIDPNGLHLPSFSPSPQLIFIIQGK", 48.01, 85.0},
                {"GQLVVVPQNFVVAEQAGEEEGLEYVVFK", 48.85, 81.0},
                {"KGQLVVVPQNFVVAEQAGEEEGLEYVVFK", 47.37, 80.3},
                {"LLENQK", 8.32, 24.0},
                {"QIEELSK", 12.03, 32.0},
                {"NQVQSYK", 6.05, 23.0},
                {"FFEITPK", 25.11, 52.0},
                {"NENQQGLR", 6.30, 21.0},
                {"KQIEELSK", 13.20, 32.0},
                {"ILLEEHEK", 18.62, 40.3},
                {"EEDDEEEEQR", 4.04, 20.0},
                {"DLTFPGSAQEVDR", 24.13, 50.4},
                {"QSYFANAQPQQR", 15.52, 38.0},
                {"ILLEEHEKETHHR", 17.28, 42.4},
                {"NFLAGEEDNVISQIQK", 32.48, 65.0},
                {"LTPGDVFVIPAGHPVAVR", 37.28, 66.1},
                {"EEDDEEEEQREEETK", 5.89, 28.0},
                {"ASSNLNLLGFGINAENNQR", 35.42, 66.0},
                {"NPQLQDLDIFVNYVEIK", 46.41, 82.1},
                {"KNPQLQDLDIFVNYVEIK", 45.53, 80.2},
                {"NENQQGLREEDDEEEEQR", 10.37, 32.0},
                {"GDQYAR", 3.50, 18.0},
                {"GDYYAR", 7.60, 26.0},
                {"EVYLFK", 24.15, 52.0},
                {"GKEVYLFK", 25.17, 50.0},
                {"VLYGPTPVR", 23.15, 42.2},
                {"TGYINAAFR", 23.93, 48.2},
                {"TNEVYFFK", 28.18, 55.0},
                {"TLDYWPSLR", 32.85, 62.7},
                {"KTLDYWPSLR", 32.13, 60.9},
                {"VLYGPTPVRDGFK", 27.02, 50.0},
                {"YVLLDYAPGTSNDK", 31.20, 56.0},
                {"SSQNNEAYLFINDK", 26.36, 54.3},
                {"NTIFESGTDAAFASHK", 26.97, 54.1}
            };

            List<double> xList = new List<double>();
            List<double> yList = new List<double>();
            for (int i = 0; i < peptideResults.GetLength(0); i++)
            {
                xList.Add((double) peptideResults[i, 1]);
                yList.Add((double) peptideResults[i, 2]);
            }
            _xValues = new Statistics(xList.ToArray());
            _yValues = new Statistics(yList);
            _xMember = (double) peptideResults[10, 1];
            _yMember = (double) peptideResults[10, 2];

            // For weighted tests
            // http://www.bmj.com/cgi/content/full/316/7125/129
            double[,] tableRequests =
            {
                {20, 100, 7, 100},
                {7, 100, 37, 89},
                {16, 94, 38, 84},
                {31, 90, 28, 82},
                {20, 90, 20, 80},
                {24, 88, 19, 79},
                {7, 86, 9, 78},
                {6, 83, 25, 76},
                {30, 83, 120, 75},
                {66, 80, 88, 73},
                {5, 80, 22, 68},
                {43, 77, 76, 68},
                {43, 74, 21, 67},
                {23, 70, 126, 66},
                {64, 69, 22, 64},
                {6, 67, 34, 62},
                {18, 56, 10, 40},
            };
            int col = 0;
            _interventionRequests = StatsFromTable(tableRequests, col++);
            _interventionPercents = StatsFromTable(tableRequests, col++);
            _controlRequests = StatsFromTable(tableRequests, col++);
            _controlPercents = StatsFromTable(tableRequests, col);
        }

        private static Statistics StatsFromTable(double[,] tableRequests, int col)
        {
            double[] values = new double[tableRequests.GetLength(0)];
            for (int i = 0; i < values.Length; i++)
                values[i] = tableRequests[i, col];
            return new Statistics(values);
        }

        /// <summary>
        /// A test for Length
        /// </summary>
        [TestMethod]
        public void LengthTest()
        {
            Assert.AreEqual(266, _xValues.Length);
            Assert.AreEqual(_xValues.Length, _yValues.Length);
        }

        /// <summary>
        ///A test for Z
        ///</summary>
        [TestMethod]
        public void ZTest()
        {
            Assert.AreEqual(-1.59, _xValues.Z(_xMember), 0.01);
            Assert.AreEqual(-1.80, _yValues.Z(_yMember), 0.01);
        }

        /// <summary>
        ///A test for YULE
        ///</summary>
        [TestMethod]
        public void YULETest()
        {
            Assert.AreEqual(-0.21, _xValues.YULE(), 0.01);
            Assert.AreEqual(-0.18, _yValues.YULE(), 0.01);
        }

        /// <summary>
        ///A test for Variance
        ///</summary>
        [TestMethod]
        public void VarianceTest()
        {
            Assert.AreEqual(181.33, _xValues.Variance(), 0.01);
            Assert.AreEqual(410.41, _yValues.Variance(), 0.01);
        }

        /// <summary>
        ///A test for weighted Variance
        ///</summary>
        [TestMethod]
        public void VarianceWeightedTest()
        {
            // Different from the article due to greater precision in the Statistics calculations
            Assert.AreEqual(/*109.77*/ 109.97, _interventionPercents.Variance(_interventionRequests), 0.01);
            Assert.AreEqual(/*76.24*/ 75.73, _controlPercents.Variance(_controlRequests), 0.01);
        }

        /// <summary>
        /// A test for StdDev
        /// </summary>
        [TestMethod]
        public void StdDevTest()
        {
            Assert.AreEqual(13.47, _xValues.StdDev(), 0.01);
            Assert.AreEqual(20.26, _yValues.StdDev(), 0.01);
        }

        /// <summary>
        ///A test for weighted StdDev
        ///</summary>
        [TestMethod]
        public void StdDevWeightedTest()
        {
            // Different from the article due to greater precision in the Statistics calculations
            Assert.AreEqual(10.48, _interventionPercents.StdDev(_interventionRequests), 0.01);
            Assert.AreEqual(/*8.73*/ 8.70, _controlPercents.StdDev(_controlRequests), 0.01);
        }

        /// <summary>
        /// A test for Range
        /// </summary>
        [TestMethod]
        public void RangeTest()
        {
            Assert.AreEqual(57.18, _xValues.Range());
            Assert.AreEqual(83.7, _yValues.Range());
        }

        /// <summary>
        /// A test for R
        /// </summary>
        [TestMethod]
        public void RTest()
        {
            double r = Statistics.R(_xValues, _yValues);
            double r2 = _xValues.R(_yValues);
            Assert.AreEqual(0.989, r, 0.001);
            Assert.AreEqual(r, r2);
        }

        /// <summary>
        /// A test for Percentile
        /// </summary>
        [TestMethod]
        public void Percentile()
        {
            Assert.AreEqual(4.21, _xValues.Percentile(0.1), 0.001);
            Assert.AreEqual(20.0, _yValues.Percentile(0.1), 0.001);
            for (int i = 1; i < 10; i++)
            {
                double p = i*0.1;
                Assert.AreEqual(_xValues.PercentileExcelSorted(p), _xValues.Percentile(p));
                Assert.AreEqual(_yValues.PercentileExcelSorted(p), _yValues.QPercentile(p));
            }
        }

        /// <summary>
        /// A test for Q1
        /// </summary>
        [TestMethod]
        public void Q1Test()
        {
            Assert.AreEqual(11.35, _xValues.Q1(), 0.001);
            Assert.AreEqual(31.85, _yValues.Q1(), 0.001);
        }

        /// <summary>
        /// A test for Q3
        /// </summary>
        [TestMethod]
        public void Q3Test()
        {
            Assert.AreEqual(31.9175, _xValues.Q3(), 0.001);
            Assert.AreEqual(60.975, _yValues.Q3(), 0.001);
        }

        /// <summary>
        /// A test for IQ
        /// </summary>
        [TestMethod]
        public void IQTest()
        {
            Assert.AreEqual(20.5675, _xValues.IQ());
            Assert.AreEqual(29.125, _yValues.IQ());
        }

        /// <summary>
        /// A test for Mode
        /// </summary>
        [TestMethod]
        public void ModeTest()
        {
            Assert.AreEqual(double.NaN, _xValues.Mode());    // Excel says 16.5
            Assert.AreEqual(56, _yValues.Mode());
        }

        /// <summary>
        /// A test for Min
        /// </summary>
        [TestMethod]
        public void MinTest()
        {
            Assert.AreEqual(-0.82, _xValues.Min());
            Assert.AreEqual(11, _yValues.Min());
        }

        /// <summary>
        /// A test for Max
        /// </summary>
        [TestMethod]
        public void MaxTest()
        {
            Assert.AreEqual(56.36, _xValues.Max());
            Assert.AreEqual(94.7, _yValues.Max());
        }

        /// <summary>
        /// A test for MiddleOfRange
        /// </summary>
        [TestMethod]
        public void MiddleOfRangeTest()
        {
            Assert.AreEqual(27.77, _xValues.MiddleOfRange());
            Assert.AreEqual(52.85, _yValues.MiddleOfRange());
        }

        /// <summary>
        /// A test for Median
        /// </summary>
        [TestMethod]
        public void MedianTest()
        {
            Assert.AreEqual(23.805, _xValues.PercentileExcelSorted(0.5));
            Assert.AreEqual(49, _yValues.PercentileExcelSorted(0.5));
            Assert.AreEqual(25.5, new Statistics(new []{25.5}).PercentileExcelSorted(0.5));
            Assert.AreEqual(23.805, _xValues.Median());
            Assert.AreEqual(49, _yValues.Median());
            Assert.AreEqual(25.5, new Statistics(new[] {25.5}).Median());
            Assert.AreEqual(3.0, new Statistics(new[] {1.0, 3.0, 5.0}).Median());
            Assert.AreEqual(2.5, new Statistics(new[] {1.0, 2.0, 3.0, 5.0}).Median());
        }

        /// <summary>
        /// A test for Mean
        /// </summary>
        [TestMethod]
        public void MeanTest()
        {
            Assert.AreEqual(22.75, _xValues.Mean(), 0.01);
            Assert.AreEqual(47.41, _yValues.Mean(), 0.01);
        }

        /// <summary>
        /// A test for weighted Mean
        /// </summary>
        [TestMethod]
        public void MeanWeightedTest()
        {
            Statistics s = new Statistics(80, 90);
            Statistics weights = new Statistics(20, 30);
            Assert.AreEqual(86, s.Mean(weights));
            Assert.AreEqual(79.50, _interventionPercents.Mean(_interventionRequests), 0.01);
        }

        /// <summary>
        /// A test for Covariance
        /// </summary>
        [TestMethod]
        public void CovarianceTest()
        {
            double c = Statistics.Covariance(_xValues, _yValues);
            double c2 = _xValues.Covariance(_yValues);
            Assert.AreEqual(269.88, c, 0.01);   // TODO: Excel reports 268.86
            Assert.AreEqual(c, c2);
        }

        /// <summary>
        /// A test for the b term in a linear regression function y = a*x + b
        /// </summary>
        [TestMethod]
        public void BTermTest()
        {
            double bterm = Statistics.BTerm2(_yValues, _xValues);
            double bterm2 = _yValues.BTerm2(_xValues);
            Assert.AreEqual(13.55, bterm, 0.01);
            Assert.AreEqual(bterm, bterm2);
            Assert.AreEqual(bterm, Statistics.Intercept(_yValues, _xValues));
            Assert.AreEqual(bterm2, _yValues.Intercept(_xValues));
        }

        /// <summary>
        /// A test of linear regression values on real CE optimization data.
        /// </summary>
        [TestMethod]
        public void RegressionTest()
        {
            var statPrecursorMzs = new Statistics(new[]
                {
                    458.740356,
                    533.294964,
                    623.29589,
                    471.256174,
                    509.751255,
                    490.245806,
                    506.774731,
                    487.281857,
                    500.731477,
                    713.317688,
                    621.298432,
                    499.272978,
                    530.270147,
                    869.449568,
                    482.266541,
                    540.290213,
                    692.868631,
                    634.355888,
                    582.318971,
                    653.361701
                });

            var statCEs = new Statistics(new[]
                {
                    17.911172,
                    18.446029,
                    25.50606,
                    17.33671,
                    23.645543,
                    18.982357,
                    19.544341,
                    17.881583,
                    18.33887,
                    24.566801,
                    22.438147,
                    17.289281,
                    23.343185,
                    29.875285,
                    17.711062,
                    18.683867,
                    23.871533,
                    22.8821,
                    20.112845,
                    25.528298
                });

            // Values for this CE optimization regression were verified in Excel
            Assert.AreEqual(statCEs.ATerm2(statPrecursorMzs), 0.029982, 0.000001);            
            Assert.AreEqual(statCEs.BTerm2(statPrecursorMzs), 4.104255, 0.0000001);
            Assert.AreEqual(statCEs.StdErrATerm2(statPrecursorMzs), 0.003872, 0.000001);
            Assert.AreEqual(statCEs.StdErrBTerm2(statPrecursorMzs), 2.241899, 0.000001);
        }

        /// <summary>
        /// A test for the a term in a linear regression function y = a*x + b
        /// </summary>
        [TestMethod]
        public void ATermTest()
        {
            double aterm = Statistics.ATerm2(_yValues, _xValues);
            double aterm2 = _yValues.ATerm2(_xValues);
            Assert.AreEqual(1.49, aterm, 0.01);
            Assert.AreEqual(aterm, aterm2);
            Assert.AreEqual(aterm, Statistics.Slope(_yValues, _xValues));
            Assert.AreEqual(aterm2, _yValues.Slope(_xValues));
        }

        /// <summary>
        /// A test for the Costa Soares score
        /// </summary>
        [TestMethod]
        public void CostaSoaresTest()
        {
            // Exact same order should produce 1.0
            Statistics stat1 = new Statistics(new[] { 1.0, 2, 3, 4 });
            Statistics stat2 = new Statistics(new[] { 1.0, 2, 3, 4 });
            Assert.AreEqual(1.0, stat1.CostaSoares(stat2));

            // Same order but differing intensities should also produce 1.0,
            // since this is a rank correlation
            stat2 = new Statistics(new[] { 5.0, 6, 7, 8 });
            Assert.AreEqual(1.0, stat1.CostaSoares(stat2));

            // Flipping the order of larger numbers should yeild a lower score
            // than flipping smaller numbers
            stat2 = new Statistics(new[] { 1.0, 2, 4, 3 });
            double cs1 = stat1.CostaSoares(stat2);
            stat2 = new Statistics(new[] { 2.0, 1, 3, 4 });
            double cs2 = stat1.CostaSoares(stat2);
            Assert.IsTrue(cs1 < cs2);

            // Swapping highest rank with lowest should be even worse
            stat2 = new Statistics(new[] { 4.0, 2, 3, 1 });
            Assert.IsTrue(stat1.CostaSoares(stat2) < cs1);

            // Two same ranked should have less impact than flipping
            stat2 = new Statistics(new[] { 1.0, 1, 3, 4 });
            double cs3 = stat1.CostaSoares(stat2);
            Assert.IsTrue(cs2 <= cs3);
            // Well, actually it appears to yield 1.0, which seems broken
            // Assert.AreEqual(1.0, cs3);
            // Try the same thing with 1 and 2 rankings
            stat2 = new Statistics(new[] { 1.0, 2, 3, 3.0 });
            cs3 = stat1.CostaSoares(stat2);
            Assert.IsTrue(cs1 <= cs3);
            // Again this yields 1.0, which seems more broken
            // Assert.AreEqual(1.0, cs3);
            // Try everything equal
            // stat2 = new Statistics(new[] { 1.0, 1, 1, 1 });
            // cs3 = stat1.CostaSoares(stat2);
            // Wow! 1.0 again.  Now that seems really broken
            // Assert.AreEqual(1.0, cs3);

            // Reversing the order should yield -1
            stat2 = new Statistics(new[] { 4.0, 3, 2, 1 });
            Assert.AreEqual(-1.0, stat1.CostaSoares(stat2));

            // Reordering smaller numbers below the top 4 should have no impact
            stat1 = new Statistics(new[] { 1.0, 2, 3, 4, 5, 6, 7, 8 });
            stat2 = new Statistics(new[] { 2.0, 1, 4, 3, 5, 6, 7, 8 });
            cs3 = stat1.CostaSoares(stat2, 4);
            Assert.AreEqual(1.0, cs3);
            cs3 = stat1.CostaSoares(stat2);
            Assert.IsTrue(cs3 > 0.95);

            // All same intensity of lower ranked numbers should also have no impact
            stat2 = new Statistics(new[] { 0.0, 0, 0, 0, 5, 6, 7, 8 });
            cs3 = stat1.CostaSoares(stat2, 4);
            Assert.AreEqual(1.0, cs3);
            // And some similar tests of negative correlation
            stat2 = new Statistics(new[] { 8.0, 7, 6, 5, 0, 0, 0, 0 });
            cs3 = stat1.CostaSoares(stat2, 4);
            Assert.AreEqual(-1.0, cs3);
            stat2 = new Statistics(new[] { 8.0, 7, 6, 5, 4, 3, 0, 0 });
            cs3 = stat1.CostaSoares(stat2, 4);
            Assert.AreEqual(-1.0, cs3);
            stat2 = new Statistics(new[] { 8.0, 7, 6, 5, 4, 3, 2, 1 });
            cs3 = stat1.CostaSoares(stat2);
            Assert.AreEqual(-1.0, cs3);

            // And perhaps the most broken thing of all, just having the most
            // intense peak in the right place for a set of 8 gives you 1.0
            // But this works with more recent fixes.
            stat2 = new Statistics(new[] { 0.0, 0, 0, 0, 0, 0, 0, 8 });
            cs3 = stat1.CostaSoares(stat2);
            Assert.IsTrue(cs3 < 0);

            // But swapping highest and lowest should still be worse than
            // swapping second highest and second lowest.
            stat2 = new Statistics(new[] { 8.0, 2, 3, 4, 5, 6, 7, 1 });
            cs1 = stat1.CostaSoares(stat2);
            stat2 = new Statistics(new[] { 1.0, 7.0, 3, 4, 5, 6, 2, 8 });
            cs2 = stat1.CostaSoares(stat2);
            Assert.IsTrue(cs1 < cs2);
        }
    }
}
