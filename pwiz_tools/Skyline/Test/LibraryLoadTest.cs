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
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Summary description for NistLibraryTest
    /// </summary>
    [TestClass]
    public class LibraryLoadTest : AbstractUnitTest
    {
        public const string PATH_NIST_LIB = @"C:\Libraries\NIST_yeast_v2.0_2008-07-11.msp";
        public const string PATH_NIST_LIB_CACHE = @"C:\Libraries\NIST_yeast_v2.0_2008-07-11" + NistLibrary.EXT_CACHE;
        public const string PATH_HUNTER_LIB = @"C:\Libraries\yeast_cmp_20.hlf";
        public const string PATH_BIBLIOSPEC_LIB = @"C:\Libraries\yeast.lib";
        public const string PATH_GMD_LIB = @"C:\Libraries\example_gmd.msp";
        // Cannot find a way to use anything but a file with SQLite
        // public const string _bibliospecLiteLibPath = @"C:\Libraries\yeast.blib";

        private List<SpectrumPeakAnnotation> MakeTestPeakAnnotation(Adduct adduct, float mz, string name, string note)
        {
            var ion = new CustomIon(null, adduct,
                adduct.MassFromMz(mz, MassType.Monoisotopic),
                adduct.MassFromMz(mz, MassType.Average),
                name);
            var annot = SpectrumPeakAnnotation.Create(ion, note);
            return new List<SpectrumPeakAnnotation> {annot};
        }

        [TestMethod]
        public void NistLoadLibrary()
        {
            var streamManager = new MemoryStreamManager();
            streamManager.TextFiles.Add(PATH_NIST_LIB, MODS_USING_PARENS + TEXT_LIB_YEAST_NIST +
                                                       TEXT_LIB_BICINE_NIST + TEXT_LIB_NO_ADDUCT +
                                                       TEXT_LIB_FORMULA_PLUS + TEXT_LIB_MINE +
                                                       LIB_TEXT_MONA + LIB_TEXT_MZVAULT +
                                                       LIB_TEXT_RTINSECONDS + TEXT_NIST_PARENTHESIS +
                                                       LIB_TEXT_NO_ADDUCT + TEXT_RIKEN + TEXT_USING_EXACTMASS_COMMENT +
                                                       TEXT_USING_EXACTMASS_ARG + NIST_TERM_MODS);
            var loader = new TestLibraryLoader {StreamManager = streamManager};
            var expectedFragmentAnnotations = new Dictionary<int, List<SpectrumPeakAnnotation>>
            {
                {3, MakeTestPeakAnnotation(Adduct.M_PLUS, 41.027f,"testfrag3", "85/86")},
                {44, MakeTestPeakAnnotation(Adduct.M_PLUS, 128.070f, "testfrag_next_to_last", "83/86")},
                {45, MakeTestPeakAnnotation(Adduct.M_PLUS, 146.081f, "testfrag_last", "85/86 note")},
            };

            var librarySpec = new NistLibSpec("Yeast (NIST)", PATH_NIST_LIB);

            Library lib1 = librarySpec.LoadLibrary(loader);
            CheckLibrary(lib1, 100);
            CheckLibrary(lib1, 46, KEYS_LIB_BICENE_NIST);

            Assert.AreEqual(streamManager.BinaryFiles.Count, 1);
            Assert.IsTrue(streamManager.IsCached(PATH_NIST_LIB, PATH_NIST_LIB_CACHE));

            // Corrupt the cache, and make sure it still possible to get
            // a valid library.
            byte[] cacheBytes = streamManager.BinaryFiles[PATH_NIST_LIB_CACHE];
            int len = cacheBytes.Length;
            byte[] corruptedBytes = new byte[len/2];
            Array.Copy(cacheBytes, corruptedBytes, corruptedBytes.Length);
            streamManager.BinaryFiles[PATH_NIST_LIB_CACHE] = corruptedBytes;

            // Check small molecule library with spectrum attributes
            TestSpectrumPeakAnnotations(); // First, a quick low-level unit test of annotations handler class
            Library lib2 = librarySpec.LoadLibrary(loader);
            CheckLibrary(lib2, 100);
            CheckLibrary(lib2, 46, KEYS_LIB_BICENE_NIST, expectedFragmentAnnotations);

            Assert.AreEqual(len, streamManager.BinaryFiles[PATH_NIST_LIB_CACHE].Length);
            Assert.IsTrue(lib1.IsSameLibrary(lib2));
            Assert.AreEqual(0, lib1.CompareRevisions(lib2));

            // Check ability to infer adduct from mz and formula
            var lib2Keys = lib2.Keys.ToArray();
            Assert.AreEqual(1, lib2Keys.Count(k => Equals("[M-H2O+H]", k.Adduct.AdductFormula)));

            // Check ability to parse strangely decorated formula
            Assert.AreEqual(5, lib2Keys.Count(k => Equals("[M+]", k.Adduct.AdductFormula)));
            Assert.AreEqual(1, lib2Keys.Count(k => Equals("[M3H2+]", k.Adduct.AdductFormula))); // Label found in InChi description as ".../i1D3"
            Assert.AreEqual(1, lib2Keys.Count(k => Equals("C11H22NO4", k.SmallMoleculeLibraryAttributes.ChemicalFormula)));
            Assert.AreEqual(1, lib2Keys.Count(k => Equals("C6H14FO2P1", k.SmallMoleculeLibraryAttributes.ChemicalFormula)));

            // Check use of "MW:"
            Assert.AreEqual(1, lib2Keys.Count(k => Equals(324.6, k.Target?.Molecule?.MonoisotopicMass.Value ?? 0)));

            // Check use of "ExactMass" in a comment
            Assert.AreEqual(1, lib2Keys.Count(k => Equals(330.991, k.Target?.Molecule?.MonoisotopicMass.Value ?? 0)));

            // Check use of "ExactMass" in an argument
            Assert.AreEqual(1, lib2Keys.Count(k => Equals(2912.256, k.Target?.Molecule?.MonoisotopicMass.Value ?? 0)));

            // Check use of GC not declaring mass at all
            Assert.AreEqual(1, lib2Keys.Count(k => Equals(NistLibraryBase.DUMMY_GC_ESI_MASS, k.Target?.Molecule?.MonoisotopicMass.Value ?? 0)));

            // Check case insensitive regex use in  https://minedatabase.mcs.anl.gov/#/download
            Assert.AreEqual(1, lib2Keys.Count(k => Equals("C28H38O6", k.SmallMoleculeLibraryAttributes.ChemicalFormula)));

            // Verify that we ignore zero intensity peaks on import
            SpectrumHeaderInfo info;
            var key = lib2Keys.First(k => Equals(Molecule.Parse("C27H42FeN9NaO12"), Molecule.Parse(k.SmallMoleculeLibraryAttributes.ChemicalFormula)));
            Assert.IsTrue(lib2.TryGetLibInfo(key, out info));
            SpectrumPeaksInfo peaksInfo;
            Assert.IsTrue(lib2.TryLoadSpectrum(key, out peaksInfo));
            Assert.IsTrue(peaksInfo.Peaks.Length == 4); // Declared length is 6 but two are zero intensity

            // Verify that we handled the mzVault variant
            key = lib2Keys.First(k => Equals("RHAXSHUQNIEUEY-UHFFFAOYAC", k.SmallMoleculeLibraryAttributes.InChiKey));
            Assert.IsTrue(lib2.TryLoadSpectrum(key, out peaksInfo));
            AssertEx.AreEqual(67, peaksInfo.Peaks.Length);

            // Verify that we handled the RTINSECONDS variant
            key = lib2Keys.First(k => Equals("IDAGLSESYTC[+57.0]YLLSKGK", k.Sequence));
            Assert.IsTrue(lib2.TryLoadSpectrum(key, out peaksInfo));
            Assert.IsTrue(lib2.TryGetLibInfo(key, out info));
            AssertEx.AreEqual(61.389, ((NistSpectrumHeaderInfo)info).RT, .001);
            AssertEx.AreEqual(28, peaksInfo.Peaks.Length);

            // Check use of ion mobility
            var ccsDict = new Dictionary<string, double?>()
            {
                {"Withanone; PlaSMA ID-2558", 220.9656493},
                {"ACar 4:0", 23.145},
                {"C6:OH b", 123.45},
                {"Glucolesquerellin", 189.9914404},
                {"PRZ_M632a", null} // CCS given as "-1"
            };
            foreach (var kvp in ccsDict)
            {
                var libEntry = lib2Keys.First(l => l.Target.DisplayName.Equals(kvp.Key));
                AssertEx.AreEqual(kvp.Value.HasValue, lib2.TryGetIonMobilityInfos(new LibKey(libEntry.LibraryKey), null, out var ionMobilities));
                AssertEx.AreEqual(kvp.Value ?? -1,ionMobilities?.FirstOrDefault()?.CollisionalCrossSectionSqA??-1);
            }

            // Verify that we handled the n and c terminal mods
            key = lib2Keys.First(k => Equals("A[43]FVFPKESDTSY[17]", k.Sequence));
            Assert.IsTrue(lib2.TryLoadSpectrum(key, out peaksInfo));

        }

        [TestMethod]
        public void GMDLoadLibrary()
        {
            var streamManager = new MemoryStreamManager();
            streamManager.TextFiles.Add(PATH_GMD_LIB, TEXT_LIB_GMD);
            var loader = new TestLibraryLoader {StreamManager = streamManager};

            var librarySpec = new NistLibSpec("Example GMD", PATH_GMD_LIB);

            Library lib = librarySpec.LoadLibrary(loader);

            var key = new LibKey(SmallMoleculeLibraryAttributes.Create(
                "M000880_A098001 - 101 - xxx_NA_0_FALSE_MDN35_ALK_Glycine, N, N - dimethyl - (1TMS)", "C7H17NO2Si",
                "FFDGPVCHZBVARC-UHFFFAOYSA-N",
                new Dictionary<string, string>()
                {
                    { MoleculeAccessionNumbers.TagCAS, "1118-68-9"},
                    { MoleculeAccessionNumbers.TagKEGG, "C01026"},
                    { MoleculeAccessionNumbers.TagInChI, "1S/C4H9NO2/c1-5(2)3-4(6)7/h3H2,1-2H3,(H,6,7)"}
                }), Adduct.M_PLUS);

            Assert.IsTrue(lib.TryLoadSpectrum(key, out var peaksInfo));
            Assert.AreEqual(70.1, peaksInfo.MZs.FirstOrDefault(), 0.0001);
            Assert.AreEqual(81.2, peaksInfo.Intensities.FirstOrDefault(), 0.0001);

        }
        
        [TestMethod]
        public void HunterLoadLibrary()
        {
            var streamManager = new MemoryStreamManager();
            var loader = new TestLibraryLoader { StreamManager = streamManager };

            Library libNist = CreateHunterFile(streamManager, loader, TEXT_LIB_YEAST_NIST);

            Assert.AreEqual(1, streamManager.BinaryFiles.Count);

            var librarySpecHunter = new XHunterLibSpec("Yeast (X!)", PATH_HUNTER_LIB);

            Library libHunter = librarySpecHunter.LoadLibrary(loader);
            CheckLibrary(libHunter, 20);

            Assert.IsFalse(libNist.IsSameLibrary(libHunter));
        }

        public static Library CreateHunterFile(MemoryStreamManager streamManager, ILoadMonitor loader, string nistText)
        {
            return CreateHunterFile(streamManager, loader, nistText, false);
        }

        public static Library CreateHunterFile(MemoryStreamManager streamManager, ILoadMonitor loader,
            string nistText, bool lowIntensity)
        {
            return CreateLibraryFile(streamManager, loader, nistText,
                (sm, lib) => XHunterLibrary.Write(sm, PATH_HUNTER_LIB, lib, lowIntensity));
        }

        [TestMethod]
        public void BiblioSpecLoadLibrary()
        {
            var streamManager = new MemoryStreamManager();
            var loader = new TestLibraryLoader { StreamManager = streamManager };

            Library libNist = CreateBiblioFile(streamManager, loader, TEXT_LIB_YEAST_NIST);

            Assert.AreEqual(1, streamManager.BinaryFiles.Count);

            var librarySpecBiblio = new BiblioSpecLibSpec("Yeast (BS)", PATH_BIBLIOSPEC_LIB);

            Library libBiblioSpec = librarySpecBiblio.LoadLibrary(loader);
            CheckLibrary(libBiblioSpec, 100);

            Assert.IsFalse(libNist.IsSameLibrary(libBiblioSpec));
        }

        // Unit test of annotations handler class
        private static void TestSpectrumPeakAnnotations()
        {
            var noNote = SpectrumPeakAnnotation.Create(new CustomIon(null, Adduct.FromChargeNoMass(-2), 100, 101, "noNote"), null);
            var noName = SpectrumPeakAnnotation.Create(new CustomIon(null, Adduct.FromChargeNoMass(-2), 100, 101, null), "noted");
            var fullIon = new CustomIon("C12H5O3", Adduct.FromStringAssumeChargeOnly("M-H2O+"), null, null, "full");
            var full = SpectrumPeakAnnotation.Create(fullIon, "noted");
            var fullToo = SpectrumPeakAnnotation.Create(fullIon, "noted");
            var nameOnlyTab = SpectrumPeakAnnotation.Create(new CustomIon(null, Adduct.FromChargeNoMass(-2), 100, 101,"test\ttabs"), "noted"); // Check tab escape
            var fullIonTabs = new CustomIon("C12H5", Adduct.FromChargeNoMass(-2), null, null, "full\ttabs");
            var tabbedFull = SpectrumPeakAnnotation.Create(fullIonTabs, "noted\ttabs"); // Check tab escape
            Assume.AreEqual(full, fullToo);
            Assume.AreNotEqual(full, tabbedFull);
            var tests = new List<List<SpectrumPeakAnnotation>>
            {
                new List<SpectrumPeakAnnotation> { noNote, noName, full},
                new List<SpectrumPeakAnnotation> { fullToo, nameOnlyTab, tabbedFull}
            };
            var cached = SpectrumPeakAnnotation.ToCacheFormat(tests);
            var roundtrip = SpectrumPeakAnnotation.FromCacheFormat(cached);
            int i = 0;
            foreach (var annotsPerPeak in tests)
            {
                Assume.IsTrue(CollectionUtil.EqualsDeep(annotsPerPeak, roundtrip[i++]));
            }
        }

        public static Library CreateBiblioFile(MemoryStreamManager streamManager, ILoadMonitor loader, string nistText)
        {
            return CreateLibraryFile(streamManager, loader, nistText,
                (sm, lib) => BiblioSpecLibrary.Write(sm, PATH_BIBLIOSPEC_LIB, lib));
        }

        private static Library CreateLibraryFile(MemoryStreamManager streamManager, ILoadMonitor loader,
            string nistText, Action<IStreamManager, Library> write)
        {
            streamManager.TextFiles[PATH_NIST_LIB] = nistText;

            var librarySpec = new NistLibSpec("Temporary (NIST)", PATH_NIST_LIB);

            Library libNist = librarySpec.LoadLibrary(loader);
            Assert.IsNotNull(libNist);
            write(streamManager, libNist);

            streamManager.TextFiles.Remove(PATH_NIST_LIB);
            streamManager.Delete(PATH_NIST_LIB_CACHE);

            return libNist;
        }

        private static void CheckLibrary(Library lib, int minPeaks, LibKey[] keys = null, Dictionary<int, List<SpectrumPeakAnnotation>> fragmentAnnotations = null)
        {
            Assert.IsNotNull(lib);
            Assert.IsTrue(lib.IsLoaded);
            foreach (var key in keys ?? KEYS_LIB_YEAST_NIST)
            {
                SpectrumHeaderInfo info;
                Assert.IsTrue(lib.TryGetLibInfo(key, out info));
                SpectrumPeaksInfo peaksInfo;
                Assert.IsTrue(lib.TryLoadSpectrum(key, out peaksInfo));
                Assert.IsTrue(peaksInfo.Peaks.Length >= minPeaks);
                var infoNist = info as NistSpectrumHeaderInfo;
                if (infoNist != null)
                {
                    Assert.AreEqual(80.51, infoNist.RT.Value);
                }

                if (fragmentAnnotations != null)
                {
                    for (var p = 0; p < peaksInfo.Peaks.Length; p++)
                    {
                        if (!fragmentAnnotations.TryGetValue(p, out var annotations))
                        {
                            Assume.IsTrue(peaksInfo.Peaks[p].Annotations == null);
                        }
                        else
                        {
                            // Examine as strings to avoid issues with rounding in I/O
                            Assume.IsTrue(CollectionUtil.EqualsDeep(
                                annotations.Select(a => a.ToString()).ToList(),
                                peaksInfo.Peaks[p].Annotations.Select(a => a.ToString()).ToList()), "did not find expected annotation in converted library");
                        }
                    }
                }
            }            
        }

        public class TestLibraryLoader : ILoadMonitor
        {
            public IStreamManager StreamManager { get; set; }

            public bool IsCanceled
            {
                get { return false; }
            }

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                return UpdateProgressResponse.normal;
            }

            public bool HasUI { get { return false; } }
        }

        public static readonly LibKey[] KEYS_LIB_YEAST_NIST =
        {
            new LibKey("QQGPLEPTVGNSTAITEER", Adduct.DOUBLY_PROTONATED),
            new LibKey("IPSAIHLQISNK", Adduct.DOUBLY_PROTONATED),
            new LibKey("NSTDIPLQDATETYR", Adduct.DOUBLY_PROTONATED),
            new LibKey("VLHNFLTTPSK", Adduct.DOUBLY_PROTONATED),
        };

        public const string TEXT_LIB_YEAST_NIST =
            TEXT_LIB_YEAST_NIST1 +
            "\n" +
            TEXT_LIB_YEAST_NIST2 +
            "\n" +
            TEXT_LIB_YEAST_NIST3 +
            "\n" +
            TEXT_LIB_YEAST_NIST4;

        public const string TEXT_LIB_YEAST_NIST1 =
            "NaMe: QQGPLEPTVGNSTAITEER/2\n" + // This particular strange capitalization not actually seen in the wild, but be ready
            "MW: 2028.012\n" +
            "Comment: Spec=Consensus Pep=Tryptic Fullname=K.QQGPLEPTVGNSTAITEER.R/2 Mods=0 Parent=1014.006 Inst=it Mz_diff=0.264 Mz_exact=1014.0062 Mz_av=1014.596 Protein=\"gi|6319311|ref|NP_009394.1| Fun14p [Saccharomyces cerevisiae]; gi|349745|gb|AAC04950.1| Fun14p [Saccharomyces cerevisiae]; gi|45269305|gb|AAS56033.1| YAL008W [Saccharomyces cerevisiae]; gi|629999|pir||S43447 FUN14 protein - yeast (Saccharomyces cerevisiae); gi|731265|sp|P18411|YAA8_YEAST Hypothetical 22.1 kDa protein in SPO7-ERP2 intergenic region\" Pseq=1 Organism=\"yeast\" Se=5^M1:sc=51.65/0,td=49.4/0,sr=24.94/0,sd=49.4/0,bs=24.94,bd=49.4^X6:ex=2.9e-011/8e-006,td=1.77e+011/1.324e+011,sd=0/0,hs=57.85/5.033,bs=4.9e-012,b2=5.9e-012,bd=1.61e+011^O5:ex=2.69e-006/1.486e-005,td=1.86e+007/1.956e+008,pr=2.89e-010/1.772e-009,bs=9.33e-008,b2=1.11e-007,bd=5.36e+008^S1:sc=3.16/0,pb=0.9994/0,dc=0.45/0,ps=454.9/0,pr=1/0,bs=0.9994,bd=0.45^P4:sc=27.4/3,dc=18.9/2.5,ps=3.43/0.275,bs=0 Sample=8/cbs00174_001_01_cam,1,1/cbs00174_001_02_cam,1,1/cbs00174_01_06_cam,1,1/cbs00174_01_07_cam,0,1/cbs00174_01_10_cam,1,1/cbs00175_02_05_cam,0,1/cbs00175_02_06_cam,1,1/yeast_comp12vs12standscx_cam,1,1 Nreps=6/9 Missing=0.0630/0.0510 Parent_med=1014.21/0.34 Max2med_orig=629.5/170.9 Dotfull=0.861/0.032 Dot_cons=0.936/0.042 Unassign_all=0.086 Unassigned=0.017 Dotbest=0.94 Flags=0,0,2 Naa=19 DUScorr=10/2/2.9 Dottheory=0.93 Pfin=6.1e+013 Probcorr=20 Tfratio=1.7e+010 Pfract=0.17 RetentionTime=4830.6\n" +
            "Num peaks: 178\n" +
            "304.2\t205\t\"y2/0.04,b6-46^2/0.04 6/6 0.5\"\n" +
            "314.2\t39\t\"b3/0.06 4/4 0.1\"\n" +
            "334.1\t50\t\"? 2/2 1.0\"\n" +
            "340.2\t298\t\"? 6/6 0.5\"\n" +
            "348.3\t54\t\"? 3/5 0.4\"\n" +
            "351.0\t78\t\"y6-17^2/-0.19,y6-18^2/0.31 3/5 0.4\"\n" +
            "365.4\t43\t\"? 3/5 0.1\"\n" +
            "366.2\t55\t\"b7-18^2/-0.49,b4-45/0.01 3/5 0.2\"\n" +
            "378.1\t46\t\"? 3/5 0.1\"\n" +
            "379.2\t52\t\"?i 4/5 0.0\"\n" +
            "380.2\t64\t\"?i 3/5 1.3\"\n" +
            "393.9\t126\t\"b4-17/-0.29 6/6 1.2\"\n" +
            "397.2\t307\t\"? 6/6 1.1\"\n" +
            "398.2\t91\t\"?i 5/5 2.0\"\n" +
            "415.3\t38\t\"y3-18/0.10 3/4 0.1\"\n" +
            "433.4\t196\t\"y3/0.20 6/6 0.7\"\n" +
            "434.3\t41\t\"y3i/1.10 4/5 0.2\"\n" +
            "461.3\t210\t\"? 6/6 0.3\"\n" +
            "462.4\t176\t\"?i 6/6 0.4\"\n" +
            "469.3\t56\t\"? 5/5 0.2\"\n" +
            "472.4\t87\t\"? 3/5 0.5\"\n" +
            "479.3\t775\t\"b5-45*/0.02 6/6 2.7\"\n" +
            "480.2\t156\t\"b5-45i/0.92 6/6 2.0\"\n" +
            "489.4\t76\t\"? 5/5 0.3\"\n" +
            "490.2\t114\t\"y4-44/-0.05 5/6 0.6\"\n" +
            "505.2\t61\t\"? 4/6 1.4\"\n" +
            "506.4\t335\t\"b5-18/0.12 6/6 2.5\"\n" +
            "507.3\t824\t\"b5-17/0.02 5/6 1.9\"\n" +
            "508.3\t188\t\"b5-17i/1.02 6/6 1.0\"\n" +
            "521.4\t113\t\"? 3/2 4.0\"\n" +
            "524.4\t74\t\"b5/0.12 5/5 0.2\"\n" +
            "525.2\t81\t\"b5i/0.92 5/6 2.5\"\n" +
            "534.3\t1280\t\"y4/0.05 6/6 2.2\"\n" +
            "535.3\t196\t\"y4i/1.05 6/6 1.0\"\n" +
            "556.5\t43\t\"? 4/5 0.1\"\n" +
            "590.3\t68\t\"? 4/5 0.2\"\n" +
            "591.0\t88\t\"?i 4/6 1.2\"\n" +
            "601.4\t47\t\"y5-46/0.06 3/5 0.2\"\n" +
            "607.4\t79\t\"b6-46/0.08 5/5 1.1\"\n" +
            "608.3\t65\t\"b6-45/-0.02 6/5 0.3\"\n" +
            "618.3\t296\t\"? 6/6 1.8\"\n" +
            "619.4\t133\t\"?i 6/6 0.7\"\n" +
            "635.3\t1324\t\"b6-18/-0.02 6/6 2.3\"\n" +
            "636.3\t1557\t\"b6-17/-0.02 6/6 8.9\"\n" +
            "637.2\t262\t\"b6-17i/0.88 6/6 1.3\"\n" +
            "647.4\t603\t\"y5/0.06 6/6 1.1\"\n" +
            "648.3\t106\t\"y5i/0.96 5/6 0.4\"\n" +
            "653.3\t431\t\"b6/-0.02 6/6 0.3\"\n" +
            "654.3\t106\t\"b6i/0.98 6/6 0.5\"\n" +
            "676.5\t79\t\"? 3/5 1.9\"\n" +
            "679.2\t51\t\"y13-17^2/-0.15,y13-18^2/0.35 4/5 0.1\"\n" +
            "688.0\t507\t\"y13^2/0.15 6/6 6.4\"\n" +
            "688.5\t179\t\"? 4/6 4.2\"\n" +
            "694.4\t58\t\"? 4/5 0.3\"\n" +
            "710.4\t76\t\"? 6/6 0.6\"\n" +
            "718.4\t484\t\"y6/0.03 5/6 0.6\"\n" +
            "719.5\t111\t\"y6i/1.13 5/6 0.5\"\n" +
            "733.5\t63\t\"b7-17/0.13 3/5 0.2\"\n" +
            "752.3\t86\t\"y14^2/-0.07 4/5 0.5\"\n" +
            "798.2\t51\t\"b16^2/0.30 4/5 0.9\"\n" +
            "816.2\t76\t\"? 5/5 0.5\"\n" +
            "819.5\t343\t\"y7/0.08 6/6 0.8\"\n" +
            "820.5\t83\t\"y7i/1.08 6/6 0.4\"\n" +
            "823.3\t66\t\"a8/-0.12 5/5 0.5\"\n" +
            "824.4\t50\t\"a8i/0.98 4/5 0.1\"\n" +
            "833.4\t69\t\"b8-18/-0.02 3/5 0.2\"\n" +
            "839.3\t48\t\"b17-46^2/-0.12 3/5 0.2\"\n" +
            "841.4\t113\t\"? 6/6 1.7\"\n" +
            "848.8\t94\t\"y16-17^2/-0.14,y16-18^2/0.36 4/5 0.9\"\n" +
            "849.6\t37\t\"y16-17i^2/0.66 3/4 0.0\"\n" +
            "851.4\t42\t\"b8/-0.02 3/5 0.1\"\n" +
            "857.7\t399\t\"y16^2/0.26 6/6 2.2\"\n" +
            "877.4\t197\t\"y17-17^2/-0.05,y17-18^2/0.45 4/6 0.3\"\n" +
            "878.0\t70\t\"y17-17i^2/0.55 3/5 0.3\"\n" +
            "886.1\t1723\t\"y17^2/0.15 6/6 4.8\"\n" +
            "887.1\t62\t\"y17i^2/1.15 3/5 0.3\"\n" +
            "888.4\t75\t\"y8-18/-0.05 6/5 0.2\"\n" +
            "889.3\t42\t\"y8-17/-0.15 4/5 0.1\"\n" +
            "896.0\t37\t\"? 3/4 0.1\"\n" +
            "897.4\t79\t\"? 5/5 0.2\"\n" +
            "898.5\t65\t\"?i 5/5 0.2\"\n" +
            "899.6\t36\t\"?i 3/4 0.2\"\n" +
            "905.5\t59\t\"b9-45/0.01 3/5 0.4\"\n" +
            "906.4\t516\t\"y8/-0.05,b9-44/-0.09 6/6 1.9\"\n" +
            "907.5\t193\t\"y8i/1.05 5/6 1.0\"\n" +
            "909.4\t40\t\"? 2/2 0.2\"\n" +
            "915.3\t628\t\"? 5/6 0.9\"\n" +
            "916.3\t196\t\"?i 5/6 0.6\"\n" +
            "922.5\t89\t\"? 3/3 3.5\"\n" +
            "932.3\t253\t\"b9-18/-0.19 6/6 1.6\"\n" +
            "933.5\t357\t\"b9-17/0.01 6/6 2.6\"\n" +
            "934.4\t82\t\"b9-17i/0.91 6/5 0.7\"\n" +
            "941.3\t73\t\"y18-17^2/-0.18,y18-18^2/0.32 4/6 0.5\"\n" +
            "947.9\t42\t\"? 3/2 0.1\"\n" +
            "950.5\t490\t\"b9/0.01 6/6 1.1\"\n" +
            "951.5\t166\t\"b9i/1.01 6/6 1.8\"\n" +
            "958.9\t50\t\"? 2/2 0.5\"\n" +
            "970.8\t60\t\"? 3/5 0.2\"\n" +
            "972.6\t61\t\"? 4/5 0.2\"\n" +
            "974.8\t67\t\"y9-46*/0.30 5/5 0.5\"\n" +
            "978.7\t74\t\"? 4/6 0.3\"\n" +
            "979.2\t107\t\"? 4/5 0.5\"\n" +
            "983.2\t239\t\"? 5/6 1.0\"\n" +
            "986.3\t54\t\"? 3/2 0.5\"\n" +
            "987.6\t254\t\"?i 5/6 1.0\"\n" +
            "988.6\t62\t\"?i 3/5 0.3\"\n" +
            "989.5\t68\t\"b10-18/-0.01 4/5 0.2\"\n" +
            "990.1\t72\t\"b10-17/-0.41,b10-18/0.59 3/5 0.8\"\n" +
            "990.7\t85\t\"p-46/-0.31,b10-17/0.19 3/5 0.4\"\n" +
            "991.7\t62\t\"p-45/0.19,p-44/-0.31 5/5 0.2\"\n" +
            "994.5\t195\t\"? 4/6 1.1\"\n" +
            "996.3\t2849\t\"p-35/-0.20,p-36/0.30 6/6 6.7\"\n" +
            "997.0\t1105\t\"p-35i/0.50 5/6 3.2\"\n" +
            "999.3\t53\t\"? 3/5 0.3\"\n" +
            "1003.5\t51\t\"y9-17/0.00 6/5 0.2\"\n" +
            "1005.2\t950\t\"p-18/0.20,p-17/-0.31 6/6 16.1\"\n" +
            "1005.6\t257\t\"p-17/0.09 4/6 2.9\"\n" +
            "1042.1\t57\t\"? 3/5 0.3\"\n" +
            "1059.5\t116\t\"y10-18/-0.02 6/6 0.3\"\n" +
            "1060.5\t133\t\"y10-17/-0.02 5/6 0.4\"\n" +
            "1061.5\t57\t\"y10-17i/0.98 6/5 0.4\"\n" +
            "1071.4\t48\t\"? 3/5 0.1\"\n" +
            "1077.5\t3561\t\"y10/-0.02,b11-44/-0.05 6/6 4.5\"\n" +
            "1078.5\t1357\t\"y10i/0.98 6/6 6.7\"\n" +
            "1103.6\t47\t\"b11-18/0.05 4/4 0.2\"\n" +
            "1104.5\t63\t\"b11-17/-0.05 4/5 0.1\"\n" +
            "1121.6\t58\t\"b11/0.05 5/5 0.1\"\n" +
            "1176.6\t484\t\"y11/0.01 5/6 0.5\"\n" +
            "1177.6\t193\t\"y11i/1.01 5/6 0.6\"\n" +
            "1190.6\t54\t\"b12-18/0.02 4/5 0.2\"\n" +
            "1191.5\t85\t\"b12-17/-0.08 5/5 0.3\"\n" +
            "1192.6\t53\t\"b12-17i/1.02 4/5 0.2\"\n" +
            "1200.5\t108\t\"? 3/5 2.9\"\n" +
            "1208.6\t94\t\"b12/0.02 4/6 2.0\"\n" +
            "1237.6\t42\t\"? 3/5 0.1\"\n" +
            "1277.6\t172\t\"y12/-0.03 6/6 1.3\"\n" +
            "1278.7\t64\t\"y12i/1.07 3/5 0.2\"\n" +
            "1291.8\t63\t\"b13-18/0.17 3/5 0.4\"\n" +
            "1310.4\t111\t\"? 4/6 4.3\"\n" +
            "1338.8\t48\t\"? 5/5 0.1\"\n" +
            "1339.8\t62\t\"?i 5/5 0.3\"\n" +
            "1345.6\t99\t\"? 6/5 0.4\"\n" +
            "1346.4\t58\t\"?i 4/5 0.2\"\n" +
            "1356.6\t404\t\"y13-18/-0.09 6/6 2.5\"\n" +
            "1357.6\t385\t\"y13-17/-0.09 6/6 0.8\"\n" +
            "1358.7\t128\t\"y13-17i/1.01 5/6 0.8\"\n" +
            "1362.6\t96\t\"b14-18/-0.07 5/6 0.3\"\n" +
            "1363.6\t48\t\"b14-17/-0.07 4/5 0.2\"\n" +
            "1374.6\t10000\t\"y13/-0.09 6/6 0.0\"\n" +
            "1375.6\t5241\t\"y13i/0.91 6/6 16.5\"\n" +
            "1376.5\t799\t\"y13i/1.81 5/6 13.1\"\n" +
            "1380.6\t87\t\"b14/-0.07 4/6 0.3\"\n" +
            "1381.6\t74\t\"b14i/0.93 4/5 1.3\"\n" +
            "1458.5\t76\t\"y14-45/-0.23 5/5 0.2\"\n" +
            "1459.7\t80\t\"y14-44/-0.03 3/5 0.3\"\n" +
            "1475.7\t155\t\"b15-18/-0.05 4/6 0.2\"\n" +
            "1476.7\t276\t\"b15-17/-0.05 6/6 0.7\"\n" +
            "1477.5\t124\t\"b15-17i/0.75 6/6 0.5\"\n" +
            "1485.6\t151\t\"y14-18/-0.13 6/6 0.4\"\n" +
            "1486.8\t79\t\"y14-17/0.07 6/6 0.4\"\n" +
            "1493.8\t274\t\"b15/0.05 6/6 1.0\"\n" +
            "1494.6\t121\t\"b15i/0.85 5/6 0.2\"\n" +
            "1503.6\t731\t\"y14/-0.13 5/6 0.7\"\n" +
            "1504.8\t478\t\"y14i/1.07 6/6 2.4\"\n" +
            "1576.8\t106\t\"b16-18/0.00 3/5 2.8\"\n" +
            "1595.0\t44\t\"b16/0.20 3/4 0.2\"\n" +
            "1705.8\t260\t\"b17-18/-0.04 5/6 0.5\"\n" +
            "1706.6\t180\t\"b17-17/-0.24 6/6 1.2\"\n" +
            "1713.5\t183\t\"y16/-0.37 6/6 1.2\"\n" +
            "1714.9\t156\t\"? 6/6 1.2\"\n" +
            "1723.7\t78\t\"b17/-0.14 4/5 0.2\"\n" +
            "1724.9\t51\t\"y17-46/0.01 4/5 0.2\"\n" +
            "1770.8\t382\t\"y17/-0.09 6/6 0.6\"\n" +
            "1771.9\t323\t\"y17i/1.01 6/6 1.2\"\n" +
            "1834.8\t148\t\"b18-18/-0.09 5/6 0.6\"\n" +
            "1835.7\t153\t\"b18-17/-0.19 5/6 0.6\"\n" +
            "1852.8\t77\t\"b18/-0.09,y18-46/-0.15 5/5 0.2\"\n" +
            "1854.0\t81\t\"y18-45*/0.05 4/5 0.5\"\n";

        public const string TEXT_LIB_YEAST_NIST2 =
            "Name: IPSAIHLQISNK/2\n" +
            "MW: 1321.772\n" +
            "Comment: Spec=Consensus Pep=Tryptic Fullname=R.IPSAIHLQISNK.S/2 Mods=0 Parent=660.886 Inst=it Mz_diff=0.114 Mz_exact=660.8859 Mz_av=661.285 Protein=\"gi|171865|gb|AAC04947.1| Mdm10p: Mitochondria outer membrane protein [Saccharomyces cerevisiae]\" Pseq=2 Organism=\"yeast\" Se=3^X2:ex=0.14/0.14,td=1e+009/1e+009,sd=0/0,hs=37.2/18,bs=4.9e-010,b2=0.28,bd=2e+009^O1:ex=2.29e-006/0,td=2.19e+007/0,pr=2.89e-010/0,bs=2.29e-006,bd=2.19e+007^P1:sc=28.8/0,dc=21.2/0,ps=3.68/0,bs=0 Sample=4/cbs00174_001_01_cam,0,1/cbs00174_01_07_cam,1,1/cbs00175_02_01_cam,1,1/yeast_ffey1_none,0,1 Nreps=2/4 Missing=0.1592/0.0033 Parent_med=661.00/0.23 Max2med_orig=506.6/251.6 Dotfull=0.617/0.000 Dot_cons=0.857/0.002 Unassign_all=0.336 Unassigned=0.422 Dotbest=0.86 Flags=0,0,0 Naa=12 DUScorr=5.3/0.17/2.9 Dottheory=0.92 Pfin=1.9e+013 Probcorr=1 Tfratio=1e+009 Pfract=0 RetentionTime=4830.6\n" +
            "Num peaks: 106\n" +
            "280.3\t213\t\"b3-18*/0.13 2/2 2.8\"\n" +
            "295.1\t270\t\"y5^2/-0.07 2/2 1.7\"\n" +
            "296.3\t61\t\"y5i^2/1.13 2/1 0.4\"\n" +
            "316.3\t61\t\"? 2/1 0.4\"\n" +
            "327.2\t53\t\"? 2/1 0.4\"\n" +
            "331.3\t106\t\"y3-17/0.11 2/2 1.4\"\n" +
            "341.2\t317\t\"a4/-0.01 2/2 4.2\"\n" +
            "348.3\t507\t\"y3/0.11 2/2 7.8\"\n" +
            "357.4\t638\t\"b7-18^2/-0.32 2/2 7.7\"\n" +
            "358.4\t188\t\"b7-18i^2/0.68 2/2 2.6\"\n" +
            "365.3\t307\t\"? 2/2 2.5\"\n" +
            "369.2\t390\t\"b4/-0.01 2/2 5.7\"\n" +
            "370.7\t106\t\"? 2/1 1.1\"\n" +
            "420.7\t97\t\"y7^2/0.46 2/1 1.1\"\n" +
            "439.8\t64\t\"? 2/1 0.4\"\n" +
            "443.4\t166\t\"y4-18/0.13 2/2 0.5\"\n" +
            "444.3\t73\t\"y4-17/0.03 2/2 0.7\"\n" +
            "454.4\t309\t\"a5/0.11 2/2 4.9\"\n" +
            "455.6\t107\t\"a5i/1.31 2/1 0.9\"\n" +
            "461.4\t509\t\"y4/0.13 2/2 6.9\"\n" +
            "462.4\t91\t\"y4i/1.13 2/2 1.0\"\n" +
            "464.4\t122\t\"b5-18/0.11,b9-45^2/-0.39 2/2 1.4\"\n" +
            "470.5\t149\t\"? 2/2 2.1\"\n" +
            "474.4\t72\t\"? 2/1 0.4\"\n" +
            "476.9\t144\t\"y8^2/0.12 2/2 1.7\"\n" +
            "479.4\t61\t\"? 2/1 0.3\"\n" +
            "482.4\t285\t\"b5/0.11 2/2 4.9\"\n" +
            "483.4\t100\t\"b5i/1.11 2/2 1.3\"\n" +
            "486.4\t94\t\"? 2/2 0.4\"\n" +
            "492.4\t88\t\"? 2/2 0.1\"\n" +
            "506.3\t221\t\"? 2/2 3.8\"\n" +
            "512.5\t404\t\"y9^2/0.20 2/2 4.4\"\n" +
            "522.5\t87\t\"b10-17^2/0.20 2/2 0.9\"\n" +
            "546.8\t808\t\"y10-18^2/-0.02 2/2 10.3\"\n" +
            "553.3\t84\t\"? 2/2 0.6\"\n" +
            "556.0\t1714\t\"y10^2/0.18 2/2 29.4\"\n" +
            "556.5\t125\t\"? 2/2 0.8\"\n" +
            "561.1\t175\t\"? 2/2 1.2\"\n" +
            "569.4\t55\t\"b11-36^2/-0.43 2/1 0.3\"\n" +
            "571.6\t148\t\"y5-18/0.27 2/2 1.5\"\n" +
            "572.5\t198\t\"y5-17/0.17 2/2 2.7\"\n" +
            "573.5\t70\t\"y5-17i/1.17 2/1 0.7\"\n" +
            "578.7\t123\t\"b11-18^2/-0.13 2/1 1.4\"\n" +
            "586.6\t105\t\"? 2/2 1.4\"\n" +
            "589.4\t672\t\"y5/0.07 2/2 6.9\"\n" +
            "590.5\t125\t\"y5i/1.17 2/2 1.1\"\n" +
            "591.5\t79\t\"a6/0.15 2/2 0.3\"\n" +
            "595.6\t606\t\"y11-17^2/-0.24,y11-18^2/0.26 2/2 9.7\"\n" +
            "596.4\t276\t\"y11-17i^2/0.56 2/1 4.2\"\n" +
            "598.6\t75\t\"? 2/2 0.8\"\n" +
            "601.4\t169\t\"b6-18/0.05 2/2 2.3\"\n" +
            "604.6\t5871\t\"y11^2/0.26 2/2 88.9\"\n" +
            "607.6\t3774\t\"? 2/2 42.8\"\n" +
            "615.6\t94\t\"? 2/1 0.2\"\n" +
            "617.5\t917\t\"? 2/2 12.5\"\n" +
            "618.5\t201\t\"?i 2/2 1.6\"\n" +
            "619.7\t973\t\"b6/0.35 2/2 8.1\"\n" +
            "620.6\t389\t\"b6i/1.25 2/2 1.1\"\n" +
            "621.7\t81\t\"b6i/2.35 2/1 0.3\"\n" +
            "625.0\t1876\t\"? 2/2 12.6\"\n" +
            "625.8\t128\t\"?i 2/2 0.1\"\n" +
            "628.7\t10000\t\"? 2/2 69.4\"\n" +
            "629.5\t1022\t\"?i 2/2 5.1\"\n" +
            "630.7\t127\t\"?i 2/2 0.0\"\n" +
            "632.2\t98\t\"? 2/2 0.2\"\n" +
            "634.7\t245\t\"? 2/2 2.8\"\n" +
            "638.6\t873\t\"p-45/0.21,p-44/-0.29 2/2 6.2\"\n" +
            "639.6\t229\t\"p-45i/1.21 2/2 0.6\"\n" +
            "642.8\t813\t\"p-36/-0.08 2/2 9.7\"\n" +
            "643.5\t1294\t\"p-35/0.12 2/2 19.2\"\n" +
            "644.6\t261\t\"p-35i/1.22 2/2 4.2\"\n" +
            "645.9\t237\t\"p-35i/2.52 2/2 1.6\"\n" +
            "648.6\t73\t\"? 2/1 0.6\"\n" +
            "651.4\t3379\t\"p-18/-0.48 2/2 47.0\"\n" +
            "652.5\t2566\t\"p-17/0.11 2/2 20.4\"\n" +
            "683.3\t55\t\"? 2/1 0.4\"\n" +
            "702.5\t942\t\"y6/0.08 2/2 14.8\"\n" +
            "703.5\t240\t\"y6i/1.08 2/2 3.4\"\n" +
            "730.3\t209\t\"? 2/2 0.7\"\n" +
            "732.4\t492\t\"b7/-0.03 2/2 7.5\"\n" +
            "822.5\t68\t\"y7-17/0.03 2/1 0.4\"\n" +
            "826.1\t92\t\"? 2/1 0.6\"\n" +
            "830.4\t100\t\"? 2/2 0.1\"\n" +
            "839.5\t2091\t\"y7/0.03 2/2 32.8\"\n" +
            "840.5\t608\t\"y7i/1.03 2/2 9.5\"\n" +
            "860.5\t599\t\"b8/0.01 2/2 8.9\"\n" +
            "861.5\t309\t\"b8i/1.01 2/2 0.1\"\n" +
            "875.8\t206\t\"? 2/2 0.0\"\n" +
            "928.7\t77\t\"b9-45/0.12 2/1 0.4\"\n" +
            "947.5\t100\t\"? 2/2 0.0\"\n" +
            "952.5\t508\t\"y8/-0.06 2/2 6.3\"\n" +
            "953.6\t236\t\"y8i/1.04 2/2 3.7\"\n" +
            "973.5\t869\t\"b9/-0.08 2/2 13.6\"\n" +
            "974.7\t436\t\"b9i/1.12 2/2 3.2\"\n" +
            "987.4\t73\t\"? 2/1 0.6\"\n" +
            "1023.7\t442\t\"y9/0.10 2/2 7.2\"\n" +
            "1024.6\t160\t\"y9i/1.00 2/2 2.4\"\n" +
            "1043.0\t125\t\"b10-18/0.39 2/1 1.1\"\n" +
            "1043.6\t216\t\"b10-17/-0.01 2/2 0.1\"\n" +
            "1044.5\t95\t\"b10-17i/0.89 2/2 0.3\"\n" +
            "1060.6\t326\t\"b10/-0.01 2/2 5.0\"\n" +
            "1061.4\t158\t\"b10i/0.79 2/2 2.3\"\n" +
            "1110.6\t563\t\"y10/-0.03 2/2 7.8\"\n" +
            "1111.7\t389\t\"y10i/1.07 2/2 6.5\"\n" +
            "1174.8\t301\t\"b11/0.15 2/2 4.2\"\n" +
            "1175.7\t192\t\"b11i/1.05 2/2 3.2\"\n";

        public const string TEXT_LIB_YEAST_NIST3 =
            "Name: NSTDIPLQDATETYR/2\n" +
            "MW: 1724.827\n" +
            "Comment: Spec=Consensus Pep=Tryptic Fullname=R.NSTDIPLQDATETYR.Q/2 Mods=0 Parent=862.413 Inst=it Mz_diff=0.307 Mz_exact=862.4134 Mz_av=862.915 Protein=\"gi|171865|gb|AAC04947.1| Mdm10p: Mitochondria outer membrane protein [Saccharomyces cerevisiae]\" Pseq=2 Organism=\"yeast\" Se=3^X4:ex=3.95e-007/8.997e-006,td=2.05e+007/1.367e+007,sd=0/0,hs=45.25/3.475,bs=2e-008,b2=3.9e-007,bd=3.95e+007^O4:ex=2.195e-005/1.595e-005,td=2.29e+006/1.78e+007,pr=2.945e-009/2.577e-009,bs=6.97e-007,b2=2.05e-005,bd=7.17e+007^P4:sc=31.1/2.775,dc=21.45/2.2,ps=3.705/0.2425,bs=0 Sample=7/cbs00174_001_01_cam,0,1/cbs00174_01_06_cam,0,1/cbs00174_01_10_cam,1,1/cbs00175_02_01_cam,1,1/cbs00175_02_02_cam,0,1/cbs00175_02_05_cam,0,1/cbs00175_02_06_cam,2,2 Nreps=4/8 Missing=0.1111/0.0353 Parent_med=862.72/0.06 Max2med_orig=758.8/183.5 Dotfull=0.858/0.014 Dot_cons=0.913/0.024 Unassign_all=0.121 Unassigned=0.055 Dotbest=0.89 Flags=0,0,0 Naa=15 DUScorr=10/1.4/2.9 Dottheory=0.97 Pfin=9.3e+014 Probcorr=1 Tfratio=1.4e+010 Pfract=0 RetentionTime=4830.6\n" +
            "Num peaks: 150\n" +
            "268.2\t51\t\"? 4/4 0.2\"\n" +
            "285.2\t52\t\"y4^2/0.56,b3-18/0.08 4/4 0.2\"\n" +
            "286.2\t70\t\"b3-17/0.08 3/4 0.3\"\n" +
            "296.2\t25\t\"? 2/2 0.0\"\n" +
            "303.3\t27\t\"b3/0.18 2/2 0.1\"\n" +
            "304.2\t42\t\"b3i/1.08 4/4 0.1\"\n" +
            "325.1\t26\t\"? 2/2 0.1\"\n" +
            "338.2\t111\t\"y2/0.01 4/4 0.1\"\n" +
            "343.3\t54\t\"? 4/4 0.3\"\n" +
            "353.1\t47\t\"? 3/4 0.1\"\n" +
            "354.3\t45\t\"?i 4/4 0.1\"\n" +
            "363.3\t27\t\"? 2/2 0.1\"\n" +
            "364.2\t66\t\"?i 3/4 0.4\"\n" +
            "365.3\t48\t\"?i 3/4 0.3\"\n" +
            "382.1\t94\t\"? 4/4 0.2\"\n" +
            "383.2\t157\t\"?i 4/4 0.3\"\n" +
            "384.3\t48\t\"?i 3/4 0.3\"\n" +
            "389.4\t52\t\"? 4/4 0.4\"\n" +
            "399.4\t71\t\"? 3/4 0.1\"\n" +
            "400.3\t267\t\"b4-18/0.15 4/4 0.7\"\n" +
            "401.1\t138\t\"b4-17/-0.05 4/4 0.5\"\n" +
            "418.1\t459\t\"b4/-0.05 4/4 1.8\"\n" +
            "419.2\t79\t\"y7-18^2/0.00,y7-17^2/-0.50 4/4 0.3\"\n" +
            "433.2\t25\t\"? 2/2 0.1\"\n" +
            "434.3\t27\t\"?i 2/3 0.2\"\n" +
            "439.3\t529\t\"y3/0.06 4/4 1.7\"\n" +
            "440.4\t92\t\"y3i/1.16 4/4 0.4\"\n" +
            "441.2\t29\t\"y3i/1.96 2/3 0.1\"\n" +
            "450.2\t38\t\"? 4/4 0.1\"\n" +
            "454.4\t62\t\"? 4/4 0.4\"\n" +
            "460.2\t64\t\"? 4/4 0.2\"\n" +
            "467.4\t64\t\"? 4/4 0.3\"\n" +
            "468.4\t76\t\"?i 4/4 0.4\"\n" +
            "469.2\t72\t\"y8-46^2/-0.03,b9-46^2/-0.53 4/4 0.2\"\n" +
            "478.2\t139\t\"? 4/4 0.3\"\n" +
            "479.1\t57\t\"?i 4/4 0.3\"\n" +
            "485.3\t122\t\"b5-46/0.07 4/4 0.3\"\n" +
            "486.4\t231\t\"b5-45/0.17 4/4 0.3\"\n" +
            "487.1\t55\t\"b5-44/-0.13 4/4 0.6\"\n" +
            "495.2\t249\t\"? 4/4 0.3\"\n" +
            "496.2\t315\t\"?i 4/4 0.5\"\n" +
            "497.1\t61\t\"?i 4/4 0.3\"\n" +
            "503.2\t321\t\"a5/-0.03 4/4 0.3\"\n" +
            "504.3\t76\t\"a5i/1.07 4/4 0.3\"\n" +
            "513.2\t829\t\"b5-18/-0.03 4/4 1.2\"\n" +
            "514.3\t547\t\"b5-17/0.07 4/4 1.0\"\n" +
            "515.3\t71\t\"b5-17i/1.07 4/4 0.2\"\n" +
            "531.3\t1332\t\"b5/0.07 4/4 1.2\"\n" +
            "532.4\t175\t\"b5i/1.17 4/4 0.4\"\n" +
            "565.5\t27\t\"? 2/1 0.1\"\n" +
            "568.3\t304\t\"y4/0.02 4/4 0.3\"\n" +
            "569.4\t81\t\"b11-18^2/-0.37 4/4 0.4\"\n" +
            "588.5\t59\t\"y10-18^2/0.20,y10-17^2/-0.30 3/4 0.2\"\n" +
            "592.2\t29\t\"? 2/3 0.2\"\n" +
            "597.5\t359\t\"y10^2/0.20 4/4 0.6\"\n" +
            "645.2\t44\t\"y11-17^2/-0.14,y11-18^2/0.36 3/4 0.2\"\n" +
            "651.8\t30\t\"y5-18/0.47,y5-17/-0.53 2/2 0.1\"\n" +
            "653.8\t319\t\"y11^2/-0.04 4/4 0.8\"\n" +
            "654.5\t149\t\"y11i^2/0.66 3/4 0.4\"\n" +
            "669.4\t324\t\"y5/0.07 4/4 0.7\"\n" +
            "670.4\t76\t\"b13-46^2/-0.42 4/4 0.2\"\n" +
            "697.3\t24\t\"b7-44/-0.07 2/1 0.1\"\n" +
            "711.5\t43\t\"y12^2/0.15 3/4 0.4\"\n" +
            "723.4\t62\t\"b7-18/0.03,y6-17/0.04 4/4 0.1\"\n" +
            "724.3\t53\t\"b7-17/-0.07 3/4 0.0\"\n" +
            "740.4\t492\t\"y6/0.04 4/4 1.2\"\n" +
            "741.5\t245\t\"b7/0.13 4/4 0.8\"\n" +
            "742.5\t30\t\"b7i/1.13 2/3 0.1\"\n" +
            "744.0\t32\t\"? 2/3 0.0\"\n" +
            "744.3\t96\t\"? 3/4 1.1\"\n" +
            "753.1\t268\t\"y13-18^2/0.22,b14-44^2/-0.25 4/4 1.0\"\n" +
            "757.4\t51\t\"? 3/3 0.6\"\n" +
            "761.9\t294\t\"y13^2/0.02 4/4 0.3\"\n" +
            "762.5\t139\t\"? 4/4 0.8\"\n" +
            "780.2\t33\t\"? 2/3 0.1\"\n" +
            "783.5\t33\t\"? 2/1 0.3\"\n" +
            "789.0\t54\t\"? 3/4 0.3\"\n" +
            "797.0\t87\t\"y14-17^2/0.11 4/4 0.3\"\n" +
            "801.2\t61\t\"? 3/1 0.5\"\n" +
            "804.2\t193\t\"? 4/4 2.4\"\n" +
            "805.5\t143\t\"y14^2/0.11 4/4 1.0\"\n" +
            "806.0\t29\t\"? 2/3 0.1\"\n" +
            "808.1\t21\t\"? 2/1 0.1\"\n" +
            "811.4\t34\t\"y7-44/0.01 2/1 0.3\"\n" +
            "813.4\t35\t\"? 3/4 0.1\"\n" +
            "815.8\t31\t\"? 2/3 0.3\"\n" +
            "818.4\t53\t\"? 4/4 0.2\"\n" +
            "820.5\t392\t\"? 3/4 3.2\"\n" +
            "821.7\t72\t\"?i 4/4 0.4\"\n" +
            "822.1\t183\t\"? 4/4 2.3\"\n" +
            "827.0\t47\t\"? 3/4 0.1\"\n" +
            "828.5\t611\t\"? 3/4 10.9\"\n" +
            "829.6\t70\t\"?i 3/4 0.6\"\n" +
            "831.7\t84\t\"? 4/4 0.4\"\n" +
            "833.1\t48\t\"? 3/4 0.3\"\n" +
            "834.2\t29\t\"?i 2/3 0.1\"\n" +
            "835.6\t81\t\"? 4/4 0.4\"\n" +
            "836.1\t89\t\"? 3/4 0.6\"\n" +
            "837.4\t73\t\"y7-18/0.01 3/4 0.3\"\n" +
            "838.4\t78\t\"y7-17/0.01 3/4 0.4\"\n" +
            "841.0\t94\t\"p-44/0.59,a8/-0.43 3/4 0.6\"\n" +
            "843.3\t126\t\"? 4/4 0.8\"\n" +
            "844.4\t974\t\"p-36/-0.00 4/4 5.0\"\n" +
            "845.1\t672\t\"p-35/0.19 3/4 2.7\"\n" +
            "846.5\t303\t\"? 3/4 2.6\"\n" +
            "849.8\t64\t\"? 4/4 0.3\"\n" +
            "855.5\t148\t\"y7/0.11 4/4 0.4\"\n" +
            "965.6\t24\t\"y8-18/0.15 2/2 0.1\"\n" +
            "966.7\t47\t\"b9-18/0.24,y8-17/0.25 4/4 0.2\"\n" +
            "983.5\t376\t\"y8/0.05 4/4 1.1\"\n" +
            "984.4\t213\t\"b9/-0.06 4/4 0.5\"\n" +
            "985.4\t34\t\"b9i/0.94 3/4 0.2\"\n" +
            "1013.6\t29\t\"? 2/2 0.1\"\n" +
            "1019.5\t29\t\"? 2/3 0.1\"\n" +
            "1021.3\t26\t\"? 2/2 0.1\"\n" +
            "1038.5\t47\t\"b10-17/0.01 4/4 0.2\"\n" +
            "1039.6\t45\t\"b10-17i/1.11 3/4 0.1\"\n" +
            "1055.4\t116\t\"b10/-0.09 4/4 0.4\"\n" +
            "1084.6\t33\t\"? 2/3 0.0\"\n" +
            "1096.4\t145\t\"y9/-0.13 4/4 0.3\"\n" +
            "1097.5\t79\t\"y9i/0.97 4/4 0.2\"\n" +
            "1121.3\t29\t\"? 2/3 0.1\"\n" +
            "1138.7\t75\t\"b11-18/0.16 4/4 0.1\"\n" +
            "1139.6\t57\t\"b11-17/0.06 3/4 0.2\"\n" +
            "1156.5\t107\t\"b11/-0.04 4/4 0.5\"\n" +
            "1157.7\t48\t\"b11i/1.16 3/4 0.3\"\n" +
            "1175.6\t248\t\"y10-18/0.01 4/4 0.2\"\n" +
            "1176.5\t180\t\"y10-17/-0.09 4/4 0.5\"\n" +
            "1193.5\t10000\t\"y10/-0.09 4/4 0.0\"\n" +
            "1194.6\t4444\t\"y10i/1.01 4/4 10.0\"\n" +
            "1195.3\t280\t\"y10i/1.71 4/4 3.5\"\n" +
            "1214.0\t29\t\"? 2/3 0.0\"\n" +
            "1249.3\t21\t\"? 2/1 0.1\"\n" +
            "1267.7\t131\t\"b12-18/0.12 4/4 0.4\"\n" +
            "1268.4\t89\t\"b12-17/-0.18 3/4 0.1\"\n" +
            "1285.6\t253\t\"b12/0.02 4/4 0.4\"\n" +
            "1286.5\t97\t\"b12i/0.92 4/4 0.3\"\n" +
            "1306.6\t392\t\"y11/-0.07 4/4 0.8\"\n" +
            "1307.7\t230\t\"y11i/1.03 4/4 0.5\"\n" +
            "1368.6\t38\t\"b13-18/-0.03 2/1 0.5\"\n" +
            "1386.7\t59\t\"b13/0.07 4/4 0.2\"\n" +
            "1387.7\t49\t\"b13i/1.07 4/4 0.3\"\n" +
            "1403.7\t24\t\"y12-18/0.00 2/2 0.1\"\n" +
            "1421.7\t289\t\"y12/0.00 4/4 0.2\"\n" +
            "1422.6\t206\t\"y12i/0.90 4/4 1.6\"\n" +
            "1514.6\t38\t\"? 3/4 0.3\"\n" +
            "1522.7\t41\t\"y13/-0.04 3/4 0.1\"\n" +
            "1532.6\t56\t\"b14-17/-0.10 4/4 0.2\"\n" +
            "1549.7\t153\t\"b14/0.00 4/4 0.4\"\n" +
            "1550.7\t46\t\"b14i/1.00 3/4 0.1\"\n";

        public const string TEXT_LIB_YEAST_NIST4 =
            "Name: VLHNFLTTPSK/2\n" +
            "MW: 1257.708\n" +
            "RetentionTime: 4830.6\n" + // 80.51 minutes
            "Comment: Spec=Consensus Pep=Tryptic Fullname=R.VLHNFLTTPSK.F/2 Mods=0 Parent=628.854 Inst=it Mz_diff=0.026 Mz_exact=628.8541 Mz_av=629.242 Protein=\"gi|171865|gb|AAC04947.1| Mdm10p: Mitochondria outer membrane protein [Saccharomyces cerevisiae]\" Pseq=4 Organism=\"yeast\" Se=3^X2:ex=0.017585/0.01742,td=759.3/710.7,sd=0/0,hs=27.45/5.15,bs=0.00017,b2=0.035,bd=1470^O1:ex=0.00966/0,td=5180/0,pr=1.87e-006/0,bs=0.00966,bd=5180^P1:sc=19.2/0,dc=12.3/0,ps=2.7/0,bs=0 Sample=1/cbs00174_001_01_cam,2,2 Nreps=2/5 Missing=0.2237/0.0377 Parent_med=628.88/0.08 Max2med_orig=110.1/36.0 Dotfull=0.693/0.000 Dot_cons=0.846/0.036 Unassign_all=0.331 Unassigned=0.238 Dotbest=0.88 Flags=0,0,0 Naa=11 DUScorr=10/0.17/1.7 Dottheory=0.95 Pfin=3.1e+007 Probcorr=1 Tfratio=1.2e+005 Pfract=0\n" +
            "Num peaks: 113\n" +
            "213.1\t283\t\"b2/-0.05 2/2 1.9\"\n" +
            "252.1\t166\t\"? 2/2 1.0\"\n" +
            "253.0\t179\t\"?i 2/1 2.2\"\n" +
            "260.5\t176\t\"? 2/2 2.0\"\n" +
            "270.3\t212\t\"? 2/2 2.8\"\n" +
            "331.2\t624\t\"y3/0.00 2/2 3.2\"\n" +
            "332.3\t506\t\"b3-18/0.09 2/2 0.8\"\n" +
            "341.3\t250\t\"? 2/2 1.9\"\n" +
            "350.3\t614\t\"b3/0.09 2/2 0.2\"\n" +
            "381.6\t204\t\"? 2/1 1.5\"\n" +
            "383.3\t363\t\"? 2/2 1.4\"\n" +
            "386.3\t251\t\"? 2/2 1.0\"\n" +
            "387.2\t213\t\"?i 2/1 1.4\"\n" +
            "393.3\t168\t\"? 2/2 1.2\"\n" +
            "399.2\t162\t\"? 2/2 1.6\"\n" +
            "413.2\t204\t\"b7^2/-0.03 2/2 2.1\"\n" +
            "414.5\t176\t\"y4-18/0.25 2/2 1.4\"\n" +
            "432.4\t829\t\"y4/0.15 2/2 2.9\"\n" +
            "435.4\t312\t\"? 2/2 4.6\"\n" +
            "440.7\t204\t\"b8-45^2/-0.55 2/2 0.0\"\n" +
            "446.6\t196\t\"b4-18/0.35 2/1 2.8\"\n" +
            "458.7\t176\t\"? 2/1 2.0\"\n" +
            "464.3\t176\t\"b4/0.05 2/2 1.4\"\n" +
            "471.3\t211\t\"? 2/1 1.7\"\n" +
            "480.5\t218\t\"? 2/2 0.4\"\n" +
            "482.0\t185\t\"? 2/2 1.2\"\n" +
            "486.5\t201\t\"? 2/1 1.3\"\n" +
            "489.6\t516\t\"b9-45*^2/-0.18 2/1 8.1\"\n" +
            "497.5\t183\t\"? 2/1 2.1\"\n" +
            "498.9\t397\t\"? 2/2 1.4\"\n" +
            "505.2\t321\t\"? 2/2 1.0\"\n" +
            "514.0\t815\t\"y9-18^2/0.22,y9-17^2/-0.28 2/2 2.4\"\n" +
            "514.9\t260\t\"y5-18/-0.39 2/1 4.3\"\n" +
            "523.0\t10000\t\"y9^2/0.22 2/2 4.9\"\n" +
            "525.4\t122\t\"? 2/1 0.9\"\n" +
            "526.3\t188\t\"?i 2/1 2.1\"\n" +
            "529.7\t159\t\"? 2/1 1.4\"\n" +
            "533.4\t550\t\"y5/0.11,b10-45^2/0.11 2/2 0.9\"\n" +
            "535.3\t300\t\"? 2/2 2.5\"\n" +
            "538.5\t180\t\"b10-35^2/0.21 2/1 1.3\"\n" +
            "547.0\t210\t\"b10-18^2/0.21,b10-17^2/-0.29 2/2 2.6\"\n" +
            "553.5\t332\t\"? 2/2 2.0\"\n" +
            "556.9\t153\t\"? 2/1 1.8\"\n" +
            "557.6\t182\t\"?i 2/1 1.0\"\n" +
            "560.8\t247\t\"? 2/2 1.1\"\n" +
            "561.8\t235\t\"?i 2/2 1.0\"\n" +
            "572.1\t253\t\"? 2/2 0.1\"\n" +
            "575.0\t391\t\"b5-36/-0.32 2/2 7.0\"\n" +
            "576.2\t519\t\"?i 2/2 7.5\"\n" +
            "579.6\t1122\t\"y10^2/0.28 2/2 2.3\"\n" +
            "582.9\t549\t\"a5/-0.42 2/2 6.7\"\n" +
            "584.7\t455\t\"? 2/2 3.0\"\n" +
            "585.6\t841\t\"?i 2/2 10.5\"\n" +
            "587.9\t303\t\"? 2/2 3.3\"\n" +
            "588.8\t332\t\"?i 2/1 4.0\"\n" +
            "592.4\t845\t\"? 2/2 2.6\"\n" +
            "593.5\t1228\t\"b5-18/0.18 2/2 5.7\"\n" +
            "594.3\t519\t\"b5-17/-0.02 2/2 1.5\"\n" +
            "596.3\t203\t\"? 2/1 3.0\"\n" +
            "598.3\t869\t\"? 2/2 3.6\"\n" +
            "599.2\t222\t\"?i 2/1 3.2\"\n" +
            "602.9\t170\t\"? 2/2 0.3\"\n" +
            "605.1\t582\t\"? 2/2 5.9\"\n" +
            "606.4\t599\t\"p-45/0.05,p-44/-0.45 2/2 9.9\"\n" +
            "607.0\t565\t\"p-44/0.15 2/2 3.2\"\n" +
            "607.6\t356\t\"? 2/2 4.7\"\n" +
            "610.7\t1875\t\"p-36/-0.14 2/2 11.6\"\n" +
            "611.5\t2983\t\"b5/0.18,p-35/0.15 2/2 0.9\"\n" +
            "612.3\t446\t\"b5i/0.98 2/2 0.8\"\n" +
            "614.1\t316\t\"? 2/2 1.1\"\n" +
            "615.4\t290\t\"?i 2/2 0.6\"\n" +
            "617.0\t398\t\"? 2/2 5.2\"\n" +
            "618.7\t4875\t\"? 2/2 89.4\"\n" +
            "619.3\t2155\t\"p-18/-0.55 2/2 1.7\"\n" +
            "620.1\t2134\t\"p-18/0.25,p-17/-0.25 2/2 19.6\"\n" +
            "621.1\t303\t\"p-18i/1.25 2/1 3.3\"\n" +
            "639.4\t162\t\"? 2/2 1.6\"\n" +
            "646.4\t261\t\"y6/0.02 2/2 0.8\"\n" +
            "647.4\t144\t\"y6i/1.02 2/1 0.5\"\n" +
            "663.4\t622\t\"? 2/2 4.6\"\n" +
            "666.4\t213\t\"? 2/2 1.4\"\n" +
            "691.6\t237\t\"? 2/1 2.7\"\n" +
            "696.3\t131\t\"a6/-0.11 2/1 0.7\"\n" +
            "707.5\t260\t\"b6-17/0.09 2/1 2.3\"\n" +
            "724.3\t465\t\"b6/-0.11 2/2 1.5\"\n" +
            "725.5\t259\t\"b6i/1.09 2/2 1.2\"\n" +
            "754.7\t198\t\"? 2/1 1.0\"\n" +
            "793.4\t720\t\"y7/-0.05 2/2 4.3\"\n" +
            "794.6\t358\t\"y7i/1.15 2/2 2.5\"\n" +
            "798.8\t251\t\"? 2/2 1.0\"\n" +
            "807.4\t168\t\"b7-18/-0.05 2/1 1.2\"\n" +
            "808.4\t268\t\"b7-17/-0.05 2/2 3.0\"\n" +
            "811.8\t234\t\"? 2/1 1.9\"\n" +
            "826.5\t337\t\"? 2/2 1.0\"\n" +
            "847.3\t214\t\"? 2/2 2.5\"\n" +
            "849.5\t167\t\"? 2/1 2.1\"\n" +
            "865.5\t442\t\"? 2/2 5.3\"\n" +
            "869.4\t667\t\"? 2/2 5.3\"\n" +
            "871.5\t271\t\"? 2/2 3.3\"\n" +
            "880.5\t297\t\"? 2/2 2.2\"\n" +
            "890.0\t339\t\"y8-17/-0.49,y8-18/0.51 2/2 1.3\"\n" +
            "906.9\t268\t\"y8/-0.59 2/2 0.4\"\n" +
            "907.5\t1996\t\"y8/0.01 2/2 0.5\"\n" +
            "908.5\t844\t\"b8-18/-0.00 2/2 3.8\"\n" +
            "910.5\t210\t\"? 2/2 1.1\"\n" +
            "926.6\t531\t\"b8/0.10 2/2 1.4\"\n" +
            "941.5\t131\t\"? 2/1 0.7\"\n" +
            "995.4\t367\t\"? 2/2 4.3\"\n" +
            "996.1\t270\t\"?i 2/1 4.2\"\n" +
            "1044.5\t549\t\"y9/-0.05 2/2 5.2\"\n" +
            "1045.7\t333\t\"y9i/1.15 2/2 1.1\"\n" +
            "1070.8\t268\t\"? 2/2 1.0\"\n" +
            "1110.6\t387\t\"b10/0.01 2/2 2.2\"\n";

        public static readonly LibKey[] KEYS_LIB_BICENE_NIST =
        {
            new LibKey(SmallMoleculeLibraryAttributes.Create("N,N-Bis(2-hydroxyethyl)glycine", "C6H13NO4", "FSVCELGFZIQNCK-UHFFFAOYSA-N", MoleculeAccessionNumbers.TagCAS+":150-25-4" ), Adduct.M_PLUS_H),
        };

        public const string TEXT_LIB_NO_ADDUCT =
            "\n" +
            "Name: C6:OH b\n" +
            "PrecursorMz: 258.17001\n" + // No adduct declared, but this has to be [M-H2O+H] for the mz to make sense
            "Precursor_type: \n" +
            "Collision_energy: 30.0\n" +
            "Ionization: ESI\n" +
            "IonMode: positive\n" +
            "RetentionTime: 1.52616\n" +
            "InChiKey: \n" +
            "Formula: C13 H25 N O5\n" +
            "CASNo: \n" +
            "Smiles: \n" +
            "CompoundClass: OH\n" +
            "CCS: 123.45\n" + // N.B. completely invented value for test purposes
            "Comment: \n" +
            "Num peaks: 5\n" +
            "55.0546 8078.07\n" +
            "57.0339 7511.40\n" +
            "60.0810 115745.10\n" +
            "61.4383 3796.87\n" +
            "64.3453 3654.89\n";

        public const string TEXT_LIB_FORMULA_PLUS =
            "\n" +
            "Name: ACar 4:0\n" +
            "Synon: [M]+\n" +
            "Synon: $:00in-source\n" +
            "DB#: LipidBlast000001\n" +
            "InChIKey: QWYFHHGCZUCMBN-UHFFFAOYSA-O\n" +
            "CCS_sqa: 23.145\n" + // N.B. completely invented value for test purposes
            "Precursor_type: [M]+\n" +
            "Spectrum_type: MS2\n" +
            "PrecursorMZ: 232.15433\n" +
            "Instrument_type: in-silico QTOF\n" +
            "Instrument: SCIEX 5600\n" +
            "Ion_mode: P\n" +
            "Collision_energy: 45 V\n" +
            "Formula: [C11H22NO4]+\n" +
            "MW: 232\n" +
            "ExactMass: 232.1543346040907\n" +
            // TODO(bspratt) - MoNA embeds a lot of information in Comments that we should pick out, like SMILES, InChIKey, retention time etc
            "Comments: \"SMILES=CCCC(=O)OC(CC(O)=O)C[N+](C)(C)C\" \"compound class=ACar\" \"computed SMILES=O=C(O)CC(OC(=O)CCC)C[N+](C)(C)C\" \"computed InChI=InChI=1S/C11H21NO4/c1-5-6-11(15)16-9(7-10(13)14)8-12(2,3)4/h9H,5-8H2,1-4H3/p+1\" \"retention time=0.51\" \"collision energy spread=15 V\" \"author=Tobias Kind, Hiroshi Tsugawa\" \"computed mass accuracy=2.3431646471704717\" \"computed mass error=5.439758187435473E-4\" \"SPLASH=splash10-001r-7090000000-aa12589a2481560ea0d5\" \"submitter=Tobias Kind (University of California, Davis)\" \"MoNA Rating=3.6363636363636367\"\n" +
            "Num Peaks: 2\n" +
            "85.02841 80.080080\n" +
            "232.1543 100.000000\n";

        public const string TEXT_LIB_BICINE_NIST =
            "\n" +
            "Name: N,N-Bis(2-hydroxyethyl)glycine\n" +
            "Synon: BICINE\n" +
            "Synon: N,N-di(2-Hydroxyethyl)glycine\n" +
            "Synon: Glycine, N,N-bis(2-hydroxyethyl)-\n" +
            "Synon: Bicene\n" +
            "Synon: Bis(2-Hydroxyethyl)glycine\n" +
            "Synon: Diethanol glycine\n" +
            "Synon: Diethylolglycine\n" +
            "Synon: Dihydroxyethylglycine\n" +
            "Synon: DHEG\n" +
            "Synon: Fe-3-Specific\n" +
            "Synon: Glycine, N,N-dihydroxyethyl-\n" +
            "Synon: N,N-(2-Hydroxyethyl)glycine\n" +
            "Synon: N,N-Bis(.beta.-hydroxyethyl)glycine\n" +
            "Synon: N,N-Bis(hydroxyethyl)glycine\n" +
            "Synon: N,N-Bis(2-hydroxyethyl)aminoacetic acid\n" +
            "Synon: N,N-Dihydroxyethyl glycine\n" +
            "Synon: [Bis(2-hydroxyethyl)amino]acetic acid #\n" +
            "Synon: N,N-Dihydroxyethylglycine\n" +
            "Synon: NSC 7342\n" +
            "Ion_mode: P\n" +
            "RetentionTimeMins: 80.51\n" + // 4830.6 seconds
            "PrecursorMZ: 164.0917\n" +
            "Precursor_type: [M+H]+\n" +
            "Collision_energy: 23V\n" +
            "In-source_voltage: 175V\n" +
            "Instrument: Agilent QTOF 6530\n" +
            "Instrument_type: Q-TOF\n" +
            "Sample_inlet: direct flow injection\n" +
            "Ionization: ESI\n" +
            "Collision_gas: N2\n" +
            "AUX: Consensus spectrum; micromol/L in water/acetonitrile/formic acid (50/50/0.1) mz_diff=0.0673 Vial_ID=2195 QTOF_id_2012=3917\n" +
            "Spectrum_type: ms2\n" +
            "InChIKey: FSVCELGFZIQNCK-UHFFFAOYSA-N\n" +
            "Formula: C6H13NO4\n" +
            "MW: 163\n" +
            "ExactMass: 163.084458\n" +
            "CAS#: 150-25-4;  NIST#: 1108883\n" +
            "Comments: NIST Mass Spectrometry Data Center\n" +
            "Num Peaks: 46\n" +
            "28.018 7.79 \"84/86\"\n" + // Annotation should be ignored - not multipart
            "29.039 5.19 \"? 85/86\"\n" + // Annotation should be ignored - fragment unknown
            "30.034 13.19 \"?i 86/86\"\n" + // Annotation should be ignored- fragment unknown
            "41.027 5.00 \"testfrag3 85/86\"\n" + // Annotation should come back as name "testfrag3" and note "85/86"
            "42.035 9.39 \"86/86\"\n" +
            "44.050 23.48 \"86/86\"\n" +
            "45.034 430.17 \"86/86\"\n" +
            "45.061 18.78 \"86/86\"\n" +
            "45.080 6.59 \"49/86\"\n" +
            "45.095 14.79 \"86/86\"\n" +
            "45.115 4.70 \"85/86\"\n" +
            "55.055 14.29 \"86/86\"\n" +
            "56.050 999.00 \"86/86\"\n" +
            "56.086 35.86 \"83/86\"\n" +
            "56.118 40.36 \"83/86\"\n" +
            "56.140 13.49 \"86/86\"\n" +
            "56.159 4.30 \"57/86\"\n" +
            "56.174 4.60 \"76/86\"\n" +
            "56.190 4.20 \"83/86\"\n" +
            "56.208 3.80 \"80/86\"\n" +
            "58.029 8.59 \"86/86\"\n" +
            "58.065 10.99 \"86/86\"\n" +
            "70.065 67.53 \"86/86\"\n" +
            "70.090 4.30 \"85/86\"\n" +
            "71.049 4.40 \"83/86\"\n" +
            "72.044 8.59 \"85/86\"\n" +
            "72.081 42.96 \"86/86\"\n" +
            "73.052 4.40 \"81/86\"\n" +
            "74.060 484.91 \"86/86\"\n" +
            "74.092 22.28 \"86/86\"\n" +
            "74.118 8.79 \"58/86\"\n" +
            "74.138 16.88 \"86/86\"\n" +
            "74.164 5.59 \"86/86\"\n" +
            "82.065 10.49 \"86/86\"\n" +
            "88.076 53.65 \"86/86\"\n" +
            "88.104 4.40 \"86/86\"\n" +
            "100.075 127.87 \"86/86\"\n" +
            "100.105 8.39 \"85/86\"\n" +
            "102.054 13.19 \"86/86\"\n" +
            "118.086 544.55 \"86/86\"\n" +
            "118.127 25.17 \"86/86\"\n" +
            "118.159 9.39 \"53/86\"\n" +
            "118.184 18.88 \"86/86\"\n" +
            "118.217 6.09 \"86/86\"\n" +
            "128.070 4.10 \"testfrag_next_to_last 83/86 \"\n" +
            "146.081 4.90 \"testfrag_last 85/86 note \"\n";

        public const string TEXT_LIB_GMD =
            "Name: M000000_A097001-101-xxx_NA_0_FALSE_MDN35_ALK_Unknown#bth-pae-001\n" +
            "Synon: MST N: Unknown#bth-pae-001\n" +
            "Synon: RI: 0\n" +
            "Synon: RI MDN35 ALK: FALSE\n" +
            "Synon: MST: A097001-101\n" +
            "Synon: MST ISOTOPE: 101\n" +
            "Synon: MST ID: A097001-101-xxx_\n" +
            "Synon: MST SEL MASS: NA\n" +
            "Synon: GMD LINK: http://gmd.mpimp-golm.mpg.de/Analytes/aea8340f-e606-48c2-8371-dcf3d7d18a77.aspx\n" +
            "Synon: GMD VERS: 21.Nov.2011 21:54 hummel\n" +
            "CAS#: NA\n" +
            "Comment: consensus spectrum of 1 spectra per analyte, MPIMP ID and isotopomer, with majority threshold = 60%\n" +
            "DB#: 16\n" +
            "Num Peaks: 50\n" +
            "70:10 76:35 77:1000 78:110 79:42 \n" +
            "80:4 81:7 86:6 87:5 88:21 \n" +
            "90:42 91:40 97:15 100:31 103:213 \n" +
            "104:27 105:10 107:48 108:6 110:41 \n" +
            "112:14 118:39 120:79 121:8 122:4 \n" +
            "130:33 134:589 135:55 136:25 137:3 \n" +
            "138:2 140:83 141:4 143:104 144:8 \n" +
            "160:4 184:610 185:58 186:24 187:2 \n" +
            "198:7 199:3 214:294 215:33 216:12 \n" +
            "217:1 228:3 229:109 230:12 231:5 \n" +
            "\n" +
            "Name: M000880_A098001-101-xxx_NA_0_FALSE_MDN35_ALK_Glycine, N,N-dimethyl- (1TMS)\n" +
            "Synon: MST N: Glycine, N,N-dimethyl- (1TMS)\n" +
            "Synon: RI: 10,5\n" +
            "Synon: RI MDN35 ALK: FALSE\n" +
            "Synon: MST: A098001-101\n" +
            "Synon: MST ISOTOPE: 101\n" +
            "Synon: MST ID: A098001-101-xxx_\n" +
            "Synon: MST SEL MASS: NA\n" +
            "Synon: METB: M000880_NA_correct\n" +
            "Synon: METB N: (dimethylamino)acetic acid\n" +
            "Synon: METB N: (Dimethylamino)acetic acid\n" +
            "Synon: METB N: 1,5-Dihydroxynaphthalene\n" +
            "Synon: METB N: Dimethylglycine\n" +
            "Synon: METB N: Glycine, N,N-dimethyl-\n" +
            "Synon: METB N: N,N-dimethylglycine\n" +
            "Synon: METB N: N,N-Dimethylglycine\n" +
            "Synon: METB N: N,N-Dimethylglycine hydrochloride\n" +
            "Synon: METB CAS: 1118-68-9\n" +
            "Synon: METB KEGG: C01026\n" +
            "Synon: METB InChI: InChI=1S/C4H9NO2/c1-5(2)3-4(6)7/h3H2,1-2H3,(H,6,7)\n" +
            "Synon: METB InChIKey: FFDGPVCHZBVARC-UHFFFAOYSA-N\n" +
            "Synon: GMD LINK: http://gmd.mpimp-golm.mpg.de/Analytes/f8adb36f-eeba-4d1d-a589-c6ebff81f8ea.aspx\n" +
            "Synon: GMD VERS: 21.Nov.2011 21:54 hummel\n" +
            "Formula: C7H17NO2Si\n" +
            "MW: 175,301\n" +
            "CAS#: NA\n" +
            "Comment: consensus spectrum of 1 spectra per analyte, MPIMP ID and isotopomer, with majority threshold = 60%\n" +
            "DB#: 17\n" +
            "Num Peaks: 64\n" +
            "70,1:81.2 71:109 72:255 76:122 77:264 \n" +
            "78:50 79:93 80:87 81:3 82:2 \n" +
            "83:2 84:155 85:25 86:96 87:10 \n" +
            "88:56 89:34 90:3 91:2 92:4 \n" +
            "93:12 95:0 96:0 98:0 99:3 \n" +
            "100:34 101:110 102:182 103:291 104:30 \n" +
            "105:3 107:4 108:0 110:0 113:0 \n" +
            "114:37 115:14 116:129 117:179 118:15 \n" +
            "120:0 126:1 127:3 128:4 129:2 \n" +
            "130:254 131:18 132:148 133:7 134:8 \n" +
            "144:9 145:0 146:31 158:0 160:1000 \n" +
            "161:106 162:31 163:0 174:9 175:657 \n" +
            "176:72 177:18 178:0 552:0 \n";

        public const string TEXT_LIB_MINE = // As from https://minedatabase.mcs.anl.gov/#/download
            "NAME: Withanone; PlaSMA ID-2558\n" +
            "PRECURSORMZ: 471.27412\n" +
            "PRECURSORTYPE: [M+H]+\n" +
            "FORMULA: C28H38O6\n" +
            "Ontology: Withanolides and derivatives\n" +
            "INCHIKEY: FAZIYUIDUNHZRG-UHFFFAOYNA-N\n" +
            "SMILES: CC(C1CC(C)=C(C)C(=O)O1)C1(O)CCC2C3C4OC4C4(O)CC=CC(=O)C4(C)C3CCC12C\n" +
            "RETENTIONTIME: 6.82\n" +
            "CCS: 220.9656493\n" +
            "IONMODE: Positive\n" +
            "COLLISIONENERGY: \n" +
            "Comment: Annotation level-1; PlaSMA ID-2558; ID title-Withanone; Max plant tissue-Standard only\n" +
            "NUM PEAKS: 11\n" +
            "68.06053	24\n" +
            "99.06053	0\n" +
            "153.09654	20\n" +
            "171.07501	20\n" +
            "181.09505	21\n" +
            "220.09355	22\n" +
            "263.13885	18\n" +
            "283.15988	31\n" +
            "417.25351	27\n" +
            "435.2543	24\n" +
            "471.27539	57\n";

        public const string LIB_TEXT_MONA =
            "\n" +
            "Name: 4-Trimethylammoniobutanoic acid\n" +
            "Synon: $:00in-source\n" +
            "DB#: FiehnHILIC000113\n" +
            "InChIKey: JHPNVNIEXXLNTR-UHFFFAOYSA-O\n" +
            "Precursor_type: [M]+\n" +
            "Spectrum_type: MS2\n" +
            "PrecursorMZ: 147.1204\n" +
            "Instrument_type: LC-ESI-QFT\n" +
            "Instrument: Thermo Q Exactive HF\n" +
            "Ion_mode: P\n" +
            "Collision_energy: HCD (NCE 20-30-40%)\n" +
            "Formula: C7H16NO2+\n" +
            "MW: 146\n" +
            "ExactMass: 146.11755517209073\n" +
            "Comments: \"computed SMILES=O=C(O)CCC[N+](C)(C)C\" \"computed InChI=InChI=1S/C7H15NO2/c1-8(2,3)6-4-5-7(9)10/h4-6H2,1-3H3/p+1\" \"isotope=M + 2\" \"scan number=1397\" \"retention time=7.66005\" \"author=Michael Sa and Megan Showalter\" \"column=Waters Acquity UPLC BEH Amide column (2.1 x 150mm: 1.7µm)\" \"computed mass accuracy=6820.219410895567\" \"computed mass error=1.00339340781872\" \"SPLASH=splash10-000j-9300000000-1f92b994aa228da7795a\" \"submitter=Megan Showalter (University of California, Davis)\" \"MoNA Rating=4.615384615384616\"\n" +
            "Num Peaks: 11\n" +
            "50.0114 0.517519\n" +
            "60.08153 44.408092\n" +
            "61.07857 3.670960\n" +
            "61.08484 31.849958\n" +
            "61.08778 1.362497\n" +
            "87.04461 71.895759\n" +
            "87.4569 0.561183\n" +
            "88.04792 81.575568\n" +
            "95.58894 0.640074\n" +
            "147.09235 3.829274\n" +
            "147.12108 100.000000\n" +
            "\n" +
            "Name: Ferrichrome\n" +
            "Synon: $:00in-source\n" +
            "DB#: CCMSLIB00000078897\n" +
            "InChIKey: QNVPQTXXHKIFLL-UHFFFAOYSA-N\n" +
            "Precursor_type: [M+Na]+\n" +
            "Spectrum_type: MS2\n" +
            "PrecursorMZ: 763.0\n" +
            "Instrument: Hybrid FT\n" +
            "Ion_mode: P\n" +
            "Formula: [C27H42FeN9NaO12]+\n" +
            "MW: 763\n" +
            "ExactMass: 763.2194509840907\n" +
            "Comments: \"cas number=15630-64-5\" \"pubmed id=27424\" \"SMILES=CC(=O)N(CCCC1C(=O)NC(C(=O)NC(C(=O)NCC(=O)NCC(=O)NCC(=O)N1)CCCN(C(=O)C)[O-])CCCN(C(=O)C)[O-])[O-].[Fe+3][Na+]\" \"computed SMILES=CC(=O)N(CCCC1C(=O)NCC(=O)NCC(=O)NCC(=O)NC(CCCN(C(=O)C)[O-])C(=O)NC(CCCN(C(=O)C)[O-])C(=O)N1)[O-].[Fe+3][Na+]\" \"computed InChI=InChI=1S/C27H42N9O12.Fe.Na/c1-16(37)34(46)10-4-7-19-25(43)30-14-23(41)28-13-22(40)29-15-24(42)31-20(8-5-11-35(47)17(2)38)26(44)33-21(27(45)32-19)9-6-12-36(48)18(3)39;;/h19-21H,4-15H2,1-3H3,(H,28,41)(H,29,40)(H,30,43)(H,31,42)(H,32,45)(H,33,44);;/q-3;+3;+1\" \"ion source=DI-ESI\" \"compound source=Commercial\" \"exact mass=763.219\" \"charge state=1\" \"source file=f.smascuch/Standards/STANDARD_Ferrichrome_FT01_50K_MS2_mz763.mzXML;\" \"origin=GNPS-LIBRARY\" \"author=smascuch, Michael Meehan &amp; Sam Mascuch, Pieter Dorrestein &amp; Lena Gerwick\" \"computed mass accuracy=30417.65266591188\" \"computed mass error=-23.208668984090764\" \"SPLASH=splash10-0f7a-0116957601-00f3c1f5e5d4de207a7e\" \"submitter=GNPS Team (University of California, San Diego)\" \"MoNA Rating=2.727272727272727\"\n" +
            "Num Peaks: 6\n" +
            "210.000992 0.000000\n" +
            "262.147949 100.000000\n" +
            "262.148865 0.000000\n" +
            "262.149750 200.000000\n" +
            "262.150665 300.000000\n" +
            "262.177665 400.000000\n" +
            "\n";

        public const string LIB_TEXT_MZVAULT =
            "\n" +
            "#PSI Spectral Library Format\n" +
            "MS:1009003|Name = Epirizole\n" +
            "MS:1009001|Spectrum index = 1107\n" +
            "MS:1000894|RetentionTime = 1.74066\n" +
            "MS:1000864|Formula = C11H14N4O2\n" +
            "MS:1002894|InChiKey = RHAXSHUQNIEUEY-UHFFFAOYAC\n" +
            "MS:1009100|CASNo = \n" +
            "MS:1000868|Smiles = Cc1cc(nc(n1)n2c(cc(n2)C)OC)OC\n" +
            "MS:1009101|CompoundClass = \n" +
            "MS:1000744|Selected Ion m/z = 235.04883\n" +
            "MS:1000045|Collision_energy = 33.3\n" +
            "Precursor_type = \n" +
            "MS:1000073|Electrosprary ionization\n" +
            "MS:1000130|Positive scan\n" +
            "MS:1009006|number of peaks = 67\n" +
            "54.8789 4301.70\n" +
            "56.0499 606354.10\n" +
            "56.9651 186512.50\n" +
            "57.0702 26894.91\n" +
            "58.0655 18950.98\n" +
            "59.0495 4674.89\n" +
            "60.0447 30842.68\n" +
            "70.0652 38320.37\n" +
            "74.0601 64468.65\n" +
            "74.9753 10056.45\n" +
            "84.9596 57395.49\n" +
            "86.0601 96432.38\n" +
            "88.0393 73237.30\n" +
            "95.0854 9277.52\n" +
            "98.9754 85266.63\n" +
            "100.9910 94618.79\n" +
            "102.0550 1557200.00\n" +
            "102.9696 7409.72\n" +
            "104.0706 9240.21\n" +
            "107.0853 9679.18\n" +
            "109.1012 9594.82\n" +
            "110.0713 89577.13\n" +
            "112.9910 32771.41\n" +
            "114.0550 105109.00\n" +
            "116.9858 54641.13\n" +
            "118.0862 20236.58\n" +
            "119.0016 30095.12\n" +
            "119.0856 7297.21\n" +
            "120.9991 23992.73\n" +
            "130.0046 6795.89\n" +
            "130.0738 7348.58\n" +
            "131.0016 273602.10\n" +
            "132.0656 546111.90\n" +
            "134.0451 18632.84\n" +
            "134.9964 20397.06\n" +
            "143.0017 8175.11\n" +
            "143.0122 6078.07\n" +
            "143.0816 43623.21\n" +
            "145.0975 8857.70\n" +
            "147.1171 6639.98\n" +
            "149.0123 594835.40\n" +
            "155.0123 8942.16\n" +
            "160.0606 526461.80\n" +
            "161.0123 19541.33\n" +
            "162.1030 7385.49\n" +
            "164.0173 19866.08\n" +
            "166.0616 6882.53\n" +
            "167.0228 141595.00\n" +
            "171.0769 9519.30\n" +
            "175.0282 15731.51\n" +
            "177.1638 7834.97\n" +
            "179.0225 51275.80\n" +
            "179.1071 17609.73\n" +
            "189.0070 6404.95\n" +
            "189.0872 238332.50\n" +
            "191.0227 319221.00\n" +
            "197.0338 8952.61\n" +
            "207.0173 8484.99\n" +
            "207.0353 6133.31\n" +
            "209.0331 72709.38\n" +
            "217.0391 14763.00\n" +
            "217.0820 163496.00\n" +
            "217.1950 24278.64\n" +
            "235.0925 551996.30\n" +
            "235.1189 207471.40\n" +
            "235.1690 76988.90\n" +
            "235.2054 61542.45\n";

        private const string LIB_TEXT_NO_ADDUCT =
            "Name: (2,2,2,-2H3)ACETOPHENONE\n" +
            "Spectrum_type: in-source\n" +
            "InChIKey: KWOLFJPFCHCOCG-FIBGUPNXSA-N\n" +
            "Spectrum_type: MS1\n" +
            "Instrument_type: EI-B\n" +
            "Instrument: HITACHI RMU-6M\n" +
            "Ion_mode: P\n" +
            "Formula: C8H8O\n" +
            "MW: 123\n" +
            "ExactMass: 123.076345\n" +
            "DB#: 18525\n" +
            "Comments: \"SMILES=[2H]C([2H])([2H])C(=O)c(c1)cccc1\" \"InChI=InChI=1S/C8H8O/c1-7(9)8-5-3-2-4-6-8/h2-6H,1H3/i1D3\" \"computed SMILES=C([2H])([2H])([2H])C(=O)C1=CC=CC=C1\" \"accession=JP001492\" \"date=2016.01.19 (Created 2008.10.21, modified 2011.05.06)\" \"author=YAMAMOTO M, DEP. CHEMISTRY, FAC. SCIENCE, NARA WOMEN'S UNIV.\" \"license=CC BY-NC-SA\" \"exact mass=120.05751\" \"ionization energy=70 eV\" \"ion type=[M]+*\" \"SPLASH=splash10-0a6r-9700000000-d60fa0e8b56fa3eac3dd\" \"submitter=University of Tokyo Team (Faculty of Engineering, University of Tokyo)\" \"MoNA Rating=3.75\"\n" +
            "Num Peaks: 26\n" +
            "18 45.96\n" +
            "27 23.98\n" +
            "28 23.48\n" +
            "37 11.79\n" +
            "38 21.68\n" +
            "38.5 11.49\n" +
            "39 27.28\n" +
            "40 13.39\n" +
            "46 179.84\n" +
            "50 110.80\n" +
            "51 266.66\n" +
            "52 31.57\n" +
            "52.5 14.19\n" +
            "53 13.19\n" +
            "63 11.19\n" +
            "74 37.77\n" +
            "75 25.58\n" +
            "76 30.27\n" +
            "77 808.57\n" +
            "78 64.64\n" +
            "79 25.68\n" +
            "105 999.00\n" +
            "106 81.03\n" +
            "122 13.69\n" +
            "123 454.09\n" +
            "124 43.66\n";

        private const string LIB_TEXT_RTINSECONDS =
            "Name: IDAGLSESYTCYLLSKGK/2\n" +
            "MW: 2003.9873686836993\n" +
            "Comment: Mods=1/10,C,Carbamidomethyl Parent=1003.0015093739197 Protein=\"sp|A5A625|YIBV_ECOLI\" RTINSECONDS=3683.3344054867403 MS2PIP_ID=\"sp|A5A625|YIBV_ECOLI_001_000_2\"\n" +
            "Num peaks: 30\n" +
            "114.091293	28	\"b1\"\n" +
            "147.112762	88	\"y1\"\n" +
            "204.134232	2262	\"y2\"\n" +
            "229.118225	916	\"b2\"\n" +
            "300.155334	331	\"b3\"\n" +
            "332.229156	1298	\"y3\"\n" +
            "357.176788	10000	\"b4\"\n" +
            "419.261200	6707	\"y4\"\n" +
            "470.260834	927	\"b5\"\n" +
            "532.345276	3559	\"y5\"\n" +
            "557.292908	55	\"b6\"\n" +
            "645.429321	2132	\"y6\"\n" +
            "686.335510	198	\"b7\"\n" +
            "773.367554	67	\"b8\"\n" +
            "808.492676	1400	\"y7\"\n" +
            "968.523315	3038	\"y8\"\n" +
            "1069.571045	2459	\"y9\"\n" +
            "1232.634399	2039	\"y10\"\n" +
            "1319.666382	3234	\"y11\"\n" +
            "1448.708984	1506	\"y12\"\n" +
            "1473.656616	21	\"b13\"\n" +
            "1535.740967	5521	\"y13\"\n" +
            "1586.740723	11	\"b14\"\n" +
            "1648.825073	103	\"y14\"\n" +
            "1673.772705	0	\"b15\"\n" +
            "1705.846558	113	\"y15\"\n" +
            "1776.883667	32	\"y16\"\n" +
            "1801.867676	0	\"b16\"\n" +
            "1858.889160	13	\"b17\"\n" +
            "1891.910645	15	\"y17\"\n";

        private const string TEXT_RIKEN =
            "\n" +
            "NAME: Glucolesquerellin\n" +
            "PRECURSORMZ: 448.0775180239999\n" +
            "PRECURSORTYPE: [M-H]-\n" +
            "IONMODE: Negative\n" +
            "FORMULA: C14H27NO9S3\n" +
            "SMILES: CSCCCCCCC(=NOS(=O)(=O)O)S[C@H]1[C@@H]([C@H]([C@@H]([C@H](O1)CO)O)O)O\n" +
            "INCHIKEY: ZAKICGFSIJSCSF-LPUQOGTASA-N\n" +
            "IONIZATION: ESI\n" +
            "INSTRUMENTTYPE: LC-ESI-QTOF\n" +
            "COLLISIONENERGY: 30 eV\n" +
            "RETENTIONTIME: 4.0667\n" +
            "CCS: 189.9914404\n" +
            "ONTOLOGY: Alkylglucosinolates\n" +
            "COMMENT: DB#=SMI00033; origin=MassBank High Quality Mass Spectral Database\n" +
            "Num Peaks: 4\n" +
            "95.949\t819\n" +
            "96.957\t1000\n" +
            "259.012\t221\n" +
            "274.987\t104\n" +
            "\n" +
            "NAME: PRZ_M632a\n" +
            "PRECURSORMZ: 632.02808644783\n" +
            "PRECURSORTYPE: [M-H]-\n" +
            "FORMULA: C21H26Cl3N3O11S\n" +
            "Ontology: NA\n" +
            "INCHIKEY: UNCSXZRCRHPBJA-UHFFFAOYNA-N\n" +
            "SMILES: CCCN(CCOC=1C(=CC(=C(C1Cl)OC2C(C(C(C(COS(O)(=O)=O)O2)O)O)O)Cl)Cl)C(N3C=CN=C3)=O\n" +
            "RETENTIONTIME: \n" +
            "CCS: -1\n" + // Note negative CCS, presumably means N/A
            "IONMODE: Negative\n" +
            "INSTRUMENTTYPE: LC-ESI-QFT\n" +
            "INSTRUMENT: Q Exactive Orbitrap Thermo Scientific\n" +
            "COLLISIONENERGY: 40 (nominal)\n" +
            "Comment: DB#=ET201352; origin=MassBank-EU\n" +
            "Num Peaks: 3\n" +
            "67.0295	10\n" +
            "96.9602	1000\n" +
            "241.0032	9200\n";

        private const string TEXT_NIST_PARENTHESIS =
            "\n" +
            "NAME: 1,2-Dimethylpropyl methylphosphonofluoridate\n" +
            "FORM: C6 H14 F O2 P1\n" +  // N.B. we expect to retain this atom order, and that weird "1" in P1, but we'll drop the spaces
            "CASNBR: 6154-51-4\n" +
            "NUM PEAKS: 44\n" +
            "( 26 19) (  27 164) (29   131 ) ( 31 14  ) ( 38 21)\n" + // BSP note: I stuck some extra spaces in here just in case
            " ( 39 197) ( 40 28) ( 41 262) ( 42 139) ( 43 262 )\n" +
            "( 44 15) ( 45 68) ( 47 66) ( 50 16) ( 51 22) \n" +
            "( 52 9) ( 53 66) ( 54 21) ( 55 614)   ( 56 35 ) \n" +
            "( 65 8) ( 66 21) ( 67 66) ( 68 9) ( 69 43)\n" +
            "( 70 563) ( 71 121) ( 78 13) ( 79 28) ( 80 32)\n" +
            "( 81 166) ( 82 457) ( 83 87) ( 98 107) ( 99 1000)\n" +
            "( 100 16) ( 101 6) ( 110 6) ( 111 10) ( 124 75)\n" +
            "( 125 791) ( 126 459) ( 127 46) ( 153 42)\n" +
            "\n" +
            "NAME:ketone resin dimer 14 (DOME 2009) RI=1565.4\n" + // Note no mass hint here - not necessarily an error with GCMS
            "COMMENT: RI=1565.4,  14.2924 min 1EE8529691734FF6B9B0360C49DA5AA5|RI:1565.40\n" +
            "RI:1565.40\n" +
            "CASNO:1EE852~1-N1018\n" + // Note - not actually a CAS number, but seen in the wild
            "RT:14.292\n" +
            "SOURCE:C:\\Users\\bob\\OneDrive - lab\\GC stuff\\TotoroQuadrupole.msp\n" +
            "NUM PEAKS:  52 \n" +
            "( 39  241) ( 41  445) ( 42   50) ( 43  108) ( 51   50) \n" + // BSP note - I did NOT add these extra spaces, seen in the wild
            "( 53  192) ( 54   86) ( 55  425) ( 57   37) ( 65  154) \n" +
            "( 66   68) ( 67  734) ( 68  125) ( 69  117) ( 70   64) \n" +
            "( 77  247) ( 78   49) ( 79  514) ( 80  122) ( 81  271) \n" +
            "( 82  119) ( 83  194) ( 91  186) ( 92   44) ( 93  180) \n" +
            "( 94  303) ( 95 1000) ( 96  309) ( 97  352) ( 98  754) \n" +
            "( 99   72) (107   81) (109  128) (110  618) (111  214) \n" +
            "(112   58) (117   28) (123   38) (124   21) (125   33) \n" +
            "(131   59) (132   33) (133   44) (135   32) (136   19) \n" +
            "(149   44) (163   32) (164   74) (174   32) (192  362) \n" +
            "(193   50) (194   34) \n" +
            "\n" +
            "NAME:Tricosane\n" +
            "COMMENT: Japan AIST/NIMC Database- Spectrum MS-NW-5921|RI:2300.00\n" +
            "RI:2300.00\n" +
            "MW:324.6\n" +
            "CASNO:638-67-5\n" +
            "RSN:503\n" +
            "SOURCE:C:\\NIST11\\AMDIS32\\LIB\\HiMWHCs.MSP\n" +
            "NUM PEAKS:  100 \n" +
            "( 26    1) ( 27   38) ( 28    9) ( 29  142) ( 30    3) \n" +
            "( 39   20) ( 40    5) ( 41  257) ( 42   67) ( 43  697) \n" +
            "( 44   22) ( 53    9) ( 54   20) ( 55  216) ( 56  121) \n" +
            "( 57 1000) ( 58   43) ( 65    1) ( 66    1) ( 67   20) \n" +
            "( 68   21) ( 69  121) ( 70   99) ( 71  672) ( 72   38) \n" +
            "( 73    1) ( 77    1) ( 79    2) ( 81    9) ( 82   25) \n" +
            "( 83   94) ( 84   68) ( 85  461) ( 86   30) ( 87    1) \n" +
            "( 95    3) ( 96   14) ( 97   72) ( 98   54) ( 99  172) \n" +
            "(100   12) (109    1) (110    8) (111   36) (112   47) \n" +
            "(113  118) (114   10) (124    4) (125   19) (126   42) \n" +
            "(127   94) (128    9) (138    3) (139    9) (140   36) \n" +
            "(141   77) (142    9) (152    2) (153    5) (154   31) \n" +
            "(155   67) (156    8) (166    1) (167    3) (168   27) \n" +
            "(169   57) (170    7) (181    2) (182   23) (183   53) \n" +
            "(184    8) (195    1) (196   20) (197   47) (198    7) \n" +
            "(210   17) (211   41) (212    7) (224   14) (225   38) \n" +
            "(226    6) (238   12) (239   34) (240    6) (252    9) \n" +
            "(253   30) (254    6) (266    8) (267   26) (268    6) \n" +
            "(280    4) (281   15) (282    3) (294    3) (295    9) \n" +
            "(296    2) (323    2) (324  134) (325   34) (326    4) \n";

        private const string MODS_USING_PARENS = // As in  https://chemdata.nist.gov/dokuwiki/doku.php?id=peptidew:lib:phoshopept_labelfree_20190214
            "Name: DQELYFFHELSPGSCFFLPK/2_2(10,S,Phospho)(14,C,CAM)_57.2eV\r\n" +
            "MW: 2542.1268\r\n" +
            "Comment: Single Pep=Tryptic Mods=2(10,S,Phospho)(14,C,CAM) Fullname=R.DQELYFFHELSPGSCFFLPK.G Charge=2 Parent=1271.0634 Mz_diff=1.7ppm HCD=57.2203750610352eV Scan=34250 Origfile=\"20120208_EXQ5_KiSh_SA_LabelFree_HeLa_Phospho_Nocodazole_rep2_Fr2.raw.FT.hcd.ch.MGF\" Sample=\"KusterSyn_MOD\" Protein=\"sp|P26639|SYTC_HUMAN(pre=R,post=G)\" Unassign_all=0.2001 Unassigned=0.0000 max_unassigned_ab=0.07 num_unassigned_peaks=0/20 FTResolution=17500 ms2IsolationWidth=1.60 ms1PrecursorAb=25740807.69 Precursor1MaxAb=23835712.36 PrecursorMonoisoMZ=1271.0656 Filter=\"FTMS + p NSI d Full ms2 1271.56@hcd25.00 [100.00-2615.00]\"\r\n" +
            "Num peaks: 419\r\n" +
            "101.0714\t23777.1\t\"IQA/4.6ppm\"\r\n" +
            "101.1074\t18745.6\t\"IKA/0.8ppm\"\r\n" +
            "102.0555\t11336\t\"IEA/5.4ppm\"\r\n" +
            "103.0543\t5074.5\t\"?\"\r\n" +
            "110.0714\t35557.7\t\"IHA/1.2ppm\"\r\n" +
            "114.0553\t10133.9\t\"?\"\r\n" +
            "115.0869\t8382.8\t\"?\"\r\n" +
            "120.081\t107876\t\"IFA/1.9ppm\"\r\n" +
            "126.0549\t9238.7\t\"?\"\r\n" +
            "129.0655\t6541.2\t\"IQD/-2.7ppm\"\r\n" +
            "129.1024\t142378\t\"IKD/1.3ppm,y1-H2O/1.3ppm\"\r\n" +
            "130.0654\t5533.3\t\"?\"\r\n" +
            "130.078\t5379.7\t\"?\"\r\n" +
            "130.0861\t45495.8\t\"y1-NH3/-1.2ppm\"\r\n" +
            "133.0433\t25863.2\t\"?\"\r\n" +
            "133.0608\t26481.9\t\"?\"\r\n" +
            "136.0755\t42604.9\t\"IYA/-1.4ppm\"\r\n" +
            "138.0548\t24589.8\t\"?\"\r\n" +
            "143.1184\t5368.4\t\"?\"\r\n" +
            "147.1128\t147121\t\"y1/0.0ppm\"\r\n" +
            "155.0815\t21122.7\t\"Int/PG/0.0ppm\"\r\n" +
            "159.0917\t9135.5\t\"?\"\r\n" +
            "163.4219\t4345.3\t\"?\"\r\n" +
            "167.082\t9279.5\t\"?\"\r\n" +
            "169.0975\t6562.9\t\"?\"\r\n" +
            "175.1192\t18302.2\t\"IKF/1.4ppm\"\r\n" +
            "181.0981\t5139\t\"?\"\r\n" +
            "181.1328\t4376.3\t\"?\"\r\n" +
            "183.1494\t36800.5\t\"?\"\r\n" +
            "185.0913\t5627.2\t\"?\"\r\n" +
            "197.1287\t10557.6\t\"?\"\r\n" +
            "199.1089\t6271.6\t\"?\"\r\n" +
            "201.1241\t7215.9\t\"?\"\r\n" +
            "204.0873\t18236.5\t\"?\"\r\n" +
            "209.1284\t19132.6\t\"y2-H2O-NH3/-0.3ppm\"\r\n" +
            "211.1445\t40117.3\t\"Int/LP/1.9ppm\"\r\n" +
            "212.1044\t9898.8\t\"?\"\r\n" +
            "213.0872\t8306.6\t\"?\"\r\n" +
            "215.1391\t8208.2\t\"?\"\r\n" +
            "216.0979\t11426.8\t\"a2/0.1ppm\"\r\n" +
            "224.1031\t8337.9\t\"Int/PGS-H2O/0.6ppm\"\r\n" +
            "226.0816\t11366\t\"b2-H2O/-2.8ppm\"\r\n" +
            "226.1196\t11567.2\t\"?\"\r\n" +
            "226.155\t210767\t\"y2-H2O/0.0ppm\"\r\n" +
            "227.0659\t25492.8\t\"b2-NH3/-1.5ppm\"\r\n" +
            "227.103\t24897.6\t\"?\"\r\n" +
            "227.1388\t10581.9\t\"y2-NH3/-1.0ppm\"\r\n" +
            "228.1342\t6691.7\t\"?\"\r\n" +
            "242.1131\t5248.6\t\"Int/PGS/-1.8ppm\"\r\n" +
            "242.1502\t6863.4\t\"?\"\r\n" +
            "243.0972\t53822.9\t\"?\"\r\n" +
            "243.134\t9589.9\t\"Int/EL/0.3ppm\"\r\n" +
            "244.0934\t170799\t\"b2/2.5ppm\"\r\n" +
            "244.1656\t1188370\t\"y2/0.1ppm\"\r\n" +
            "245.097\t7733.5\t\"b2+i/5.8ppm\"\r\n" +
            "245.1689\t113460\t\"y2+i/1.8ppm\"\r\n" +
            "258.1083\t11450\t\"Int/QE/-0.6ppm\"\r\n" +
            "267.1072\t6270.4\t\"Int/HE/-5.9ppm\"\r\n" +
            "267.1482\t5503.9\t\"?\"\r\n" +
            "277.1546\t8452.7\t\"?\"\r\n" +
            "280.1097\t17301.2\t\"?\"\r\n" +
            "280.1655\t6798.2\t\"?\"\r\n" +
            "282.1801\t5210.6\t\"?\"\r\n" +
            "283.1427\t6707\t\"?\"\r\n" +
            "285.1346\t23741.5\t\"?\"\r\n" +
            "294.1825\t5736\t\"?\"\r\n" +
            "295.1425\t18257.4\t\"Int/FF/-5.4ppm\"\r\n" +
            "308.1048\t10596.9\t\"Int/cF/-5.0ppm\"\r\n" +
            "308.1951\t7471.1\t\"?\"\r\n" +
            "311.1373\t8053.5\t\"?\"\r\n" +
            "323.1694\t4994.5\t\"?\"\r\n" +
            "326.1735\t7698.8\t\"?\"\r\n" +
            "327.1314\t5733.4\t\"?\"\r\n" +
            "328.1866\t10471.3\t\"?\"\r\n" +
            "339.2019\t5455.3\t\"?\"\r\n" +
            "339.2409\t8177\t\"y3-H2O/5.4ppm\"\r\n" +
            "342.1302\t10228.2\t\"?\"\r\n" +
            "354.1695\t5784.6\t\"?\"\r\n" +
            "355.1953\t5495.8\t\"?\"\r\n" +
            "355.3674\t5154\t\"?\"\r\n" +
            "356.1087\t41477.7\t\"b3-NH3/-0.4ppm\"\r\n" +
            "356.1822\t45545.8\t\"?\"\r\n" +
            "357.1802\t12008.5\t\"?\"\r\n" +
            "357.2496\t240801\t\"y3/-0.1ppm\"\r\n" +
            "358.2545\t28333.2\t\"y3+i/5.3ppm\"\r\n" +
            "359.1752\t6596.3\t\"?\"\r\n" +
            "371.1898\t23029.5\t\"?\"\r\n" +
            "373.1355\t299419\t\"b3/0.3ppm\"\r\n" +
            "374.1384\t39851.3\t\"b3+i/0.4ppm\"\r\n" +
            "380.1955\t7027\t\"?\"\r\n" +
            "384.1338\t6032.4\t\"?\"\r\n" +
            "395.1411\t7727.8\t\"?\"\r\n" +
            "398.2006\t5130.2\t\"?\"\r\n" +
            "402.1443\t12016.6\t\"Int/PGSc/0.3ppm\"\r\n" +
            "406.2009\t24085.2\t\"?\"\r\n" +
            "410.1609\t4818.4\t\"?\"\r\n" +
            "411.2252\t8400.8\t\"?\"\r\n" +
            "414.1812\t11973.8\t\"?\"\r\n" +
            "424.2256\t5794.6\t\"?\"\r\n" +
            "425.2025\t6172.8\t\"?\"\r\n" +
            "426.1617\t6132.7\t\"?\"\r\n" +
            "432.2014\t18831.1\t\"?\"\r\n" +
            "441.1973\t4975.2\t\"?\"\r\n" +
            "449.2152\t23646.7\t\"?\"\r\n" +
            "455.1701\t40381.8\t\"?\"\r\n" +
            "456.179\t4879\t\"?\"\r\n" +
            "458.2253\t23041.1\t\"a4/1.7ppm\"\r\n" +
            "468.209\t11115.2\t\"b4-H2O/0.2ppm\"\r\n" +
            "469.1964\t11703.3\t\"b4-NH3/7.5ppm\"\r\n" +
            "470.1961\t7779.4\t\"b4-NH3+i/0.4ppm\"\r\n" +
            "475.1801\t9544.3\t\"?\"\r\n" +
            "476.1861\t6939\t\"?\"\r\n" +
            "486.2202\t217416\t\"b4/1.5ppm\"\r\n" +
            "487.2221\t32124.6\t\"b4+i/-0.6ppm\"\r\n" +
            "491.1769\t7486.9\t\"?\"\r\n" +
            "503.2174\t6444.7\t\"?\"\r\n" +
            "504.3185\t70032.7\t\"y4/0.9ppm\"\r\n" +
            "505.3181\t10720.1\t\"y4+i/-5.9ppm\"\r\n" +
            "514.2085\t9133.2\t\"?\"\r\n" +
            "517.2274\t6550.4\t\"?\"\r\n" +
            "521.2183\t8883.9\t\"Int/PGScF-CO/1.2ppm\"\r\n" +
            "527.2596\t10015\t\"?\"\r\n" +
            "531.2\t7104.4\t\"?\"\r\n" +
            "536.1995\t11413.6\t\"?\"\r\n" +
            "537.2\t5934\t\"?\"\r\n" +
            "540.268\t8662.1\t\"?\"\r\n" +
            "544.2466\t6073.4\t\"?\"\r\n" +
            "547.2501\t5944.5\t\"?\"\r\n" +
            "560.259\t48320.1\t\"?\"\r\n" +
            "561.2541\t30526.4\t\"?\"\r\n" +
            "562.2485\t8888.8\t\"?\"\r\n" +
            "572.2435\t5143.7\t\"?\"\r\n" +
            "573.2247\t7332.7\t\"?\"\r\n" +
            "588.2701\t33263.1\t\"?\"\r\n" +
            "596.285\t27044\t\"?\"\r\n" +
            "605.2663\t6440\t\"b9^2/3.5ppm\"\r\n" +
            "621.2884\t26336.8\t\"a5/0.9ppm\"\r\n" +
            "622.2962\t7953.3\t\"a5+i/8.6ppm\"\r\n" +
            "631.2766\t21944.8\t\"b5-H2O/6.9ppm\"\r\n" +
            "632.2551\t18826.8\t\"b5-NH3/-1.8ppm\"\r\n" +
            "633.2864\t10686.5\t\"?\"\r\n" +
            "649.2835\t101965\t\"b5/1.1ppm\"\r\n" +
            "650.2859\t32175.1\t\"b5+i/0.2ppm\"\r\n" +
            "651.2965\t6717.8\t\"b5+2i/12.6ppm\"\r\n" +
            "651.3934\t39190.2\t\"y5/10.7ppm\"\r\n" +
            "652.3925\t17945.7\t\"y5+i/4.6ppm\"\r\n" +
            "654.3284\t5777.7\t\"?\"\r\n" +
            "668.2905\t25244.3\t\"Int/PGScFF-CO/6.6ppm\"\r\n" +
            "669.3008\t6594\t\"Int/PGScFF-CO+i/17.6ppm\"\r\n" +
            "678.2732\t8154.8\t\"?\"\r\n" +
            "679.2853\t5754\t\"?\"\r\n" +
            "683.2726\t8199.2\t\"?\"\r\n" +
            "696.2885\t27801.2\t\"Int/PGScFF/10.8ppm\"\r\n" +
            "708.3497\t5845.1\t\"?\"\r\n" +
            "720.2949\t32962.8\t\"?\"\r\n" +
            "721.2942\t11313.9\t\"?\"\r\n" +
            "728.3214\t6148.5\t\"?\"\r\n" +
            "742.3582\t5570.7\t\"?\"\r\n" +
            "743.3555\t25677.3\t\"?\"\r\n" +
            "750.871\t22639.4\t\"y13-H3PO4^2/2.9ppm\"\r\n" +
            "751.3425\t22252.6\t\"?\"\r\n" +
            "762.3659\t6135.6\t\"?\"\r\n" +
            "765.3044\t10241.8\t\"?\"\r\n" +
            "768.3569\t21646.7\t\"a6/0.8ppm\"\r\n" +
            "773.3472\t5171.8\t\"b13-H3PO4^2/-8.9ppm\"\r\n" +
            "778.3356\t17572.1\t\"b6-H2O/-6.5ppm\"\r\n" +
            "779.3405\t21448.9\t\"b6-H2O+i/-4.1ppm\"\r\n" +
            "780.3499\t10784.4\t\"b6-H2O+2i/4.5ppm\"\r\n" +
            "781.3708\t31392.2\t\"Int/PGScFFL-CO/0.8ppm\"\r\n" +
            "782.3751\t5863.5\t\"Int/PGScFFL-CO+i/2.6ppm\"\r\n" +
            "791.355\t6736.5\t\"y13-NH3^2/13.9ppm\"\r\n" +
            "792.3603\t11147\t\"?\"\r\n" +
            "796.3513\t92225.6\t\"b6/0.1ppm\"\r\n" +
            "797.3536\t34598.6\t\"b6+i/-0.8ppm\"\r\n" +
            "799.8602\t21781.6\t\"y13^2/3.6ppm\"\r\n" +
            "800.3635\t8561.4\t\"y13^2+i/6.0ppm\"\r\n" +
            "809.3671\t83081.3\t\"Int/PGScFFL/2.5ppm\"\r\n" +
            "810.3707\t29892.9\t\"Int/PGScFFL+i/3.3ppm\"\r\n" +
            "811.4193\t57929.2\t\"y6/2.7ppm\"\r\n" +
            "812.4242\t9669.2\t\"y6+i/5.1ppm\"\r\n" +
            "813.419\t8200.4\t\"y6+2i/-1.5ppm\"\r\n" +
            "816.8699\t20067.4\t\"b14-H3PO4^2/-0.2ppm\"\r\n" +
            "817.3749\t7520.9\t\"b14-H3PO4^2+i/4.1ppm\"\r\n" +
            "824.4094\t7557.1\t\"y14-H3PO4^2/7.7ppm\"\r\n" +
            "824.9045\t7622.9\t\"y14-H3PO4^2+i/0.0ppm\"\r\n" +
            "825.397\t10049.6\t\"y14-H3PO4^2+2i/-10.3ppm\"\r\n" +
            "833.3736\t18778.3\t\"?\"\r\n" +
            "837.3941\t11604.1\t\"?\"\r\n" +
            "838.3861\t8864.9\t\"?\"\r\n" +
            "842.3762\t23791.9\t\"?\"\r\n" +
            "851.3848\t8073\t\"?\"\r\n" +
            "851.8807\t7757.4\t\"?\"\r\n" +
            "852.3936\t6879.3\t\"?\"\r\n" +
            "857.3478\t7192.4\t\"b14-NH3^2/3.0ppm\"\r\n" +
            "860.3748\t8529.8\t\"?\"\r\n" +
            "873.3966\t9877\t\"y14^2/5.9ppm\"\r\n" +
            "874.3835\t8799\t\"?\"\r\n" +
            "878.3816\t35045.1\t\"y14-H2O+CO^2/-2.4ppm\"\r\n" +
            "879.3934\t5761.9\t\"?\"\r\n" +
            "883.3977\t10582.8\t\"?\"\r\n" +
            "887.8852\t24026.8\t\"?\"\r\n" +
            "888.3824\t32078.1\t\"?\"\r\n" +
            "888.8885\t6790.4\t\"?\"\r\n" +
            "889.4063\t10832.6\t\"?\"\r\n" +
            "891.8692\t6650\t\"?\"\r\n" +
            "896.8893\t21085.8\t\"b15-H3PO4^2/4.3ppm\"\r\n" +
            "897.3875\t41469.9\t\"b15-H3PO4^2+i/0.7ppm\"\r\n" +
            "897.9397\t37005.2\t\"y15-H3PO4^2/2.7ppm\"\r\n" +
            "898.4457\t84541.2\t\"y7/-3.8ppm,y15-H3PO4^2+i/7.8ppm\"\r\n" +
            "898.9418\t11260.3\t\"y15-H3PO4^2+2i/2.2ppm\"\r\n" +
            "899.4433\t21689.5\t\"y15-H3PO4^2+3i/2.9ppm,y7+i/-9.8ppm\"\r\n" +
            "900.444\t7704.1\t\"y7+2i/-9.5ppm\"\r\n" +
            "906.4155\t22287.5\t\"Int/PGScFFLP/-2.6ppm\"\r\n" +
            "907.4037\t18906.3\t\"Int/PGScFFLP+i/-18.8ppm\"\r\n" +
            "915.4265\t8935.4\t\"a7/2.0ppm\"\r\n" +
            "924.4162\t6847.7\t\"?\"\r\n" +
            "924.9228\t10225.1\t\"?\"\r\n" +
            "925.4178\t48001.5\t\"b7-H2O/9.5ppm\"\r\n" +
            "926.4042\t19470\t\"b7-NH3/12.0ppm,b7-H2O+i/-8.5ppm\"\r\n" +
            "938.3917\t6222.6\t\"?\"\r\n" +
            "942.3793\t5718\t\"?\"\r\n" +
            "943.4207\t53355.8\t\"b7/1.2ppm\"\r\n" +
            "944.429\t30687.2\t\"b7+i/6.7ppm\"\r\n" +
            "945.4325\t9037\t\"b7+2i/7.5ppm\"\r\n" +
            "946.921\t7359.2\t\"y15^2/-5.0ppm\"\r\n" +
            "947.4327\t29880.6\t\"y15^2+i/5.8ppm\"\r\n" +
            "947.9277\t19926.4\t\"y15^2+2i/-0.6ppm\"\r\n" +
            "955.477\t39368\t\"y8/6.7ppm\"\r\n" +
            "956.4255\t33792.7\t\"?\"\r\n" +
            "956.927\t25963.7\t\"?\"\r\n" +
            "957.4073\t17961\t\"?\"\r\n" +
            "961.9133\t21112.1\t\"?\"\r\n" +
            "963.4299\t7132.4\t\"?\"\r\n" +
            "965.4313\t5953.7\t\"?\"\r\n" +
            "966.426\t6768.3\t\"?\"\r\n" +
            "970.4231\t65339.5\t\"b16-H3PO4^2/3.6ppm\"\r\n" +
            "970.9201\t62307\t\"b16-H3PO4^2+i/-1.0ppm\"\r\n" +
            "971.4241\t29763.4\t\"b16-H3PO4^2+2i/2.0ppm\"\r\n" +
            "979.4595\t25636.5\t\"y16-H3PO4^2/-9.6ppm\"\r\n" +
            "979.9686\t35658.3\t\"y16-H3PO4^2+i/-1.8ppm\"\r\n" +
            "980.468\t29901.5\t\"y16-H3PO4^2+2i/-3.6ppm\"\r\n" +
            "991.4714\t27034.3\t\"?\"\r\n" +
            "1007.4467\t6684.4\t\"?\"\r\n" +
            "1019.4656\t23762.1\t\"y16-H2O^2/13.3ppm\"\r\n" +
            "1028.4578\t38762.4\t\"y16^2/0.4ppm\"\r\n" +
            "1028.9524\t18798\t\"y16^2+i/-6.3ppm\"\r\n" +
            "1029.4631\t8968.4\t\"y16^2+2i/3.0ppm\"\r\n" +
            "1029.9631\t24230.5\t\"y16^2+3i/2.1ppm\"\r\n" +
            "1030.4661\t25246.1\t\"y16^2+4i/4.1ppm\"\r\n" +
            "1030.9618\t7992.9\t\"y16^2+5i/-1.0ppm\"\r\n" +
            "1034.499\t22973.6\t\"y9-H2O/-13.3ppm\"\r\n" +
            "1035.4633\t20489.2\t\"?\"\r\n" +
            "1035.9891\t21842.4\t\"?\"\r\n" +
            "1036.5048\t30915.2\t\"?\"\r\n" +
            "1037.0143\t10368\t\"?\"\r\n" +
            "1043.9563\t52826.4\t\"b17-H3PO4^2/2.4ppm\"\r\n" +
            "1044.4495\t61430.2\t\"b17-H3PO4^2+i/-5.6ppm\"\r\n" +
            "1044.942\t38048.1\t\"b17-H3PO4^2+2i/-13.8ppm\"\r\n" +
            "1045.469\t6621.3\t\"b17-H3PO4^2+3i/11.1ppm\"\r\n" +
            "1052.522\t433858\t\"y9/-1.3ppm\"\r\n" +
            "1053.5247\t225638\t\"y9+i/-1.5ppm\"\r\n" +
            "1054.5277\t66541.1\t\"y9+2i/0.4ppm\"\r\n" +
            "1062.4661\t9895\t\"b8-H2O/-1.7ppm\"\r\n" +
            "1063.4635\t11133.9\t\"b8-NH3/10.8ppm,b8-H2O+i/-7.0ppm\"\r\n" +
            "1080.4797\t107285\t\"b8/1.1ppm\"\r\n" +
            "1081.4818\t60167.8\t\"b8+i/0.3ppm\"\r\n" +
            "1082.4841\t20805.2\t\"b8+2i/-0.2ppm\"\r\n" +
            "1085.0037\t10084.2\t\"y17^2/4.0ppm\"\r\n" +
            "1085.5026\t21213\t\"y17^2+i/1.6ppm\"\r\n" +
            "1092.0002\t19482.2\t\"?\"\r\n" +
            "1092.5093\t10143.1\t\"?\"\r\n" +
            "1093.4911\t21668.3\t\"?\"\r\n" +
            "1094.5037\t11261.5\t\"?\"\r\n" +
            "1100.5105\t51443.5\t\"b18-H3PO4^2/13.3ppm,y18-H3PO4^2/-19.8ppm\"\r\n" +
            "1101.01\t46000.3\t\"b18-H3PO4^2+i/11.5ppm\"\r\n" +
            "1101.5226\t31887.7\t\"?\"\r\n" +
            "1104.5292\t8061.8\t\"?\"\r\n" +
            "1120.5262\t22669.1\t\"?\"\r\n" +
            "1121.5452\t69616.7\t\"y10-H3PO4/0.3ppm\"\r\n" +
            "1122.5552\t35054.2\t\"y10-H3PO4+i/6.6ppm\"\r\n" +
            "1123.5439\t7479.3\t\"y10-H3PO4+2i/-4.4ppm\"\r\n" +
            "1131.5007\t7807.2\t\"?\"\r\n" +
            "1133.5382\t7566.3\t\"?\"\r\n" +
            "1137.4999\t16357.3\t\"?\"\r\n" +
            "1140.5359\t10765.5\t\"y18-H2O^2/18.0ppm\"\r\n" +
            "1144.4928\t10424.7\t\"?\"\r\n" +
            "1145.5038\t7555.6\t\"?\"\r\n" +
            "1149.5289\t27689.2\t\"y18^2/7.1ppm\"\r\n" +
            "1150.5342\t8487.8\t\"?\"\r\n" +
            "1156.0553\t23817.4\t\"?\"\r\n" +
            "1156.5444\t20746.2\t\"?\"\r\n" +
            "1162.5197\t8088.9\t\"?\"\r\n" +
            "1163.5024\t10070.1\t\"?\"\r\n" +
            "1164.5452\t28418.6\t\"y19-H3PO4^2/-14.0ppm\"\r\n" +
            "1165.0612\t31130.8\t\"y19-H3PO4^2+i/-1.6ppm\"\r\n" +
            "1165.551\t26168.1\t\"y19-H3PO4^2+2i/-11.3ppm\"\r\n" +
            "1169.538\t7665.5\t\"?\"\r\n" +
            "1170.5474\t9556.5\t\"?\"\r\n" +
            "1176.5698\t8507.3\t\"?\"\r\n" +
            "1178.5728\t6939.6\t\"?\"\r\n" +
            "1179.0731\t8137.1\t\"?\"\r\n" +
            "1181.5499\t37250.7\t\"?\"\r\n" +
            "1182.5477\t24752.5\t\"?\"\r\n" +
            "1191.552\t7575.5\t\"?\"\r\n" +
            "1200.5116\t7220.5\t\"?\"\r\n" +
            "1201.5305\t8766.6\t\"y10-H2O/16.1ppm\"\r\n" +
            "1204.5602\t19946.6\t\"y19-H2O^2/12.9ppm\"\r\n" +
            "1205.0376\t24227.8\t\"y19-NH3^2/0.7ppm,y19-H2O^2+i/-7.1ppm\"\r\n" +
            "1205.5479\t30736.9\t\"y19-H2O^2+2i/0.4ppm,y19-NH3^2+i/8.0ppm\"\r\n" +
            "1209.52\t169011\t\"b9/-0.9ppm\"\r\n" +
            "1210.5199\t95570.3\t\"b9+i/-3.5ppm\"\r\n" +
            "1211.5227\t19465.3\t\"b9+2i/-3.4ppm\"\r\n" +
            "1213.0771\t25236.4\t\"?\"\r\n" +
            "1213.5724\t107331\t\"y19^2/18.5ppm\"\r\n" +
            "1214.0599\t87035.2\t\"y19^2+i/6.9ppm\"\r\n" +
            "1214.5554\t23287\t\"y19^2+2i/2.3ppm\"\r\n" +
            "1217.5929\t6530.6\t\"?\"\r\n" +
            "1218.5558\t9963.1\t\"y19-H2O+CO^2/11.2ppm\"\r\n" +
            "1219.5255\t55631.2\t\"y10/3.1ppm\"\r\n" +
            "1220.5234\t37983.4\t\"y10+i/-1.0ppm\"\r\n" +
            "1222.0847\t517770\t\"p-H3PO4/7.9ppm\"\r\n" +
            "1222.5771\t918635\t\"p-H3PO4+i/0.5ppm\"\r\n" +
            "1223.0706\t572054\t\"p-H3PO4+2i/-5.8ppm\"\r\n" +
            "1223.5763\t49062\t\"p-H3PO4+3i/-2.0ppm\"\r\n" +
            "1231.0845\t55964.2\t\"?\"\r\n" +
            "1231.58\t81120.6\t\"?\"\r\n" +
            "1232.075\t66617.9\t\"?\"\r\n" +
            "1232.5739\t8116\t\"?\"\r\n" +
            "1234.6359\t77982.5\t\"y11-H3PO4/5.7ppm\"\r\n" +
            "1235.6451\t54773.9\t\"y11-H3PO4+i/10.7ppm\"\r\n" +
            "1236.6317\t10678.1\t\"y11-H3PO4+2i/-1.2ppm\"\r\n" +
            "1252.6284\t8050.3\t\"?\"\r\n" +
            "1253.5887\t10490\t\"?\"\r\n" +
            "1257.5763\t21297.8\t\"?\"\r\n" +
            "1259.5837\t10807.6\t\"?\"\r\n" +
            "1262.0758\t42087.7\t\"p-H2O/14.0ppm\"\r\n" +
            "1262.5575\t89750.3\t\"p-NH3/5.8ppm,p-H2O+i/-1.7ppm\"\r\n" +
            "1263.0543\t57018.2\t\"p-H2O+2i/-5.2ppm,p-NH3+i/2.1ppm\"\r\n" +
            "1263.5527\t24951.5\t\"p-H2O+3i/-7.3ppm,p-NH3+2i/-0.1ppm\"\r\n" +
            "1271.0743\t311140\t\"p/8.5ppm\"\r\n" +
            "1271.5635\t565653\t\"p+i/-1.1ppm\"\r\n" +
            "1272.0565\t314912\t\"p+2i/-7.6ppm\"\r\n" +
            "1272.5231\t40199\t\"?\"\r\n" +
            "1294.6285\t8331.9\t\"a10/14.1ppm\"\r\n" +
            "1295.6317\t10416\t\"a10+i/14.2ppm\"\r\n" +
            "1304.5736\t19329.6\t\"b10-H2O/-16.1ppm\"\r\n" +
            "1305.592\t8637.5\t\"b10-NH3/10.2ppm,b10-H2O+i/-4.3ppm\"\r\n" +
            "1322.6068\t124083\t\"b10/1.2ppm\"\r\n" +
            "1323.6143\t91537.5\t\"b10+i/4.6ppm\"\r\n" +
            "1324.6133\t27718.7\t\"b10+2i/1.8ppm\"\r\n" +
            "1332.611\t37088.4\t\"y11/3.9ppm\"\r\n" +
            "1333.6256\t30182.8\t\"y11+i/12.7ppm\"\r\n" +
            "1355.5547\t11511.5\t\"Int/HELsPGScFFL/4.2ppm\"\r\n" +
            "1363.6685\t68896.1\t\"y12-H3PO4/-2.2ppm\"\r\n" +
            "1364.6669\t37726.9\t\"y12-H3PO4+i/-5.5ppm\"\r\n" +
            "1373.6368\t27671.6\t\"?\"\r\n" +
            "1374.6073\t29721.5\t\"?\"\r\n" +
            "1391.6289\t367404\t\"b11-H3PO4/1.6ppm\"\r\n" +
            "1392.6304\t270720\t\"b11-H3PO4+i/0.6ppm\"\r\n" +
            "1393.6274\t82733.7\t\"b11-H3PO4+2i/-3.6ppm\"\r\n" +
            "1404.6639\t23776.1\t\"?\"\r\n" +
            "1405.6503\t18779.3\t\"?\"\r\n" +
            "1409.6224\t11523.7\t\"?\"\r\n" +
            "1443.6327\t29740.8\t\"y12-H2O/-3.5ppm\"\r\n" +
            "1444.6433\t21221.6\t\"y12-NH3/14.9ppm,y12-H2O+i/1.8ppm\"\r\n" +
            "1461.6613\t28951.3\t\"y12/8.8ppm\"\r\n" +
            "1462.6515\t19639.8\t\"y12+i/0.1ppm\"\r\n" +
            "1489.6506\t23188.9\t\"?\"\r\n" +
            "1500.7289\t107446\t\"y13-H3PO4/-1.0ppm\"\r\n" +
            "1501.7428\t81058.5\t\"y13-H3PO4+i/6.3ppm\"\r\n" +
            "1502.7074\t35149.9\t\"y13-H3PO4+2i/-18.4ppm\"\r\n" +
            "1545.7194\t10661.4\t\"b13-H3PO4/12.0ppm\"\r\n" +
            "1546.7029\t18746.3\t\"b13-H3PO4+i/-0.6ppm\"\r\n" +
            "1598.7202\t61829.5\t\"y13/8.1ppm\"\r\n" +
            "1599.7059\t46460\t\"y13+i/-2.7ppm\"\r\n" +
            "1600.7031\t20906.8\t\"y13+2i/-5.5ppm\"\r\n" +
            "1633.7676\t18701.6\t\"?\"\r\n" +
            "1644.6777\t9187.1\t\"?\"\r\n" +
            "1647.8059\t50276.7\t\"y14-H3PO4/4.3ppm\"\r\n" +
            "1648.8105\t42623.8\t\"y14-H3PO4+i/5.3ppm\"\r\n" +
            "1649.782\t31715.9\t\"y14-H3PO4+2i/-13.1ppm\"\r\n" +
            "1666.8002\t10448.9\t\"?\"\r\n" +
            "1712.7043\t20683.3\t\"b14-H2O/3.0ppm\"\r\n" +
            "1713.7008\t28878.3\t\"b14-NH3/10.2ppm,b14-H2O+i/-0.8ppm\"\r\n" +
            "1714.7452\t19416.2\t\"?\"\r\n" +
            "1715.7915\t8639.8\t\"?\"\r\n" +
            "1745.7642\t32944.2\t\"y14/-6.6ppm\"\r\n" +
            "1746.7966\t45277.2\t\"y14+i/10.3ppm\"\r\n" +
            "1747.7634\t9720\t\"y14+2i/-9.8ppm\"\r\n" +
            "1792.7811\t8855.7\t\"b15-H3PO4/9.8ppm\"\r\n" +
            "1794.8777\t45792.3\t\"y15-H3PO4/5.8ppm\"\r\n" +
            "1795.8743\t41776.1\t\"y15-H3PO4+i/2.3ppm\"\r\n" +
            "1796.8806\t9777.6\t\"y15-H3PO4+2i/4.6ppm\"\r\n" +
            "1812.8123\t9855\t\"?\"\r\n" +
            "1875.8801\t9513.4\t\"?\"\r\n" +
            "1892.8807\t39559.9\t\"y15/19.3ppm\"\r\n" +
            "1893.8363\t29168.1\t\"y15+i/-5.7ppm\"\r\n" +
            "1939.8417\t20847.8\t\"b16-H3PO4/5.0ppm\"\r\n" +
            "1940.8715\t22876.1\t\"b16-H3PO4+i/18.9ppm\"\r\n" +
            "1957.9188\t37125.2\t\"y16-H3PO4/-6.0ppm\"\r\n" +
            "1958.9116\t28616.8\t\"y16-H3PO4+i/-11.2ppm\"\r\n" +
            "2086.9258\t34646.4\t\"b17-H3PO4/12.2ppm\"\r\n" +
            "2087.9285\t44508.1\t\"b17-H3PO4+i/12.1ppm\"\r\n" +
            "2172.9775\t29916\t\"?\"\r\n" +
            "2173.9949\t10718.7\t\"?\"\r\n" +
            "2182.9971\t15647.9\t\"?\"\r\n" +
            "2184.8613\t30691.2\t\"b17/-7.3ppm\"\r\n" +
            "2185.8931\t41300.1\t\"b17+i/5.9ppm\"\r\n" +
            "2199.9724\t116351\t\"b18-H3PO4/-5.5ppm\"\r\n" +
            "2200.9846\t121177\t\"b18-H3PO4+i/-1.3ppm\"\r\n" +
            "2201.9985\t57479.1\t\"b18-H3PO4+2i/4.0ppm\"\r\n" +
            "2202.9756\t17280.6\t\"b18-H3PO4+3i/-7.4ppm\"\r\n" +
            "2269.9438\t10809.3\t\"a18/-10.0ppm\"\r\n" +
            "2270.9419\t19094.7\t\"a18+i/-12.1ppm\"\r\n" +
            "2279.968\t16840.2\t\"b18-H2O/7.6ppm\"\r\n" +
            "2280.9294\t19768.7\t\"b18-NH3/-2.4ppm,b18-H2O+i/-10.7ppm\"\r\n" +
            "2297.9775\t75343.1\t\"b18/7.0ppm\"\r\n" +
            "2298.96\t103185\t\"b18+i/-1.9ppm\"\r\n" +
            "2299.9441\t65307.3\t\"b18+2i/-9.8ppm\"\r\n" +
            "\r\n";

        private const string TEXT_USING_EXACTMASS_COMMENT = // As in MoNA-export-GNPS.msp
            "Name: \"1,1-dioxo-6-(trifluoromethyl)-3,4-dihydro-2H-1$l^{6},2,4-benzothiadiazine-7-sulfonamide\"\r\n" +
            "Synon: $:00in-source\r\n" +
            "DB#: CCMSLIB00000076973\r\n" +
            "Spectrum_type: 2\r\n" +
            "Instrument: qTof\r\n" +
            "Comments: \"SMILES=NS(=O)(=O)c1cc2c(NCNS2(=O)=O)cc1C(F)(F)F\" \"ion source=LC-ESI\" \"compound source=Commercial\" \"adduct=[M+H]+\" \"exactmass=330.991\" \"charge=1\" \"ion mode=Positive\" \"source file=NCP002928_A2_B11_BA2_01_15696.mzXML\" \"origin=GNPS-NIH-CLINICALCOLLECTION2\" \"authors=Garg_Neha, negarg, Dorrestein\" \"submitter=GNPS Team (University of California, San Diego)\"\r\n" +
            "Num Peaks: 92\r\n" +
            "45.035854 144.000000\r\n" +
            "65.534843 220.000000\r\n" +
            "84.186409 148.000000\r\n" +
            "85.804817 176.000000\r\n" +
            "116.466408 168.000000\r\n" +
            "126.015266 188.000000\r\n" +
            "140.034027 168.000000\r\n" +
            "147.003082 168.000000\r\n" +
            "147.994156 148.000000\r\n" +
            "148.045547 160.000000\r\n" +
            "151.457245 156.000000\r\n" +
            "158.018539 128.000000\r\n" +
            "159.025208 312.000000\r\n" +
            "159.045288 160.000000\r\n" +
            "160.035797 168.000000\r\n" +
            "163.002869 188.000000\r\n" +
            "163.048615 180.000000\r\n" +
            "167.035721 176.000000\r\n" +
            "168.034363 176.000000\r\n" +
            "169.987930 180.000000\r\n" +
            "170.006088 196.000000\r\n" +
            "172.031082 200.000000\r\n" +
            "173.037598 220.000000\r\n" +
            "174.016815 776.000000\r\n" +
            "175.015503 140.000000\r\n" +
            "176.029800 152.000000\r\n" +
            "181.015976 172.000000\r\n" +
            "182.682724 200.000000\r\n" +
            "183.460999 176.000000\r\n" +
            "185.122681 192.000000\r\n" +
            "186.036453 880.000000\r\n" +
            "187.047455 3112.000000\r\n" +
            "188.032974 220.000000\r\n" +
            "188.052200 172.000000\r\n" +
            "189.231659 164.000000\r\n" +
            "191.040237 236.000000\r\n" +
            "198.991821 172.000000\r\n" +
            "199.081680 172.000000\r\n" +
            "201.057251 140.000000\r\n" +
            "207.988678 140.000000\r\n" +
            "214.981384 184.000000\r\n" +
            "218.024139 160.000000\r\n" +
            "218.950851 152.000000\r\n" +
            "219.009552 164.000000\r\n" +
            "219.398743 180.000000\r\n" +
            "223.093369 164.000000\r\n" +
            "237.978195 196.000000\r\n" +
            "239.003174 308.000000\r\n" +
            "240.025101 204.000000\r\n" +
            "240.996201 156.000000\r\n" +
            "251.009552 784.000000\r\n" +
            "251.196213 148.000000\r\n" +
            "253.003830 144.000000\r\n" +
            "253.494766 148.000000\r\n" +
            "255.002914 128.000000\r\n" +
            "256.495026 148.000000\r\n" +
            "267.003876 1340.000000\r\n" +
            "268.008209 220.000000\r\n" +
            "268.995422 116.000000\r\n" +
            "269.596619 152.000000\r\n" +
            "270.024902 184.000000\r\n" +
            "271.974396 172.000000\r\n" +
            "274.949738 148.000000\r\n" +
            "275.851471 180.000000\r\n" +
            "282.998962 280.000000\r\n" +
            "285.017914 172.000000\r\n" +
            "289.070160 164.000000\r\n" +
            "289.940491 216.000000\r\n" +
            "298.980682 172.000000\r\n" +
            "302.966553 180.000000\r\n" +
            "308.495148 148.000000\r\n" +
            "310.206970 244.000000\r\n" +
            "311.990326 144.000000\r\n" +
            "313.038849 144.000000\r\n" +
            "314.972351 6400.000000\r\n" +
            "315.119934 192.000000\r\n" +
            "315.971069 944.000000\r\n" +
            "316.968140 648.000000\r\n" +
            "320.982697 924.000000\r\n" +
            "330.991455 152.000000\r\n" +
            "331.161652 164.000000\r\n" +
            "331.875549 148.000000\r\n" +
            "331.958954 164.000000\r\n" +
            "331.997070 412.000000\r\n" +
            "332.220093 156.000000\r\n" +
            "332.984955 320.000000\r\n" +
            "333.203949 144.000000\r\n" +
            "333.989166 200.000000\r\n" +
            "334.281769 216.000000\r\n" +
            "335.146851 236.000000\r\n" +
            "346.204071 164.000000\r\n" +
            "426.735748 152.000000\r\n" +
            "\r\n";

        private const string TEXT_USING_EXACTMASS_ARG = // As in https://chemdata.nist.gov/dokuwiki/doku.php?id=peptidew:lib:newigghsalib  IgG_Glycopeptide _HCD_V1.msp 
            "Name: TKPREEQYNSTYR_G33100/4/E12\r\n" +
            "Comment: ID_Score=94.08 Source=NS0,CHO StartMZ=Auto Collision_energy=14.01 NCE=12.0 Inst=QExactive Scan=12646 Charge=4 RT=32.1578 ObservedPrecursorMZ=729.0703 Mz_exact=729.0712 Mz_diff=-0.0009 Dev_ppm=1.26 BasePeak=124585 max2med=23.9 Unassign_all=0.030 Unassigned=0.016 Run2rep=16/84 Run=2014-08-05_24_IgG8670__2D_Fract12_GuanRT_18h_QE_ms10msms_HCD12_Res17k_5ul\r\n" +
            "ExactMass=2912.256\r\n" +
            "Num peaks: 57\r\n" +
            "204.0864\t6931\t\"HexNAc/Dev=1.3\"\r\n" +
            "205.0897\t241\t\"HexNAci\"\r\n" +
            "325.9717\t117\t\"\"\r\n" +
            "338.1698\t130\t\"\"\r\n" +
            "366.1383\t418\t\"HexNAcHex/Dev=3.2\"\r\n" +
            "674.9804\t138\t\"\"\r\n" +
            "729.0735\t135\t\"\"\r\n" +
            "742.3519\t142\t\"\"\r\n" +
            "747.3410\t216\t\"P_G21000^3/Dev=5.2\"\r\n" +
            "747.6796\t574\t\"P_G21000i^3\"\r\n" +
            "796.0261\t781\t\"P_G21100^3/Dev=6.1\"\r\n" +
            "796.3538\t774\t\"P_G21100i^3\"\r\n" +
            "796.6937\t766\t\"P_G21100i^3\"\r\n" +
            "801.3654\t760\t\"P_G22000^3/Dev=3.7\"\r\n" +
            "801.6998\t1083\t\"P_G22000i^3\"\r\n" +
            "802.0424\t399\t\"P_G22000i^3\"\r\n" +
            "850.0485\t3199\t\"P_G22100^3/Dev=0.0\"\r\n" +
            "850.3824\t5262\t\"P_G22100i^3\"\r\n" +
            "850.7149\t3893\t\"P_G22100i^3\"\r\n" +
            "851.0498\t2954\t\"P_G22100i^3\"\r\n" +
            "851.3830\t902\t\"P_G22100i^3\"\r\n" +
            "851.7251\t179\t\"P_G22100i^3\"\r\n" +
            "855.3832\t570\t\"P_G23000^3/Dev=3.6\"\r\n" +
            "855.7093\t1025\t\"P_G23000i^3\"\r\n" +
            "856.0436\t1022\t\"P_G23000i^3\"\r\n" +
            "856.3839\t168\t\"P_G23000i^3\"\r\n" +
            "856.7167\t139\t\"P_G23000i^3\"\r\n" +
            "903.9282\t152\t\"\"\r\n" +
            "904.0674\t7232\t\"P_G23100^3/Dev=1.4\"\r\n" +
            "904.3995\t10000\t\"P_G23100i^3\"\r\n" +
            "904.7333\t6866\t\"P_G23100i^3\"\r\n" +
            "905.0663\t4541\t\"P_G23100i^3\"\r\n" +
            "905.4029\t1594\t\"P_G23100i^3\"\r\n" +
            "905.7382\t446\t\"P_G23100i^3\"\r\n" +
            "906.0609\t130\t\"P_G23100i^3\"\r\n" +
            "919.0640\t234\t\"\"\r\n" +
            "937.9464\t656\t\"P_G10000^2/Dev=1.4\"\r\n" +
            "938.4535\t627\t\"P_G10000i^2\"\r\n" +
            "1010.9859\t1044\t\"P_G10100^2/Dev=9.2\"\r\n" +
            "1011.4749\t1038\t\"P_G10100i^2\"\r\n" +
            "1011.9875\t627\t\"P_G10100i^2\"\r\n" +
            "1012.4742\t174\t\"P_G10100i^2\"\r\n" +
            "1012.9798\t170\t\"P_G10100i^2\"\r\n" +
            "1112.5083\t195\t\"P_G20100^2/Dev=7.1\"\r\n" +
            "1113.0183\t402\t\"P_G20100i^2\"\r\n" +
            "1114.0231\t166\t\"\"\r\n" +
            "1120.5024\t146\t\"P_G21000^2/Dev=10.0\"\r\n" +
            "1121.0151\t235\t\"P_G21000i^2\"\r\n" +
            "1201.5505\t193\t\"\"\r\n" +
            "1203.0271\t197\t\"\"\r\n" +
            "1282.5559\t434\t\"P_G23000^2/Dev=8.3\"\r\n" +
            "1283.0718\t182\t\"P_G23000i^2\"\r\n" +
            "1283.5687\t428\t\"P_G23000i^2\"\r\n" +
            "1284.0553\t179\t\"P_G23000i^2\"\r\n" +
            "1836.4081\t161\t\"\"\r\n" +
            "2337.8401\t201\t\"\"\r\n" +
            "2562.6294\t168\t\"\"\r\n" +
            "\r\n";


        private const string NIST_TERM_MODS =
            "Name: n[43]AFVFPKESDTSYc[17]/5\n" +
            "LibID: 5\n" +
            "MW: 1456.6922\n" +
            "PrecursorMZ: 291.3384\n" +
            "Status: Normal\n" +
            "FullName: K.n[43]AFVFPKESDTSYc[17].V/5\n" +
            "NumPeaks: 82\n"+
            "128.9000	25.0	?	2/3 0.1\n"+
            "134.9000	19.0	y2^2/-0.16,y5-36^4/0.09	2/3 0.1\n"+
            "173.0000	26.0	b3-17^2/0.91,b7^5/-0.10	2/3 0.3\n"+
            "174.3000	20.0	?	2/1 0.2\n"+
            "181.0000	29.0	b3^2/0.40,b8-44^5/-0.70,b8-46^5/-0.30	2/3 0.1\n"+
            "182.9000	20.0	y1/0.82,b6^4/-0.96,y8-17^5/0.22,y8-18^5/0.41,b8-34^5/-0.79,b8-35^5/-0.59,b8-36^5/-0.40	2/2 0.2\n"+
            "184.9000	17.0	y3^2/-0.68,y5-18^3/-0.51,a8^5/-0.00	2/1 0.1\n"+
            "188.1000	24.0	?	2/3 0.3\n"+
            "190.9000	26.0	y5^3/-0.51,b8^5/0.40	2/3 0.1\n"+
            "192.2000	24.0	a5^3/-0.58	2/3 0.3\n"+
            "198.1000	87.0	?	2/3 0.2\n"+
            "199.9000	79.0	y7-35^4/0.56,y7-36^4/0.81	2/3 0.5\n"+
            "200.9000	20.0	?	2/1 0.2\n"+
            "205.8000	13.0	b7-44^4/0.68	2/1 0.0\n"+
            "207.0000	30.0	b7-34^4/-0.60,b7-35^4/-0.36,y9-44^5/0.30,y9-46^5/0.70,a9^5/-0.91,b9-34^5/0.30,b9-35^5/0.50,b9-36^5/0.70	2/3 0.1\n"+
            "211.1000	15.0	b7-17^4/-0.76,b7-18^4/-0.52,b9-17^5/1.00	2/2 0.1\n"+
            "213.0000	45.0	y9-17^5/0.90,b9^5/-0.51	2/3 0.7\n"+
            "214.2000	25.0	?	2/3 0.1\n"+
            "214.9000	47.0	?	2/3 0.1\n"+
            "216.0000	80.0	b7^4/-0.12,y9^5/0.50	2/3 0.5\n"+
            "217.2000	47.0	?	3/3 0.4\n"+
            "218.1000	54.0	?	2/3 0.5\n"+
            "218.9000	45.0	y6-44^3/-0.86,y6-46^3/-0.19	2/3 0.1\n"+
            "223.0000	43.0	y2-46/-0.11,y6-36^3/0.58,y8-36^4/-0.35	3/3 0.1\n"+
            "224.0000	45.0	y2-46i/0.00,y8-35^4/0.40	2/3 0.0\n"+
            "225.1000	99.0	y2-44/-0.02,y4-36^2/0.01,b10-44^5/0.18,b10-46^5/0.58	2/3 1.1\n"+
            "226.1000	101.0	y2-44i/0.00,b8-46^4/-0.27,y10-46^5/-0.01	2/3 0.9\n"+
            "226.9000	112.0	y2-44i/-0.20,b8-44^4/0.02,y10-44^5/0.38,b10-34^5/-0.01,b10-35^5/0.19,b10-36^5/0.39	2/3 1.0\n"+
            "228.0000	80.0	y6-18^3/-0.42,y8-17^4/-0.10,y8-18^4/0.14,y10-35^5/-0.31,y10-36^5/-0.11,a10^5/-0.12	2/3 0.6\n"+
            "229.1000	106.0	b8-34^4/-0.26,b8-35^4/-0.02,b8-36^4/0.23	2/3 1.3\n"+
            "230.0000	131.0	b10-17^5/-0.31,b10-18^5/-0.12	2/3 1.2\n"+
            "230.9000	81.0	a8^4/0.02	3/3 0.3\n"+
            "232.0000	39.0	y10-17^5/0.09,y10-18^5/0.29	3/3 0.3\n"+
            "232.9000	81.0	y8^4/0.54,a2/-0.23,y2-36/-0.19,b6-34^3/-0.56,b8-18^4/-0.47	2/3 0.7\n"+
            "233.9000	50.0	y6^3/-0.53,y4-18^2/-0.19,b8-17^4/0.28,b10^5/0.18	2/3 0.3\n"+
            "239.0000	23.0	b6-17^3/-0.13	3/3 0.2\n"+
            "240.9000	63.0	a4^2/0.76	3/3 0.5\n"+
            "242.0000	168.0	b11-44^5/-0.33,b11-46^5/0.08	3/3 1.4\n"+
            "243.0000	257.0	y4^2/-0.10	3/3 0.7\n"+
            "243.9000	361.0	b2-17/-0.20,b11-35^5/-0.22,b11-36^5/-0.02	3/3 2.3\n"+
            "244.9000	1011.0	b6^3/0.09,b11-34^5/0.59	3/3 0.5\n"+
            "245.8000	478.0	b4-17^2/0.18,a11^5/0.28	3/3 5.9\n"+
            "246.9000	2115.0	b11-17^5/-0.82,b11-18^5/-0.62	3/3 44.0\n"+
            "247.9000	198.0	?	3/3 1.3\n"+
            "249.0000	53.0	?	3/3 0.3\n"+
            "251.8000	64.0	y2-18/0.70,b11^5/0.68	2/3 0.5\n"+
            "253.1000	87.0	y2-18i/0.30	3/3 1.0\n"+
            "254.3000	51.0	b4^2/0.17	2/3 0.6\n"+
            "255.0000	43.0	b4^2i/0.20,b9-46^4/-0.13,b11+18^5/0.27	3/3 0.1\n"+
            "255.9000	58.0	b9-44^4/0.26,y11-44^5/-0.03,y11-46^5/0.37	2/3 0.2\n"+
            "257.1000	72.0	?	3/3 0.4\n"+
            "257.9000	75.0	y9-44^4/-0.23,y9-46^4/0.28,b9-34^4/-0.22,b9-35^4/0.03,b9-36^4/0.27,y11-35^5/0.18,y11-36^5/0.38	2/3 0.3\n"+
            "259.0000	201.0	a9^4/-0.63	3/3 1.1\n"+
            "260.0000	88.0	y9-36^4/-0.12	3/3 0.2\n"+
            "261.0000	234.0	b2/-0.12,y9-35^4/0.63,y11-17^5/-0.32,y11-18^5/-0.13	3/3 1.4\n"+
            "261.9000	71.0	b2i/-0.10,y7-46^3/0.11,b9-17^4/-0.48,b9-18^4/-0.23	3/3 0.4\n"+
            "262.9000	75.0	b2i/-0.10,y7-44^3/0.44	3/3 0.4\n"+
            "263.8000	67.0	y5-46^2/0.19	3/3 0.4\n"+
            "264.8000	149.0	y5-44^2/0.18,y7-35^3/-0.64,y7-36^3/-0.32,y9-17^4/-0.07,y9-18^4/0.18,y11^5/0.07	3/3 1.9\n"+
            "266.6000	70.0	b9^4/-0.03	3/3 0.1\n"+
            "268.9000	4744.0	y2/-0.21,y9^4/-0.23,y5-36^2/0.30	3/3 3.4\n"+
            "269.8000	10000.0	y2i/-0.10	3/3 0.0\n"+
            "270.9000	1163.0	y2i/0.00,y7-17^3/-0.55,y7-18^3/-0.22	3/3 4.7\n"+
            "272.1000	244.0	b7-46^3/-0.39	2/3 0.9\n"+
            "273.8000	31.0	b7-44^3/0.64	2/3 0.4\n"+
            "275.0000	84.0	?	2/3 0.7\n"+
            "277.8000	115.0	y7^3/0.68,y5-18^2/0.19	2/3 0.1\n"+
            "278.5000	183.0	p-44^5/-0.04,p-46^5/0.36,a7^3/0.01	3/3 0.9\n"+
            "279.7000	145.0	p-35^5/-0.63,p-36^5/-0.43,b10-46^4/-0.69	2/3 0.2\n"+
            "282.6000	43.0	y10-44^4/-0.29,y10-46^4/0.21,b7-17^3/0.45,b7-18^3/0.78,b10-34^4/-0.78,b10-35^4/-0.54,b10-36^4/-0.29	2/3 0.3\n"+
            "286.3000	100.0	?	2/3 1.2\n"+
            "287.0000	573.0	y5^2/0.39,p^5/-0.34,b10-18^4/-0.39	3/3 2.5\n"+
            "287.9000	621.0	b7^3/0.08,b10-17^4/0.26	3/3 1.8\n"+
            "288.9000	150.0	y10-17^4/-0.74,y10-18^4/-0.49,a5^2/0.24	2/3 0.5\n"+
            "292.7000	34.0	b10^4/0.81	2/3 0.2\n"+
            "296.0000	38.0	?	2/3 0.0\n"+
            "300.0000	39.0	?	2/3 0.1\n"+
            "304.8000	47.0	b8-36^3/-0.03,b11-35^4/-0.09,b11-36^4/0.15	3/3 0.2\n"+
            "305.8000	113.0	b8-34^3/0.32,b8-35^3/0.65,b11-34^4/0.66,a11^4/-0.85	3/3 0.3\n"+
            "316.5000	38.0	b8^3/-0.33	2/3 0.0\n"+
            "319.9000	43.0	y11-44^4/0.24,y11-46^4/0.74	2/3 0.7\n"+
            "323.9000	24.0	y3-46/-0.26	2/3 0.2\n";

    }
}