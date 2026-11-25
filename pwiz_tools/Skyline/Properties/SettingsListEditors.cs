/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using SkylineTool;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.CommonFileDialogs;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.CommonMsData.RemoteApi.Unifi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Properties
{
    /// <summary>
    /// Editing for the settings lists, which live in SkylineCore and know nothing of dialogs.
    /// The editing a list used to do in its own EditItem / EditList / AcceptList overrides is
    /// registered here by list type, and reached through extension methods of the same names,
    /// so a caller still writes <c>Settings.Default.EnzymeList.EditItem(this, ...)</c>.
    /// Generic list dialogs get an <see cref="IItemEditor{TItem}"/> from <see cref="GetItemEditor{TItem}"/>.
    /// </summary>
    public static class SettingsListEditors
    {
        private static readonly Dictionary<Type, object> ITEM_EDITORS = new Dictionary<Type, object>();
        private static readonly Dictionary<Type, object> LIST_EDITORS = new Dictionary<Type, object>();
        private static readonly Dictionary<Type, object> LIST_ACCEPTORS = new Dictionary<Type, object>();

        static SettingsListEditors()
        {
            AddItemEditor<ToolList, ToolDescription>(EditItemToolList);
            AddItemEditor<EnzymeList, Enzyme>(EditItemEnzymeList);
            AddItemEditor<PeptideExcludeList, PeptideExcludeRegex>(EditItemPeptideExcludeList);
            AddItemEditor<ServerList, Server>(EditItemServerList);
            AddItemEditor<SearchToolList, SearchTool>(EditItemSearchToolList);
            AddItemEditor<SpectralLibraryList, LibrarySpec>(EditItemSpectralLibraryList);
            AddItemEditor<CarafeModelList, CarafeModelSpec>(EditItemCarafeModelList);
            AddListEditor<CarafeModelList, CarafeModelSpec>(EditListCarafeModelList);
            AddItemEditor<BackgroundProteomeList, BackgroundProteomeSpec>(EditItemBackgroundProteomeList);
            AddItemEditor<StaticModList, StaticMod>(EditItemStaticModList);
            AddItemEditor<HeavyModList, StaticMod>(EditItemHeavyModList);
            AddItemEditor<CollisionEnergyList, CollisionEnergyRegression>(EditItemCollisionEnergyList);
            AddItemEditor<OptimizationLibraryList, OptimizationLibrary>(EditItemOptimizationLibraryList);
            AddItemEditor<DeclusterPotentialList, DeclusteringPotentialRegression>(EditItemDeclusterPotentialList);
            AddItemEditor<CompensationVoltageList, CompensationVoltageParameters>(EditItemCompensationVoltageList);
            AddListAcceptor<RTScoreCalculatorList, RetentionScoreCalculatorSpec>(AcceptListRTScoreCalculatorList);
            AddItemEditor<RTScoreCalculatorList, RetentionScoreCalculatorSpec>(EditItemRTScoreCalculatorList);
            AddItemEditor<IrtStandardList, IrtStandard>(EditItemIrtStandardList);
            AddListAcceptor<IonMobilityLibraryList, IonMobilityLibrary>(AcceptListIonMobilityLibraryList);
            AddItemEditor<IonMobilityLibraryList, IonMobilityLibrary>(EditItemIonMobilityLibraryList);
            AddItemEditor<PeakScoringModelList, PeakScoringModelSpec>(EditItemPeakScoringModelList);
            AddItemEditor<RetentionTimeList, RetentionTimeRegression>(EditItemRetentionTimeList);
            AddItemEditor<MeasuredIonList, MeasuredIon>(EditItemMeasuredIonList);
            AddItemEditor<IsotopeEnrichmentsList, IsotopeEnrichments>(EditItemIsotopeEnrichmentsList);
            AddItemEditor<IsolationSchemeList, IsolationScheme>(EditItemIsolationSchemeList);
            AddItemEditor<AnnotationDefList, AnnotationDef>(EditItemAnnotationDefList);
            AddItemEditor<ColorSchemeList, ColorScheme>(EditItemColorSchemeList);
            AddItemEditor<GroupComparisonDefList, GroupComparisonDef>(EditItemGroupComparisonDefList);
            AddItemEditor<ListDefList, ListData>(EditItemListDefList);
            AddItemEditor<MetadataRuleSetList, MetadataRuleSet>(EditItemMetadataRuleSetList);
            AddItemEditor<RemoteAccountList, RemoteAccount>(EditItemRemoteAccountList);
            // Lists that are not SettingsList<T>, so copy through their own CopyItem
            AddItemEditor<ReportSpecList, ReportSpec>((list, owner, item, existing, tag) => throw new InvalidOperationException(),
                (list, item) => list.CopyItem(item));
            AddItemEditor<ReportOrViewSpecList, ReportOrViewSpec>((list, owner, item, existing, tag) => null,
                (list, item) => list.CopyItem(item));
        }

        /// <summary>
        /// The item editor for a list: the list itself when it implements
        /// <see cref="IItemEditor{TItem}"/> (lists defined in the UI), otherwise the one registered for
        /// its type, or null when it has none.
        /// </summary>
        public static IItemEditor<TItem> GetItemEditor<TItem>(object list)
        {
            if (list is IItemEditor<TItem> itemEditor)
                return itemEditor;
            var registration = (ItemEditorRegistration<TItem>) Find(ITEM_EDITORS, list);
            return registration == null ? null : new ListItemEditor<TItem>(list, registration);
        }

        public static TItem NewItem<TItem>(this SettingsList<TItem> list, Control owner, IEnumerable<TItem> existing, object tag)
            where TItem : IKeyContainer<string>, IXmlSerializable
        {
            return list.EditItem(owner, default(TItem), existing, tag);
        }

        public static TItem EditItem<TItem>(this SettingsList<TItem> list, Control owner, TItem item, IEnumerable<TItem> existing, object tag)
            where TItem : IKeyContainer<string>, IXmlSerializable
        {
            var itemEditor = GetItemEditor<TItem>(list);
            if (itemEditor == null)
                throw new InvalidOperationException(string.Format(@"No editor is registered for {0}", list.GetType()));
            return itemEditor.EditItem(owner, item, existing, tag);
        }

        /// <summary>
        /// Shows the list editor for a whole list, returning the new list, or null if the user cancelled.
        /// </summary>
        public static IEnumerable<TItem> EditList<TItem>(this SettingsListBase<TItem> list, Control owner, object tag)
            where TItem : IKeyContainer<string>, IXmlSerializable
        {
            var editList = (Func<object, Control, object, IEnumerable<TItem>>) Find(LIST_EDITORS, list);
            return editList != null ? editList(list, owner, tag) : DefaultEditList(list, owner, tag);
        }

        /// <summary>
        /// True, if a new list is accepted to replace the current list.
        /// </summary>
        public static bool AcceptList<TItem>(this SettingsListBase<TItem> list, Control owner, IList<TItem> listNew)
            where TItem : IKeyContainer<string>, IXmlSerializable
        {
            var acceptList = (Func<object, Control, IList<TItem>, bool>) Find(LIST_ACCEPTORS, list);
            return acceptList == null || acceptList(list, owner, listNew);
        }

        public static Server AddServerWithAccount(this ServerList list, Control owner, IEnumerable<Server> existing)
        {
            return EditPanoramaServer(list, owner, null, existing, null, null, true);
        }

        public static Server AddCredentials(this ServerList list, Control owner, Server item, IEnumerable<Server> existing)
        {
            return EditPanoramaServer(list, owner, item, existing, null, null, true);
        }

        public static Server EditCredentials(this ServerList list, Control owner, Server item, IEnumerable<Server> existing, string username, string password)
        {
            return EditPanoramaServer(list, owner, item, existing, username, password);
        }

        private static Server EditPanoramaServer(ServerList list, Control owner, Server item, IEnumerable<Server> existing, string username, string password, bool disableAnonymousCb = false)
        {
            using (var editServerDlg = new EditServerDlg(existing ?? list))
            {
                if (item != null)
                {
                    editServerDlg.Server = item;
                    editServerDlg.textServerURL.Enabled = false;
                }

                editServerDlg.Username = username;
                editServerDlg.Password = password;

                if (disableAnonymousCb)
                {
                    editServerDlg.AnonymousServer = false;
                    editServerDlg.cbAnonymous.Enabled = false;
                }

                return editServerDlg.ShowDialog(owner) == DialogResult.OK ? editServerDlg.Server : null;
            }
        }

        private static IEnumerable<TItem> DefaultEditList<TItem>(SettingsListBase<TItem> list, Control owner, object tag)
            where TItem : IKeyContainer<string>, IXmlSerializable
        {
            using (var dlg = new EditListDlg<SettingsListBase<TItem>, TItem>(list, tag))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    var listNew = dlg.GetAllEdited().ToArray();
                    if (list.AcceptList(owner, listNew))
                        return listNew;
                }
                return null;
            }
        }

        private static void AddItemEditor<TList, TItem>(Func<TList, Control, TItem, IEnumerable<TItem>, object, TItem> editItem)
            where TList : SettingsList<TItem>
            where TItem : IKeyContainer<string>, IXmlSerializable
        {
            AddItemEditor(editItem, (list, item) => list.CopyItem(item));
        }

        private static void AddItemEditor<TList, TItem>(Func<TList, Control, TItem, IEnumerable<TItem>, object, TItem> editItem,
            Func<TList, TItem, TItem> copyItem)
        {
            ITEM_EDITORS.Add(typeof(TList), new ItemEditorRegistration<TItem>(
                (list, owner, item, existing, tag) => editItem((TList) list, owner, item, existing, tag),
                (list, item) => copyItem((TList) list, item)));
        }

        private static void AddListEditor<TList, TItem>(Func<TList, Control, object, IEnumerable<TItem>> editList)
        {
            LIST_EDITORS.Add(typeof(TList), new Func<object, Control, object, IEnumerable<TItem>>(
                (list, owner, tag) => editList((TList) list, owner, tag)));
        }

        private static void AddListAcceptor<TList, TItem>(Func<TList, Control, IList<TItem>, bool> acceptList)
        {
            LIST_ACCEPTORS.Add(typeof(TList), new Func<object, Control, IList<TItem>, bool>(
                (list, owner, listNew) => acceptList((TList) list, owner, listNew)));
        }

        /// <summary>
        /// The registration for the list's type or the nearest base type that has one.
        /// </summary>
        private static object Find(Dictionary<Type, object> registry, object list)
        {
            for (var type = list.GetType(); type != null; type = type.BaseType)
            {
                if (registry.TryGetValue(type, out var registration))
                    return registration;
            }
            return null;
        }

        private sealed class ItemEditorRegistration<TItem>
        {
            public ItemEditorRegistration(Func<object, Control, TItem, IEnumerable<TItem>, object, TItem> editItem,
                Func<object, TItem, TItem> copyItem)
            {
                EditItem = editItem;
                CopyItem = copyItem;
            }

            public Func<object, Control, TItem, IEnumerable<TItem>, object, TItem> EditItem { get; }
            public Func<object, TItem, TItem> CopyItem { get; }
        }

        private sealed class ListItemEditor<TItem> : IItemEditor<TItem>
        {
            private readonly object _list;
            private readonly ItemEditorRegistration<TItem> _registration;

            public ListItemEditor(object list, ItemEditorRegistration<TItem> registration)
            {
                _list = list;
                _registration = registration;
            }

            public TItem NewItem(Control owner, IEnumerable<TItem> existing, object tag)
            {
                return EditItem(owner, default(TItem), existing, tag);
            }

            public TItem EditItem(Control owner, TItem item, IEnumerable<TItem> existing, object tag)
            {
                return _registration.EditItem(_list, owner, item, existing, tag);
            }

            public TItem CopyItem(TItem item)
            {
                return _registration.CopyItem(_list, item);
            }
        }

        private static ToolDescription EditItemToolList(ToolList list, Control owner, ToolDescription item, IEnumerable<ToolDescription> existing, object tag)
        {   throw new InvalidOperationException(); }

        private static Enzyme EditItemEnzymeList(EnzymeList list, Control owner, Enzyme item, IEnumerable<Enzyme> existing, object tag)
        {
            using (EditEnzymeDlg editEnzyme = new EditEnzymeDlg(existing ?? list))
            {
                editEnzyme.Enzyme = item;
                if (editEnzyme.ShowDialog(owner) == DialogResult.OK)
                    return editEnzyme.Enzyme;

                return null;
            }
        }

        private static PeptideExcludeRegex EditItemPeptideExcludeList(PeptideExcludeList list, Control owner, PeptideExcludeRegex item,
            IEnumerable<PeptideExcludeRegex> existing, object tag)
        {
            using (EditExclusionDlg editExclusion = new EditExclusionDlg(existing ?? list))
            {
                editExclusion.Exclusion = item;
                if (editExclusion.ShowDialog(owner) == DialogResult.OK)
                    return editExclusion.Exclusion;

                return null;
            }
        }

        private static Server EditItemServerList(ServerList list, Control owner, Server item, IEnumerable<Server> existing, object tag)
        {
            using (EditServerDlg editServer = new EditServerDlg(existing ?? list))
            {
                editServer.Server = item;
                bool instructionsRequired = false;
                if (tag != null)
                     instructionsRequired = (bool) tag;
                if (instructionsRequired)
                    editServer.ShowInstructions();
                if (editServer.ShowDialog(owner) == DialogResult.OK)
                    return editServer.Server;

                return null;
            }
        }

        private static SearchTool EditItemSearchToolList(SearchToolList list, Control owner, SearchTool item, IEnumerable<SearchTool> existing, object tag)
        {
            using (var editTool = new EditSearchToolDlg(existing ?? list))
            {
                editTool.SearchTool = item;
                if (editTool.ShowDialog(owner) == DialogResult.OK)
                    return editTool.SearchTool;

                return null;
            }
        }

        private static LibrarySpec EditItemSpectralLibraryList(SpectralLibraryList list, Control owner, LibrarySpec item,
            IEnumerable<LibrarySpec> existing, object tag)
        {
            using (EditLibraryDlg editLibrary = new EditLibraryDlg(existing ?? list))
            {
                editLibrary.LibrarySpec = item;
                if (editLibrary.ShowDialog(owner) == DialogResult.OK)
                    return editLibrary.LibrarySpec;

                return null;
            }
        }

        private static CarafeModelSpec EditItemCarafeModelList(CarafeModelList list, Control owner, CarafeModelSpec item,
            IEnumerable<CarafeModelSpec> existing, object tag)
        {
            using (var editModel = new EditCarafeModelDlg(existing ?? list))
            {
                editModel.ModelSpec = item;
                if (editModel.ShowDialog(owner) == DialogResult.OK)
                    return editModel.ModelSpec;

                return null;
            }
        }

        private static IEnumerable<CarafeModelSpec> EditListCarafeModelList(CarafeModelList list, Control owner, object tag)
        {
            using (var dlg = new CarafeModelListDlg(list))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                    return dlg.GetAllEdited().ToArray();
                return null;
            }
        }

        private static BackgroundProteomeSpec EditItemBackgroundProteomeList(BackgroundProteomeList list, Control owner, BackgroundProteomeSpec item,
            IEnumerable<BackgroundProteomeSpec> existing, object tag)
        {
            using (var editBackgroundProteomeDlg = new BuildBackgroundProteomeDlg(existing ?? list))
            {
                editBackgroundProteomeDlg.BackgroundProteomeSpec = item;
                if (editBackgroundProteomeDlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return editBackgroundProteomeDlg.BackgroundProteomeSpec;
                }
                return null;
            }
        }

        private static StaticMod EditItemStaticModList(StaticModList list, Control owner, StaticMod item,
            IEnumerable<StaticMod> existing, object tag)
        {
            using (EditStaticModDlg editMod = new EditStaticModDlg(item, existing ?? list, false))
            {
                if (editMod.ShowDialog(owner) == DialogResult.OK)
                    return editMod.Modification;

                return null;
            }
        }

        private static StaticMod EditItemHeavyModList(HeavyModList list, Control owner, StaticMod item,
            IEnumerable<StaticMod> existing, object tag)
        {
            using (EditStaticModDlg editMod = new EditStaticModDlg(item, existing ?? list, true))
            {
                editMod.Text = PropertiesResources.HeavyModList_EditItem_Edit_Isotope_Modification;
                if (editMod.ShowDialog(owner) == DialogResult.OK)
                    return editMod.Modification;

                return null;
            }
        }

        private static CollisionEnergyRegression EditItemCollisionEnergyList(CollisionEnergyList list, Control owner, CollisionEnergyRegression item,
            IEnumerable<CollisionEnergyRegression> existing, object tag)
        {
            using (EditCEDlg editCE = new EditCEDlg(existing ?? list))
            {
                editCE.Regression = item;
                if (editCE.ShowDialog(owner) == DialogResult.OK)
                    return editCE.Regression;

                return null;
            }
        }

        private static OptimizationLibrary EditItemOptimizationLibraryList(OptimizationLibraryList list, Control owner, OptimizationLibrary item,
            IEnumerable<OptimizationLibrary> existing, object tag)
        {
            using (EditOptimizationLibraryDlg editOptimizationLibraryDlg = new EditOptimizationLibraryDlg(Program.ActiveDocument, item, existing ?? list))
            {
                return editOptimizationLibraryDlg.ShowDialog(owner) == DialogResult.OK ? editOptimizationLibraryDlg.Library : null;
            }
        }

        private static DeclusteringPotentialRegression EditItemDeclusterPotentialList(DeclusterPotentialList list, Control owner, DeclusteringPotentialRegression item,
            IEnumerable<DeclusteringPotentialRegression> existing, object tag)
        {
            using (EditDPDlg editDP = new EditDPDlg(existing ?? list))
            {
                editDP.Regression = item;
                if (editDP.ShowDialog(owner) == DialogResult.OK)
                    return editDP.Regression;

                return null;
            }
        }

        private static CompensationVoltageParameters EditItemCompensationVoltageList(CompensationVoltageList list, Control owner, CompensationVoltageParameters item,
            IEnumerable<CompensationVoltageParameters> existing, object tag)
        {
            using (var editCov = new EditCoVDlg(item, existing ?? list))
            {
                return editCov.ShowDialog(owner) == DialogResult.OK ? editCov.Parameters : null;
            }
        }

        /// <summary>
        /// <see cref="RetentionTimeRegression"/> objects depend on calculators. If a user deletes or changes a calculator,
        /// the <see cref="RetentionTimeRegression"/> objects that depend on it may need to be removed.
        /// </summary>
        private static bool AcceptListRTScoreCalculatorList(RTScoreCalculatorList list, Control owner, IList<RetentionScoreCalculatorSpec> listNew)
        {
            var listMissingCalc = new List<RetentionTimeRegression>();
            var listChangedCalc = new List<RetentionTimeRegression>();
            foreach (var regression in Settings.Default.RetentionTimeList.ToArray())
            {
                var regressionInst = regression;

                //There is a dummy regression called "None" with a null calculator
                if (regressionInst.Calculator == null)
                    continue;

                if (listNew.Contains(calc => Equals(calc, regressionInst.Calculator)))
                {
                    var calcChanged = listNew.FirstOrDefault(calc =>
                        Equals(calc, regressionInst.Calculator.ChangeName(calc.Name)));

                    if (calcChanged == null)
                        listMissingCalc.Add(regressionInst);
                    else
                        listChangedCalc.Add(regressionInst.ChangeCalculator(calcChanged));
                }
            }

            if (listMissingCalc.Count > 0)
            {
                var message = TextUtil.LineSeparate(PropertiesResources.RTScoreCalculatorList_AcceptList_The_regressions,
                                                    TextUtil.LineSeparate(listMissingCalc.Select(reg => reg.Name)),
                                                    PropertiesResources.RTScoreCalculatorList_AcceptList_will_be_deleted_because_the_calculators_they_depend_on_have_changed_Do_you_want_to_continue);
                if (DialogResult.Yes != MultiButtonMsgDlg.Show(owner, message, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true))
                {
                    return false;
                }
            }

            foreach (var regression in listChangedCalc)
            {
                Settings.Default.RetentionTimeList.SetValue(regression);                
            }

            return true;
        }

        private static RetentionScoreCalculatorSpec EditItemRTScoreCalculatorList(RTScoreCalculatorList list, Control owner, RetentionScoreCalculatorSpec item,
            IEnumerable<RetentionScoreCalculatorSpec> existing, object tag)
        {
            var calc = item as RCalcIrt;
            if (item == null || calc != null)
            {
                using (EditIrtCalcDlg editStandardDlg = new EditIrtCalcDlg(calc, existing))
                {
                    if (editStandardDlg.ShowDialog(owner) == DialogResult.OK)
                    {
                        return editStandardDlg.Calculator;
                    }
                }
            }

            return null;
        }

        private static IrtStandard EditItemIrtStandardList(IrtStandardList list, Control owner, IrtStandard item, IEnumerable<IrtStandard> existing,
            object tag)
        {
            var updatePeptides = new DbIrtPeptide[0];
            var editIrtCalcDlg = owner as EditIrtCalcDlg;
            var recalibrate = item != null && editIrtCalcDlg != null;
            if (recalibrate)
            {
                updatePeptides = editIrtCalcDlg.AllPeptides.ToArray();
            }

            using (var calibrateIrtDlg = new CalibrateIrtDlg(item, existing ?? list, updatePeptides))
            {
                if (calibrateIrtDlg.ShowDialog(owner) == DialogResult.OK)
                {
                    if (recalibrate)
                    {
                        editIrtCalcDlg.ResetPeptideListBindings();
                    }
                    return calibrateIrtDlg.IrtStandard;
                }
                return null;
            }
        }

        private static bool AcceptListIonMobilityLibraryList(IonMobilityLibraryList list, Control owner, IList<IonMobilityLibrary> listNew)
        {
            return true;
        }

        private static IonMobilityLibrary EditItemIonMobilityLibraryList(IonMobilityLibraryList list, Control owner, IonMobilityLibrary item,
            IEnumerable<IonMobilityLibrary> existing, object tag)
        {
            var ionMobilityFilteringUserControl = (owner as IonMobilityFilteringUserControl) ??
                                                  ((owner as TransitionSettingsUI)?.IonMobilityControl) ??  // Accessed via Settings>TransitionSettings>IonMobility>Add
                                                  ((owner as Form)?.Owner as TransitionSettingsUI)?.IonMobilityControl; // Accessed via Settings>TransitionSettings>IonMobility>EditList>Add|EditCurrent
            var ionMobilityWindowWidthCalculator = ionMobilityFilteringUserControl!.IonMobilityWindowWidthCalculator;
            using var editIonMobilityLibraryDlg = new EditIonMobilityLibraryDlg(item, existing, ionMobilityWindowWidthCalculator);
            if (editIonMobilityLibraryDlg.ShowDialog(owner) == DialogResult.OK)
            {
                return editIonMobilityLibraryDlg.IonMobilityLibrary;
            }

            return null;
        }

        private static PeakScoringModelSpec EditItemPeakScoringModelList(PeakScoringModelList list, Control owner, PeakScoringModelSpec item,
            IEnumerable<PeakScoringModelSpec> existing, object tag)
        {
            return EditPeakScoringModelDlg.ShowEditPeakScoringModelDlg(owner, item, existing ?? list,
                tag as IFeatureScoreProvider);
        }

        private static RetentionTimeRegression EditItemRetentionTimeList(RetentionTimeList list, Control owner, RetentionTimeRegression item,
            IEnumerable<RetentionTimeRegression> existing, object tag)
        {
            using (EditRTDlg editRT = new EditRTDlg(existing ?? list))
            {
                editRT.Regression = item;
                if (editRT.ShowDialog(owner) == DialogResult.OK)
                    return editRT.Regression;
            }

            return null;
        }

        private static MeasuredIon EditItemMeasuredIonList(MeasuredIonList list, Control owner, MeasuredIon item,
            IEnumerable<MeasuredIon> existing, object tag)
        {
            using (EditMeasuredIonDlg editIon = new EditMeasuredIonDlg(existing ?? list))
            {
                editIon.MeasuredIon = item;
                if (editIon.ShowDialog(owner) == DialogResult.OK)
                    return editIon.MeasuredIon;
            }

            return null;
        }

        private static IsotopeEnrichments EditItemIsotopeEnrichmentsList(IsotopeEnrichmentsList list, Control owner, IsotopeEnrichments item,
            IEnumerable<IsotopeEnrichments> existing, object tag)
        {
            using (EditIsotopeEnrichmentDlg editEnrichment = new EditIsotopeEnrichmentDlg(existing ?? list))
            {
                editEnrichment.Enrichments = item;
                if (editEnrichment.ShowDialog(owner) == DialogResult.OK)
                    return editEnrichment.Enrichments;
            }

            return null;
        }

        private static IsolationScheme EditItemIsolationSchemeList(IsolationSchemeList list, Control owner, IsolationScheme item,
            IEnumerable<IsolationScheme> existing, object tag)
        {
            using (var editIsolationScheme = new EditIsolationSchemeDlg(existing ?? list, tag as SrmSettings))
            {
                editIsolationScheme.IsolationScheme = item;
                if (editIsolationScheme.ShowDialog(owner) == DialogResult.OK)
                    return editIsolationScheme.IsolationScheme;
            }

            return null;
        }

        private static AnnotationDef EditItemAnnotationDefList(AnnotationDefList list, Control owner, AnnotationDef item,
            IEnumerable<AnnotationDef> existing, object tag)
        {
            using (var dlg = new DefineAnnotationDlg(existing ?? list))
            {
                dlg.SetAnnotationDef(item);
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.GetAnnotationDef();
                }

                return null;
            }
        }

        private static ColorScheme EditItemColorSchemeList(ColorSchemeList list, Control owner, ColorScheme item, IEnumerable<ColorScheme> existing, object tag)
        {
            // < Edit List.. > selected
            using (var dlg = new EditCustomThemeDlg(item, existing ?? list))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.NewScheme;
                }
            }
            return null;
        }

        private static GroupComparisonDef EditItemGroupComparisonDefList(GroupComparisonDefList list, Control owner, GroupComparisonDef item, IEnumerable<GroupComparisonDef> existing, object tag)
        {
            using (var dlg = new EditGroupComparisonDlg(tag as IDocumentUIContainer, item ?? GroupComparisonDef.EMPTY, existing))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.GroupComparisonDef;
                }
            }
            return null;
        }

        private static ListData EditItemListDefList(ListDefList list, Control owner, ListData item, IEnumerable<ListData> existing, object tag)
        {
            using (var dlg = new ListDesigner(item, existing))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.GetListDef();
                }
            }
            return null;
        }

        private static MetadataRuleSet EditItemMetadataRuleSetList(MetadataRuleSetList list, Control owner, MetadataRuleSet item, IEnumerable<MetadataRuleSet> existing, object tag)
        {
            var document = (SrmDocument) tag;
            using (var dlg = new MetadataRuleSetEditor(document, item, existing))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.MetadataRuleSet;
                }
                else
                {
                    return null;
                }
            }
        }

        private static RemoteAccount EditItemRemoteAccountList(RemoteAccountList list, Control owner, RemoteAccount item, IEnumerable<RemoteAccount> existing, object tag)
        {
            using (EditRemoteAccountDlg editRemoteAccountDlg = new EditRemoteAccountDlg(item, existing ?? list))
            {
                if (editRemoteAccountDlg.ShowDialog(owner) == DialogResult.OK)
                    return editRemoteAccountDlg.GetRemoteAccount();

                return null;
            }
        }
    }
}
