/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class SrmSettingsNewTest : AbstractUnitTest
    {
        [TestMethod]
        public void NewDocumentSettingsTest()
        {
            var settingsDef = SrmSettingsList.GetDefault();
            var settingsLight = settingsDef.ChangePeptideModifications(pm =>
                pm.ChangeInternalStandardTypes(new[] { IsotopeLabelType.light }));
            // Light internal standard type should get set back to heavy
            var settingsNew = SrmSettingsList.GetNewDocumentSettings(settingsLight);
            AssertEx.AreEqualDeep(settingsDef.PeptideSettings.Modifications.InternalStandardTypes,
                settingsNew.PeptideSettings.Modifications.InternalStandardTypes);
            // Internal standard "none" should be left alone
            var settingsNone = settingsDef.ChangePeptideModifications(pm =>
                pm.ChangeInternalStandardTypes(Array.Empty<IsotopeLabelType>()));
            settingsNew = SrmSettingsList.GetNewDocumentSettings(settingsNone);
            Assert.AreEqual(0, settingsNew.PeptideSettings.Modifications.InternalStandardTypes.Count);
            // Internal standard heavy with labeling modifications should be left alone
            var aquaMods = new[]
            {
                UniMod.GetModification("Label:13C(6)15N(4) (C-term R)", out _),
                UniMod.GetModification("Label:13C(6)15N(2) (C-term K)", out _),
            };
            var settingsAqua = settingsDef.ChangePeptideModifications(pm =>
                pm.ChangeModifications(IsotopeLabelType.heavy, aquaMods));
            settingsNew = SrmSettingsList.GetNewDocumentSettings(settingsAqua);
            AssertEx.AreEqualDeep(settingsDef.PeptideSettings.Modifications.InternalStandardTypes,
                settingsNew.PeptideSettings.Modifications.InternalStandardTypes);
            AssertEx.AreEqualDeep(settingsAqua.PeptideSettings.Modifications.GetModifications(IsotopeLabelType.heavy),
                settingsNew.PeptideSettings.Modifications.GetModifications(IsotopeLabelType.heavy));
            // Inverse curve settings ("light" as standard) should get set back to "heavy"
            // with modifications list cleared like the default settings
            var settingsInverse = settingsAqua.ChangePeptideModifications(pm =>
                pm.ChangeInternalStandardTypes(new[] { IsotopeLabelType.light }));
            settingsNew = SrmSettingsList.GetNewDocumentSettings(settingsInverse);
            AssertEx.AreEqualDeep(settingsDef.PeptideSettings.Modifications.InternalStandardTypes,
                settingsNew.PeptideSettings.Modifications.InternalStandardTypes);
            AssertEx.AreEqualDeep(settingsDef.PeptideSettings.Modifications.GetModifications(IsotopeLabelType.heavy),
                settingsNew.PeptideSettings.Modifications.GetModifications(IsotopeLabelType.heavy));
            // Complex internal standard types should also get set back to "heavy"
            // with all isotope label type mods cleared to avoid creating unwanted
            // labeled precursors
            var typedModsAqua = new TypedModifications(IsotopeLabelType.heavy, aquaMods);
            var ilt15N = new IsotopeLabelType("15N", 2);
            var typedMods15N = new TypedModifications(ilt15N, new[]
            {
                UniMod.GetModification("Label:15N", out _),
            });
            var settingsComplex = settingsAqua.ChangePeptideModifications(pm =>
                new PeptideModifications(pm.StaticModifications, pm.MaxVariableMods, pm.MaxNeutralLosses,
                    new[] {typedModsAqua, typedMods15N},
                    new[] { IsotopeLabelType.heavy, ilt15N }));
            settingsNew = SrmSettingsList.GetNewDocumentSettings(settingsComplex);
            AssertEx.AreEqualDeep(settingsDef.PeptideSettings.Modifications.InternalStandardTypes,
                settingsNew.PeptideSettings.Modifications.InternalStandardTypes);
            var heavyMods = settingsNew.PeptideSettings.Modifications.HeavyModifications;
            Assert.AreEqual(2, heavyMods.Length);
            foreach (var heavyMod in settingsNew.PeptideSettings.Modifications.HeavyModifications)
                Assert.AreEqual(0, heavyMod.Modifications.Count);
        }
    }
}
