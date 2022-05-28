using MathNet.Numerics.LinearRegression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class BilinearFitTest : AbstractUnitTest
    {
        const double delta = 1e-6;
        [TestMethod]
        public void TestFitBilinearCurve()
        {
            var points = GetWeightedPoints();
            var result = new BilinearCurveFitter().FitBilinearCurve(points);
            Assert.AreEqual(0.025854009062249942, result.Slope, delta);
            Assert.AreEqual(0.16317916521766687, result.Intercept, delta);
            Assert.AreEqual(0.172315, result.BaselineHeight, delta);
            Assert.AreEqual(0.057632885215053185, result.Error, delta);
            Assert.AreEqual(0.0008070195509128315, result.StdDevBaseline, delta);
        }

        [TestMethod]
        public void TestBilinearFitWithOffset()
        {
            var points = GetWeightedPoints();
            var result = new BilinearCurveFitter().FitBilinearCurveWithOffset(0.03, points);
            Assert.AreEqual(0.00239897, result.Slope, delta);
            Assert.AreEqual(0.16965172, result.Intercept, delta);
            Assert.AreEqual(0.17205111, result.BaselineHeight, delta);
            Assert.AreEqual(0.07240250761116633, result.Error, delta);
            Assert.AreEqual(0.0007680004501027473, result.StdDevBaseline, delta);
        }

        [TestMethod]
        public void TestBilinearFitTooFewPoints()
        {
            var points = GetWeightedPoints();
            var result = new BilinearCurveFitter().FitBilinearCurveWithOffset(0.7, points);
            Assert.AreEqual(0, result.Slope);
            Assert.AreEqual(0, result.Intercept);
            Assert.AreEqual(0.17326666666666662, result.BaselineHeight, delta);
            Assert.AreEqual(0.0648963223840987, result.Error, delta);
            Assert.AreEqual(0.00626862504860516, result.StdDevBaseline, delta);

        }

        [TestMethod]
        public void TestWeightedRegression()
        {
            var x = new[] {0.05, 0.07, 0.1, 0.3, 0.5, 0.7, 1.0};
            var y = new[]
            {
                0.17310666666666666, 0.1674433333333333, 0.15965666666666667, 0.18022333333333332, 0.18300000000000002,
                0.17776666666666666, 0.17531666666666668
            };
            var weights = new[]
            {
                20.0, 14.285714285714285, 10.0, 3.3333333333333335, 2.0, 1.4285714285714286, 1.0
            };
            var result = WeightedRegression.Weighted(x.Zip(y, (a, b) => Tuple.Create(new []{a}, b)), weights, true);
            // Assert.AreEqual(0.00239897, result[0], 1e-6);
            // Assert.AreEqual(0.16965172, result[1], 1e-6);
            var difference1 = CalculateSumOfDifferences(x, y, weights, result[1], result[0], false);
            var difference2 = CalculateSumOfDifferences(x, y, weights, .00239897, .16965172, false);
            var difference3 = CalculateSumOfDifferences(x, y, weights, 0.00113951, 0.17091118, false);
            Assert.IsTrue(difference1 < difference2);
            Assert.IsTrue(difference1 < difference3);
        }

        public static double CalculateSumOfDifferences(IList<double> x, IList<double> y, IList<double> weights, double slope,
            double intercept, bool squareArea)
        {
            double sum = 0;
            for (int i = 0; i < x.Count; i++)
            {
                var expected = slope * x[i] + intercept;
                var difference = y[i] - expected;
                sum += difference * difference * weights[i];
            }

            return sum;
        }

        private IList<WeightedPoint> GetWeightedPoints()
        {
            var areas = new[]
            {
                0.16751, 0.17056, 0.18132, 0.17482, 0.17060, 0.16879, 0.17469, 0.17645, 0.16372, 0.17310, 0.17941,
                0.16681, 0.18539, 0.17096, 0.14598, 0.15127, 0.17316, 0.15454, 0.18983, 0.17845, 0.17239, 0.18395,
                0.18260, 0.18245, 0.19410, 0.16476, 0.17444, 0.18214, 0.17844, 0.16537
            };
            var concentrations = new[]
            {
                0.005, 0.005, 0.005, 0.01, 0.01, 0.01, 0.03, 0.03, 0.03, 0.05, 0.05, 0.05, 0.07, 0.07, 0.07,
                0.1, 0.1, 0.1, 0.3, 0.3, 0.3, 0.5, 0.5, 0.5, 0.7, 0.7, 0.7, 1.0, 1.0, 1.0
            };
            return MakeWeightedPoints(concentrations, areas);
        }

        private IList<WeightedPoint> MakeWeightedPoints(IList<double> concentrations, IList<double> areas)
        {
            Assert.AreEqual(concentrations.Count, areas.Count);
            return concentrations
                .Zip(areas, (conc, area) => Tuple.Create(conc, area)).ToLookup(tuple => tuple.Item1)
                .Select(grouping => new WeightedPoint(grouping.Key, grouping.Average(tuple => tuple.Item2),
                    Math.Pow(grouping.Key <= 0 ? 1e-6 : 1 / grouping.Key, 2))).ToList();
        }

        [TestMethod]
        public void TestComputeLod()
        {
            var points = GetWeightedPoints();
            var lod = new BilinearCurveFitter().ComputeLod(points);
            Assert.AreEqual(0.3845768874492954, lod, delta);
        }

        [TestMethod]
        public void TestComputeLoq()
        {
            var areas = new double[]
            {
                0.00000000000000000000, 0.00000000000000000000, 0.00000000000000000000, 0.00000000000000000000,
                0.00000000000000000000, 0.00000000000000000000, 0.00000000000000000000, 0.00000000000000000000,
                0.00000000000000000000, 0.00014956993646152492, 0.00014995676055750597, 0.00000000000000000000,
                0.00017176402663390654, 0.00043060652812320662, 0.00000000000000000000, 0.00047242916172544338,
                0.00024007723157733063, 0.00016317133730713851, 0.00224918647629133951, 0.00208678377690913949,
                0.00126969307708830310, 0.00352413988932613045, 0.00360755126030127627, 0.00287816697272668147,
                0.00512273079101743887, 0.00491574289577129727, 0.00501447277612654049, 0.00750785941174682541,
                0.00808758893988542615, 0.00522947445339865240
            };
            var concentrations = new double[]
            {0.005, 0.005, 0.005, 0.01, 0.01, 0.01, 0.03, 0.03, 0.03, 0.05, 0.05, 0.05, 0.07, 0.07, 0.07, 0.1, 0.1,
                0.1, 0.3, 0.3, 0.3, 0.5, 0.5, 0.5, 0.7, 0.7, 0.7, 1.0, 1.0, 1.0
            };
            var weightedPoints = MakeWeightedPoints(concentrations, areas);
            var loq = new BilinearCurveFitter()
            {
                MaxBootstrapIterations = 10000,
                MinBootstrapIterations = 1000,
                Random = new Random(0)
            }.ComputeBootstrappedLoq(weightedPoints);
            Assert.AreEqual(0.07947949229870066, loq, delta);
        }

        [TestMethod]
        public void TestComputeQuantitativeLimits()
        {
            var concentrations = datasetConcentrations;
            var areas = datasetTransitionAreas;
            int numTransitions = areas.GetLength(1);

            double[] expectedLoqs = new[]
            {
                0.38469098478247465,
                0.005,
                1,
                .005,
                1.0,
                0.8494518939992266
            };
            double[] expectedLods = new[]
            {
                0.38469098478247465,
                0.005,
                1,
                .005,
                1,
                0.8494518939992266
            };
            for (int iTransition = 0; iTransition < numTransitions; iTransition++)
            {
                var transitionAreas =
                    Enumerable.Range(0, areas.GetLength(0)).Select(i => areas[i, iTransition]).ToList();
                Assert.AreEqual(concentrations.Length, transitionAreas.Count);
                var weightedPoints = MakeWeightedPoints(concentrations, transitionAreas);
                var lod = new BilinearCurveFitter().ComputeLod(weightedPoints);
                Assert.AreEqual(expectedLods[iTransition], lod, delta, "Lod Mismatch Transition #{0}", iTransition);
                var loq = new BilinearCurveFitter() {MaxBootstrapIterations = 10000, Random = new Random(0)}.ComputeBootstrappedLoq(weightedPoints);
                Assert.AreEqual(expectedLoqs[iTransition], loq, delta, "Loq Mismatch Transition #{0}", loq);
            }
        }

        [TestMethod]
        public void TestOptimizeTransitions()
        {
            var concentrations = datasetConcentrations;
            var areas = datasetTransitionAreas;
            var curveFitter = new BilinearCurveFitter() {Random = new Random(0), MinNumTransitions = 4};
            var weightedPoints = new List<IList<WeightedPoint>>();
            for (int iTransition = 0; iTransition < areas.GetLength(1); iTransition++)
            {
                var transitionAreas =
                    Enumerable.Range(0, areas.GetLength(0)).Select(i => areas[i, iTransition]).ToList();
                Assert.AreEqual(concentrations.Length, transitionAreas.Count);
                weightedPoints.Add(MakeWeightedPoints(concentrations, transitionAreas));
            }

            var result = curveFitter.OptimizeTransitions(OptimizeType.LOQ, weightedPoints);
            var acceptedIndices = result.Item1.ToList();
            CollectionAssert.AreEqual(new[]{1,3,0,5}, acceptedIndices);
            Assert.AreEqual(result.Item2.Lod, 0.3719707784516309, delta);
            Assert.AreEqual(result.Item2.Loq, 0.3719707784516309, delta);
        }

        private double[] datasetConcentrations =
        {
            0.005, 0.005, 0.005, 0.01, 0.01, 0.01, 0.03, 0.03, 0.03, 0.05, 0.05, 0.05, 0.07, 0.07, 0.07, 0.1, 0.1,
            0.1, 0.3, 0.3, 0.3, 0.5, 0.5, 0.5, 0.7, 0.7, 0.7, 1.0, 1.0, 1.0
        };
        private double[,] datasetTransitionAreas = new double[,]
        {
            {
                0.16751410125091631409, 3.00915079259725359861, 1.11722399188912291379, 0.81733198743689461363,
                0.12911616716360180268, 1.76430493997375426041
            },
            {
                0.17056394539830774248, 3.08373313809153781762, 1.13061528477993755715, 0.80732723538952655407,
                0.12787802569243905682, 1.84248540882006084374
            },
            {
                0.18132310829694300858, 3.24233390929768638955, 1.18128937847621551249, 0.85079886906821078352,
                0.13501460057716671570, 1.95418527070999004103
            },
            {
                0.17481819757815444949, 3.14519964096282622634, 1.16194550444372302067, 0.84589382517661160232,
                0.13508790357849004282, 1.84465724689993781915
            },
            {
                0.17060122098892124831, 3.07519206143569467926, 1.13508303511806163399, 0.82676964263446861558,
                0.13069535785294938979, 1.80006361742260390102
            },
            {
                0.16878907424836195328, 2.99023303660873818188, 1.10796399070380990892, 0.80467498930601510931,
                0.12953521203013307339, 1.74539885261277483153
            },
            {
                0.17468792073850866742, 3.17047914972316879911, 1.14951316181279317163, 0.82580933932542577303,
                0.12924791200364865729, 1.92382819540429350624
            },
            {
                0.17644813912927478916, 3.16266032787611184318, 1.15579271256038373927, 0.82607994740327361782,
                0.13136284963230590583, 1.90994582329214979133
            },
            {
                0.16371742156407967372, 2.93390002228872015522, 1.06425870398444111231, 0.76469346822680273057,
                0.12051403804917687479, 1.79114406127301717397
            },
            {
                0.17310143138183167744, 3.12008170056664502212, 1.14222571603614153624, 0.84626334897408761471,
                0.13228533175984083514, 1.88452998885256728379
            },
            {
                0.17941092540284822587, 3.18994166498724851522, 1.16723041860580090123, 0.84223777232106122881,
                0.13567487621890658711, 1.90515943209900373567
            },
            {
                0.16680693631785328823, 2.97506746338734862078, 1.08601821075886872947, 0.78130253234326596523,
                0.12340016431457509483, 1.79878807075162883145
            },
            {
                0.18538729178290436206, 3.32346208078049842882, 1.21955808108675944901, 0.89819814757896510038,
                0.14254403280273669763, 1.99490649498965288977
            },
            {
                0.17095864517463760235, 3.07278169978321313849, 1.13382854900306573320, 0.82496287387962941029,
                0.13212539993100691493, 1.80163225873199572824
            },
            {
                0.14598190411813735667, 2.63538417912891809181, 0.96009508581517444270, 0.68285107553856216889,
                0.10963703742415030484, 1.61974864019682951444
            },
            {
                0.15127294198874896569, 2.76797571667984554367, 1.00131174024903035757, 0.74272885343745254083,
                0.11568577259276509317, 1.67863335202872576701
            },
            {
                0.17316355725229937157, 3.08448428824979670182, 1.13110446538521980386, 0.81096308398055205746,
                0.12886961092506288296, 1.83862629452985837375
            },
            {
                0.15453859890854404480, 2.76473630136679071612, 1.00492242194850711634, 0.71682506313519878116,
                0.11317058839718631413, 1.69002739217342345945
            },
            {
                0.18982538603861914828, 3.39261069576720020180, 1.24612924623786747169, 0.90024823562800304622,
                0.14278960357599213005, 1.99268257893363887057
            },
            {
                0.17844572795585969538, 3.18818628227067346614, 1.16404971605602924889, 0.83902221845562552360,
                0.13554030398107935751, 1.89428754851058034347
            },
            {
                0.17238551978383606644, 3.09329454290608119038, 1.13102919399957557722, 0.81038456879063536231,
                0.12711912934616806381, 1.87121476309031087304
            },
            {
                0.18394863874458500241, 3.33771447258528475288, 1.21277112704675582577, 0.86201922340582914916,
                0.13690577022933755891, 2.01117399887458603303
            },
            {
                0.18259777255768575022, 3.30037645693274628300, 1.21004354188978990869, 0.87504332168959386706,
                0.13775243071866857814, 1.96382259175069884272
            },
            {
                0.18244797451097016783, 3.27943494260390311368, 1.18105644311086832587, 0.84827992051136247298,
                0.13170173121075218203, 2.01662877169065213323
            },
            {
                0.19410055743442611309, 3.54058006235680222673, 1.29093627572236591128, 0.93412125614783148020,
                0.14647199050169787404, 2.10869118454420956255
            },
            {
                0.16475701451119897922, 2.93593611803801834981, 1.06142590693418203962, 0.76248957669323913500,
                0.11993870622347656274, 1.78616848948781581363
            },
            {
                0.17443985536651376855, 3.14415803684663064388, 1.12768336328078278008, 0.80376776775617686521,
                0.12551374163456885391, 1.92247991099106729784
            },
            {
                0.18213835271296621721, 3.38337386031157771882, 1.22164867564245716025, 0.87152930939957340417,
                0.13596534450530833871, 2.04801854445343733957
            },
            {
                0.17843792934976931974, 3.17653841038631901128, 1.19604085277728144909, 0.88199438514931927102,
                0.14300379959229159166, 1.84532059820752047941
            },
            {
                0.16536813312110928975, 2.96369601944129090754, 1.08120101842486260324, 0.78330633583208053583,
                0.12237734093613208963, 1.83490184829576885583
            },
        };
    }
}
