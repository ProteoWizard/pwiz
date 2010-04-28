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
using System.Xml.Serialization;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using System.Windows.Forms;

namespace pwiz.Skyline.Properties
{    
    // This class allows you to handle specific events on the settings class:
    //  The SettingChanging event is raised before a setting's value is changed.
    //  The PropertyChanged event is raised after a setting's value is changed.
    //  The SettingsLoaded event is raised after the setting values are loaded.
    //  The SettingsSaving event is raised before the setting values are saved.
    public sealed partial class Settings
    {
        public Settings()
        {
            // this.SettingChanging += this.SettingChangingEventHandler;
            
            SettingsSaving += SettingsSavingEventHandler;
            
        }

        /*
        private void SettingChangingEventHandler(object sender, System.Configuration.SettingChangingEventArgs e)
        {
            // Add code to handle the SettingChangingEvent event here.
        }        
        */
        
        private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SpectralLibraryList.RemoveDocumentLocalLibraries();
        }

        [System.Configuration.UserScopedSettingAttribute]
        public List<string> MruList
        {
            get
            {
                if (this["MruList"] == null)
                    MruList = new List<string>();
                return (List<string>)(this["MruList"]);
            }
            set
            {
                this["MruList"] = value;
            }
        }

        [System.Configuration.UserScopedSettingAttribute]
        public MethodTemplateList ExportMethodTemplateList
        {
            get
            {
                if (this["ExportMethodTemplateList"] == null)
                    ExportMethodTemplateList = new MethodTemplateList();
                return (MethodTemplateList)(this["ExportMethodTemplateList"]);
            }
            set
            {
                this["ExportMethodTemplateList"] = value;
            }
        }

        public Enzyme GetEnzymeByName(string name)
        {
            Enzyme enzyme;
            if (!EnzymeList.TryGetValue(name, out enzyme))
            {
                enzyme = EnzymeList.Count == 0 ?
                    EnzymeList.GetDefault() : EnzymeList[0];
            }
            return enzyme;
        }

        [System.Configuration.UserScopedSettingAttribute]
        public GridColumnsList GridColumnsList
        {
            get
            {
                if (this["GridColumnsList"] == null)
                {
                    var list = new GridColumnsList();
                    GridColumnsList = list;
                }
                return ((GridColumnsList)(this["GridColumnsList"]));
            }
            set
            {
                this["GridColumnsList"] = value;
            }
        }
        [System.Configuration.UserScopedSettingAttribute]
        public EnzymeList EnzymeList
        {
            get
            {
                if (this[typeof(EnzymeList).Name] == null)
                {
                    EnzymeList list = new EnzymeList();
                    list.AddDefaults();
                    EnzymeList = list;
                }
                return ((EnzymeList)(this[typeof(EnzymeList).Name]));
            }
            set
            {
                this[typeof(EnzymeList).Name] = value;
            }
        }

        public PeptideExcludeRegex GetPeptideExclusionByName(string name)
        {
            PeptideExcludeRegex exclusion;
            if (!PeptideExcludeList.TryGetValue(name, out exclusion))
                exclusion = new PeptideExcludeRegex("Unknown", "");
            return exclusion;
        }

        [System.Configuration.UserScopedSettingAttribute]
        public PeptideExcludeList PeptideExcludeList
        {
            get
            {
                if (this[typeof(PeptideExcludeList).Name] == null)
                {
                    PeptideExcludeList list = new PeptideExcludeList();
                    list.AddDefaults();
                    PeptideExcludeList = list;
                }
                return ((PeptideExcludeList)(this[typeof(PeptideExcludeList).Name]));
            }
            set
            {
                this[typeof(PeptideExcludeList).Name] = value;
            }
        }

        public LibrarySpec GetSpectralLibraryByName(string name)
        {
            LibrarySpec spec;
            if (!SpectralLibraryList.TryGetValue(name, out spec))
                spec = null;
            return spec;
        }

        [System.Configuration.UserScopedSettingAttribute]
        public SpectralLibraryList SpectralLibraryList
        {
            get
            {
                if (this[typeof(SpectralLibraryList).Name] == null)
                {
                    SpectralLibraryList list = new SpectralLibraryList();
                    list.AddDefaults();
                    SpectralLibraryList = list;
                }
                return ((SpectralLibraryList)(this[typeof(SpectralLibraryList).Name]));
            }
            set
            {
                this[typeof(SpectralLibraryList).Name] = value;
            }
        }
        [System.Configuration.UserScopedSettingAttribute]
        public BackgroundProteomeList BackgroundProteomeList
        {
            get
            {

                BackgroundProteomeList list = this[typeof (BackgroundProteomeList).Name] as BackgroundProteomeList;
                if (list == null)
                {
                    list = new BackgroundProteomeList();
                    list.AddDefaults();
                    BackgroundProteomeList = list;
                }
                else
                {
                    list.EnsureDefault();
                }
                return list;
            }
            set
            {
                this[typeof (BackgroundProteomeList).Name] = value;
            }
        }

        public StaticMod GetStaticModByName(string name)
        {
            StaticMod mod;
            if (!StaticModList.TryGetValue(name, out mod))
                mod = null;
            return mod;
        }

        [System.Configuration.UserScopedSettingAttribute]
        public StaticModList StaticModList
        {
            get
            {
                if (this[typeof(StaticModList).Name] == null)
                {
                    StaticModList list = new StaticModList();
                    list.AddDefaults();
                    StaticModList = list;
                }
                return ((StaticModList)(this[typeof(StaticModList).Name]));
            }
            set
            {
                this[typeof(StaticModList).Name] = value;
            }
        }

        public StaticMod GetHeavyModByName(string name)
        {
            StaticMod mod;
            if (!HeavyModList.TryGetValue(name, out mod))
                mod = null;
            return mod;
        }

        [System.Configuration.UserScopedSettingAttribute]
        public HeavyModList HeavyModList
        {
            get
            {
                if (this[typeof(HeavyModList).Name] == null)
                {
                    HeavyModList list = new HeavyModList();
                    list.AddDefaults();
                    HeavyModList = list;
                }
                return ((HeavyModList)(this[typeof(HeavyModList).Name]));
            }
            set
            {
                this[typeof(HeavyModList).Name] = value;
            }
        }

        public CollisionEnergyRegression GetCollisionEnergyByName(string name)
        {
            CollisionEnergyRegression regression;
            if (!CollisionEnergyList.TryGetValue(name, out regression))
                regression = CollisionEnergyList.GetDefault();
            return regression;
        }

        [System.Configuration.UserScopedSettingAttribute]
        public CollisionEnergyList CollisionEnergyList
        {
            get
            {
                if (this[typeof(CollisionEnergyList).Name] == null)
                {
                    CollisionEnergyList list = new CollisionEnergyList();
                    list.AddDefaults();
                    CollisionEnergyList = list;
                }
                return ((CollisionEnergyList)(this[typeof(CollisionEnergyList).Name]));
            }
            set
            {
                this[typeof(CollisionEnergyList).Name] = value;
            }
        }

        public DeclusteringPotentialRegression GetDeclusterPotentialByName(string name)
        {
            // Null return is valid for this list, and means no declustring potential
            // calculation should be applied.
            DeclusteringPotentialRegression regression;
            if (DeclusterPotentialList.TryGetValue(name, out regression))
            {
                if (regression.GetKey() == RetentionTimeList.GetDefault().GetKey())
                    regression = null;
            }
            return regression;
        }

        [System.Configuration.UserScopedSettingAttribute]
        public DeclusterPotentialList DeclusterPotentialList
        {
            get
            {
                DeclusterPotentialList list = (DeclusterPotentialList) this[typeof(DeclusterPotentialList).Name];
                if (list == null)
                {
                    list = new DeclusterPotentialList();
                    list.AddDefaults();
                    DeclusterPotentialList = list;
                }
                else
                {
                    list.EnsureDefault();
                }
                return list;
            }
            set
            {
                this[typeof(DeclusterPotentialList).Name] = value;
            }
        }

        public RetentionTimeRegression GetRetentionTimeByName(string name)
        {
            // Null return is valid for this list, and means no retention time
            // calculation should be applied.
            RetentionTimeRegression regression;
            if (RetentionTimeList.TryGetValue(name, out regression))
            {
                if (regression.GetKey() == RetentionTimeList.GetDefault().GetKey())
                    regression = null;
            }
            return regression;
        }

        [System.Configuration.UserScopedSettingAttribute]
        public RetentionTimeList RetentionTimeList
        {
            get
            {
                RetentionTimeList list = (RetentionTimeList) this[typeof(RetentionTimeList).Name];
                if (list == null)
                {
                    list = new RetentionTimeList();
                    list.AddDefaults();
                    RetentionTimeList = list;
                }
                else
                {
                    list.EnsureDefault();
                }
                return list;
            }
            set
            {
                this[typeof(RetentionTimeList).Name] = value;
            }
        }

        [System.Configuration.UserScopedSettingAttribute]
        public SrmSettingsList SrmSettingsList
        {
            get
            {
                if (this[typeof(SrmSettingsList).Name] == null)
                {
                    SrmSettingsList list = new SrmSettingsList();
                    list.AddDefaults();
                    SrmSettingsList = list;
                }
                return ((SrmSettingsList)(this[typeof(SrmSettingsList).Name]));
            }
            set
            {
                this[typeof(SrmSettingsList).Name] = value;
            }
        }

        public ReportSpec GetReportSpecByName(string name)
        {
            ReportSpec reportSpec;
            if (!ReportSpecList.TryGetValue(name, out reportSpec))
                reportSpec = null;
            return reportSpec;
        }

        [System.Configuration.UserScopedSettingAttribute]
        public ReportSpecList ReportSpecList
        {
            get
            {
                ReportSpecList list = (ReportSpecList)this[typeof(ReportSpecList).Name];
                if (list == null)
                {
                    list = new ReportSpecList();
                    list.AddDefaults();
                    ReportSpecList = list;
                }
                return list;
            }
            set
            {
                this[typeof(ReportSpecList).Name] = value;
            }
        }

        [System.Configuration.UserScopedSettingAttribute]
        public AnnotationDefList AnnotationDefList
        {
            get
            {
                var list = (AnnotationDefList) this["AnnotationDefList"];
                if (list == null)
                {
                    list = new AnnotationDefList();
                    list.AddDefaults();
                    AnnotationDefList = list;
                }
                return list;
            }
            set
            {
                this["AnnotationDefList"] = value;
            }
        }
    }

    /// <summary>
    /// Class wrapper around <see cref="XmlMappedList{TKey,TValue}"/> to
    /// improve XML serialization naming of the list.
    /// </summary>
    public sealed class MethodTemplateList : XmlMappedList<string, MethodTemplateFile>
    {        
    }

    public sealed class EnzymeList : SettingsList<Enzyme>
    {
        public static Enzyme GetDefault()
        {
            return new Enzyme("Trypsin", "KR", "P");
        }

        public override IEnumerable<Enzyme> GetDefaults(int revisionIndex)
        {
            return new[]
                {
                    GetDefault(),
                    new Enzyme("Trypsin/P", "KR", ""),
                    new Enzyme("TrypsinK", "K", "P"),
                    new Enzyme("TrypsinR", "R", "P"),
                    new Enzyme("Chymotrypsin", "FWYM", "P"),
                    new Enzyme("ArgC", "R", "P"),
                    new Enzyme("AspN", "D", "", SequenceTerminus.N),
                    new Enzyme("Clostripain", "R", ""),
                    new Enzyme("CNBr", "M", "P"),
                    new Enzyme("Elastase", "GVLIA", "P"),
                    new Enzyme("Formic Acid", "D", "P"),
                    new Enzyme("GluC", "DE", "P"),
                    new Enzyme("GluC bicarb", "E", "P"),
                    new Enzyme("Iodosobenzoate", "W", ""),
                    new Enzyme("LysC", "K", "P"),
                    new Enzyme("LysC/P", "K", ""),
                    new Enzyme("LysN", "K", "", SequenceTerminus.N),
                    new Enzyme("LysN promisc", "KASR", "", SequenceTerminus.N),
                    new Enzyme("PepsinA", "FL", ""),
                    new Enzyme("Protein endopeptidase", "P", ""),
                    new Enzyme("Staph protease", "E", ""),
                    new Enzyme("Trypsin-CNBr", "KRM", "P"),
                    new Enzyme("Trypsin-GluC", "DEKR", "P")
                };
        }

        public override Enzyme EditItem(Control owner, Enzyme item, IEnumerable<Enzyme> existing, object tag)
        {
            EditEnzymeDlg editEnzyme = new EditEnzymeDlg(existing ?? this) { Enzyme = item };
            if (editEnzyme.ShowDialog(owner) == DialogResult.OK)
                return editEnzyme.Enzyme;

            return null;
        }

        public override Enzyme CopyItem(Enzyme item)
        {
            return (Enzyme) item.ChangeName(string.Empty);
        }

        public override string Title { get { return "Edit Enzymes"; } }

        public override string Label { get { return "En&zymes:"; } }
    }

    public sealed class PeptideExcludeList : SettingsList<PeptideExcludeRegex>
    {
        public override IEnumerable<PeptideExcludeRegex> GetDefaults(int revisionIndex)
        {
            return new[]
                {
                    new PeptideExcludeRegex("Cys", "[C]"),
                    new PeptideExcludeRegex("Met", "[M]"),
                    new PeptideExcludeRegex("His", "[H]"),
                    new PeptideExcludeRegex("NXT/NXS", "N.[TS]"),
                    new PeptideExcludeRegex("RP/KP", "[RK]P")
                };
        }

        public override PeptideExcludeRegex EditItem(Control owner, PeptideExcludeRegex item,
            IEnumerable<PeptideExcludeRegex> existing, object tag)
        {
            EditExclusionDlg editExclusion = new EditExclusionDlg(existing ?? this) { Exclusion = item };
            if (editExclusion.ShowDialog(owner) == DialogResult.OK)
                return editExclusion.Exclusion;

            return null;
        }

        public override PeptideExcludeRegex CopyItem(PeptideExcludeRegex item)
        {
            return (PeptideExcludeRegex) item.ChangeName(string.Empty);
        }

        public override string Title { get { return "Edit Exclusions"; } }

        public override string Label { get { return "&Exclusions:"; } }
    }

    public sealed class SpectralLibraryList : SettingsList<LibrarySpec>
    {
        public override IEnumerable<LibrarySpec> GetDefaults(int revisionIndex)
        {
            return new LibrarySpec[0];
        }

        public override LibrarySpec EditItem(Control owner, LibrarySpec item,
            IEnumerable<LibrarySpec> existing, object tag)
        {
            EditLibraryDlg editLibrary = new EditLibraryDlg(existing ?? this) { LibrarySpec = item };
            if (editLibrary.ShowDialog(owner) == DialogResult.OK)
                return editLibrary.LibrarySpec;

            return null;
        }

        public override LibrarySpec CopyItem(LibrarySpec item)
        {
            return (LibrarySpec) item.ChangeName(string.Empty);
        }

        public void RemoveDocumentLocalLibraries()
        {
            foreach (LibrarySpec librarySpec in this.ToArray())
            {
                if (librarySpec.IsDocumentLocal)
                    Remove(librarySpec);
            }            
        }

        public override string Title { get { return "Edit Libraries"; } }

        public override string Label { get { return "&Libraries:"; } }

        protected override IXmlElementHelper<LibrarySpec>[] GetXmlElementHelpers()
        {
            return PeptideLibraries.LibrarySpecXmlHelpers;
        }
    }

    public sealed class BackgroundProteomeList : SettingsList<BackgroundProteomeSpec>
    {
        private static readonly BackgroundProteomeSpec NONE = new BackgroundProteomeSpec("None", "");

        public static BackgroundProteomeSpec GetDefault()
        {
            return NONE;
        }

        public override IEnumerable<BackgroundProteomeSpec> GetDefaults(int revisionIndex)
        {
            return new[] {GetDefault()};
        }

        public void EnsureDefault()
        {
            // Make sure the choice of no background proteome is present.
            BackgroundProteomeSpec defaultElement = GetDefault();
            if (Count == 0 || this[0].GetKey() != defaultElement.GetKey())
                Insert(0, defaultElement);            
        }

        public override BackgroundProteomeSpec EditItem(Control owner, BackgroundProteomeSpec item,
            IEnumerable<BackgroundProteomeSpec> existing, object tag)
        {
            var editBackgroundProteomeDlg = new BuildBackgroundProteomeDlg(existing ?? this)
                                                                      {BackgroundProteomeSpec = item};
            if (editBackgroundProteomeDlg.ShowDialog(owner) == DialogResult.OK)
            {
                return editBackgroundProteomeDlg.BackgroundProteomeSpec;
            }
            return null;
        }

        public override BackgroundProteomeSpec CopyItem(BackgroundProteomeSpec item)
        {
            return (BackgroundProteomeSpec) item.ChangeName(string.Empty);
        }

        public override String Title { get { return "Edit Background Proteomes"; } }
        public override String Label { get { return "&Background Proteomes:"; } }

        protected override IXmlElementHelper<BackgroundProteomeSpec>[] GetXmlElementHelpers()
        {
            return BackgroundProteomeSpec.BackgroundProteomeHelpers;
        }

        public BackgroundProteomeSpec GetBackgroundProteomeSpec(String name)
        {
            foreach (var backgroundProteomeSpec in this)
            {
                if (backgroundProteomeSpec.Name == name)
                {
                    return backgroundProteomeSpec;
                }
            }
            return null;
        }

        public override bool ExcludeDefault
        {
            get { return true; }
        }
    }

    public sealed class StaticModList : SettingsList<StaticMod>
    {
        public static StaticMod[] GetDefaultsOn()
        {
            return new[]
                {
                    new StaticMod("Carbamidomethyl Cysteine", 'C', "C2H3ON")
                };            
        }

        public override IEnumerable<StaticMod> GetDefaults(int revisionIndex)
        {
            return GetDefaultsOn();
        }

        public override StaticMod EditItem(Control owner, StaticMod item,
            IEnumerable<StaticMod> existing, object tag)
        {
            EditStaticModDlg editMod = new EditStaticModDlg(existing ?? this, false) { Modification = item };
            if (editMod.ShowDialog(owner) == DialogResult.OK)
                return editMod.Modification;

            return null;
        }

        public override StaticMod CopyItem(StaticMod item)
        {
            return (StaticMod) item.ChangeName(string.Empty);
        }

        public override string Title { get { return "Edit Static Modifications"; } }

        public override string Label { get { return "&Modifications:"; } }
    }

    public sealed class HeavyModList : SettingsList<StaticMod>
    {
        public static StaticMod[] GetDefaultsOn()
        {
            return new StaticMod[0];
        }

        public override IEnumerable<StaticMod> GetDefaults(int revisionIndex)
        {
            return GetDefaultsOn();
        }

        public override StaticMod EditItem(Control owner, StaticMod item,
            IEnumerable<StaticMod> existing, object tag)
        {
            EditStaticModDlg editMod = new EditStaticModDlg(existing ?? this, true)
                                           {
                                               Text = "Edit Heavy Modification",
                                               Modification = item
                                           };
            if (editMod.ShowDialog(owner) == DialogResult.OK)
                return editMod.Modification;

            return null;
        }

        public override StaticMod CopyItem(StaticMod item)
        {
            return (StaticMod) item.ChangeName(string.Empty);
        }

        public override string Title { get { return "Edit Heavy Modifications"; } }

        public override string Label { get { return "&Modifications:"; } }
    }

    public sealed class CollisionEnergyList : SettingsList<CollisionEnergyRegression>
    {
        public override int RevisionIndexCurrent { get { return 2; } }

        public static CollisionEnergyRegression GetDefault()
        {
            var thermoRegressions = new[]
                                {
                                    new ChargeRegressionLine(2, 0.03, 2.905),
                                    new ChargeRegressionLine(3, 0.038, 2.281)
                                };
            return new CollisionEnergyRegression("Thermo TSQ Vantage", thermoRegressions);
        }

        public override IEnumerable<CollisionEnergyRegression> GetDefaults(int revisionIndex)
        {
            switch (revisionIndex)
            {
                case 0: // v0.5
                    return new[]
                        {
                            new CollisionEnergyRegression("Thermo", new []
                                {
                                    new ChargeRegressionLine(2, 0.034, 3.314),
                                    new ChargeRegressionLine(3, 0.044, 3.314)
                                }), 
                            new CollisionEnergyRegression("ABI", new[]
                                { new ChargeRegressionLine(2, 0.0431, 4.7556), }),
                            new CollisionEnergyRegression("Agilent", new[]
                                { new ChargeRegressionLine(2, 0.036, -4.8), }),
                            new CollisionEnergyRegression("Waters", new[]
                                { new ChargeRegressionLine(2, 0.034, 3.314), }),
                        };
                case 1:    // v0.6
                    return new[]
                        {
                            new CollisionEnergyRegression("Thermo TSQ Vantage", new[]
                                {
                                    new ChargeRegressionLine(2, 0.03, 2.786),
                                    new ChargeRegressionLine(3, 0.038, 2.183)
                                }),
                            new CollisionEnergyRegression("Thermo TSQ Ultra", new []
                                {
                                    new ChargeRegressionLine(2, 0.035, 1.643),
                                    new ChargeRegressionLine(3, 0.037, 3.265)
                                }), 
                            new CollisionEnergyRegression("ABI 4000 QTrap", new []
                                {
                                    new ChargeRegressionLine(2, 0.057, 4.453),
                                    new ChargeRegressionLine(3, 0.03, 7.692)
                                }), 
                            new CollisionEnergyRegression("Agilent", new[]
                                { new ChargeRegressionLine(2, 0.036, -4.8), }),
                            new CollisionEnergyRegression("Waters", new[]
                                { new ChargeRegressionLine(2, 0.034, 3.314), }),
                        };
                default:    // v0.6 - fix
                    return new[]
                        {
                            GetDefault(), 
                            new CollisionEnergyRegression("Thermo TSQ Ultra", new []
                                {
                                    new ChargeRegressionLine(2, 0.036, 0.954),
                                    new ChargeRegressionLine(3, 0.037, 3.525)
                                }), 
                            new CollisionEnergyRegression("ABI 4000 QTrap", new []
                                {
                                    new ChargeRegressionLine(2, 0.057, -4.265),
                                    new ChargeRegressionLine(3, 0.031, 7.082)
                                }), 
                            new CollisionEnergyRegression("Agilent", new[]
                                { new ChargeRegressionLine(2, 0.036, -4.8), }),
                            new CollisionEnergyRegression("Waters", new[]
                                { new ChargeRegressionLine(2, 0.034, 3.314), }),
                        };
            }
        }

        public override CollisionEnergyRegression EditItem(Control owner, CollisionEnergyRegression item,
            IEnumerable<CollisionEnergyRegression> existing, object tag)
        {
            EditCEDlg editCE = new EditCEDlg(existing ?? this) { Regression = item };
            if (editCE.ShowDialog(owner) == DialogResult.OK)
                return editCE.Regression;

            return null;
        }

        public override CollisionEnergyRegression CopyItem(CollisionEnergyRegression item)
        {
            return (CollisionEnergyRegression) item.ChangeName(string.Empty);
        }

        public override string Title { get { return "Edit Collision Energy Regressions"; } }

        public override string Label { get { return "&Collision Energy Regression:"; } }
    }

    public sealed class DeclusterPotentialList : SettingsList<DeclusteringPotentialRegression>
    {
        private static readonly DeclusteringPotentialRegression NONE =
            new DeclusteringPotentialRegression("None", 0, 0);

        public static DeclusteringPotentialRegression GetDefault()
        {
            return NONE;
        }

        public override IEnumerable<DeclusteringPotentialRegression> GetDefaults(int revisionIndex)
        {
            return new[]
            {
               GetDefault(),
               new DeclusteringPotentialRegression("ABI", 0.0729, 31.117), 
            };
        }

        public void EnsureDefault()
        {
            // Make sure the choice of no retention time regression is present.
            DeclusteringPotentialRegression defaultElement = GetDefault();
            if (Count == 0 || this[0].GetKey() != defaultElement.GetKey())
                Insert(0, defaultElement);
        }

        public override DeclusteringPotentialRegression EditItem(Control owner, DeclusteringPotentialRegression item,
            IEnumerable<DeclusteringPotentialRegression> existing, object tag)
        {
            EditDPDlg editDP = new EditDPDlg(existing ?? this) { Regression = item };
            if (editDP.ShowDialog(owner) == DialogResult.OK)
                return editDP.Regression;

            return null;
        }

        public override DeclusteringPotentialRegression CopyItem(DeclusteringPotentialRegression item)
        {
            return (DeclusteringPotentialRegression) item.ChangeName(string.Empty);
        }

        public override string Title { get { return "Edit Declustering Potential Regressions"; } }

        public override string Label { get { return "&Declustering Potential Regressions:"; } }

        public override bool ExcludeDefault { get { return true; } }
    }

    public sealed class RetentionTimeList : SettingsList<RetentionTimeRegression>
    {
        private static readonly RetentionTimeRegression NONE =
            new RetentionTimeRegression("None", null, 0, 0, 0, new MeasuredRetentionTime[0]);

        public static RetentionTimeRegression GetDefault()
        {
            return NONE;
        }

        public override IEnumerable<RetentionTimeRegression> GetDefaults(int revisionIndex)
        {
            return new[] { GetDefault() };
        }

        public void EnsureDefault()
        {
            // Make sure the choice of no retention time regression is present.
            RetentionTimeRegression defaultElement = GetDefault();
            if (Count == 0 || this[0].GetKey() != defaultElement.GetKey())
                Insert(0, defaultElement);            
        }

        public override RetentionTimeRegression EditItem(Control owner, RetentionTimeRegression item,
            IEnumerable<RetentionTimeRegression> existing, object tag)
        {
            EditRTDlg editRT = new EditRTDlg(existing ?? this) { Regression = item };
            if (editRT.ShowDialog() == DialogResult.OK)
                return editRT.Regression;

            return null;
        }

        public override RetentionTimeRegression CopyItem(RetentionTimeRegression item)
        {
            return (RetentionTimeRegression) item.ChangeName(string.Empty);
        }

        public override string Title { get { return "Edit Retention Time Regressions"; } }

        public override string Label { get { return "&Retention Time Regression:"; } }

        public override bool ExcludeDefault { get { return true; } }
    }

    public sealed class SrmSettingsList : SettingsListBase<SrmSettings>, IListSerializer<SrmSettings>
    {
        private static readonly SrmSettings DEFAULT = new SrmSettings
            (
                DefaultName,
                new PeptideSettings
                (
                    EnzymeList.GetDefault(),
                    new DigestSettings(0, false),
                    new PeptidePrediction(null, true, PeptidePrediction.DEFAULT_MEASURED_RT_WINDOW),
                    new PeptideFilter
                    (
                        25,  // ExcludeNTermAAs
                         8,  // MinPeptideLength
                        25,  // MaxPeptideLength
                        new PeptideExcludeRegex[0], // Exclusions
                        true // AutoSelect
                    ),
                    new PeptideLibraries
                        (
                        PeptidePick.library,    // PeptidePick
                        null,                   // PeptideRankId
                        null,                   // PeptideCount
                        new LibrarySpec[0],     // LibrarySpecs
                        new Library[0]          // Libraries
                        ), 
                    new PeptideModifications(StaticModList.GetDefaultsOn(), HeavyModList.GetDefaultsOn()),
                    BackgroundProteome.NONE
                ),
                new TransitionSettings
                (
                    new TransitionPrediction
                    (
                        MassType.Monoisotopic, // PrecursorMassType
                        MassType.Monoisotopic, // FragmentMassType
                        CollisionEnergyList.GetDefault(),
                        null,
                        OptimizedMethodType.None
                    ),
                    new TransitionFilter
                    (
                        new[] { 2 }, // PrecursorCharges
                        new[] { 1 }, // ProductCharges
                        new[] { IonType.y }, // FragmentTypes
                        "m/z > precursor", // FragmentRangeFirst
                        "3 ions",       // FragmentRangeLast
                        true,  // IncludeNProline
                        false, // IncludeCGluAsp
                        true   // AutoSelect
                    ),
                    new TransitionLibraries
                    (
                        0.5,    // IonMatchTolerance
                        3,      // IonCount
                        TransitionLibraryPick.all  // Pick
                    ),
                    new TransitionIntegration(), 
                    new TransitionInstrument
                    (
                        50,   // MinMz
                        1500, // MaxMz
                        TransitionInstrument.DEFAULT_MZ_MATCH_TOLERANCE, // MzMatchTolerance
                        null  // MaxTransitions
                    )
                ),
                new DataSettings(new AnnotationDef[0])
            );

        public static string DefaultName
        {
            get { return "Default"; }
        }

        public static SrmSettings GetDefault()
        {
            return DEFAULT;
        }

        public override IEnumerable<SrmSettings> GetDefaults(int revisionIndex)
        {
            return new[] { GetDefault() };
        }

        public override string Title { get { return "Edit Settings"; } }

        public override string Label { get { return "&Saved Settings:"; } }

        public Type SerialType { get { return typeof(SrmSettingsList); } }

        public ICollection<SrmSettings> CreateEmptyList()
        {
            return new SrmSettingsList();
        }
    }

    public interface IReportDatabaseProvider
    {
        Database GetDatabase(Control owner);
    }

    public sealed class ReportSpecList : SettingsList<ReportSpec>, IListSerializer<ReportSpec>
    {
        public override IEnumerable<ReportSpec> GetDefaults(int revisionIndex)
        {
            Type tablePep = typeof (DbPeptide);
            Type tablePepRes = typeof (DbPeptideResult);
            Type tableTran = typeof (DbTransition);
            Type tableTranRes = typeof (DbTransitionResult);

            return new[]
                       {
                new ReportSpec("Peptide Ratio Results", 
                    new QueryDef
                        {
                            Select = new[] {
                               new ReportColumn(tablePep, "Sequence"),
                               new ReportColumn(tablePep, "Protein", "Name"),
                               new ReportColumn(tablePepRes, "ProteinResult","ReplicateName"),
                               new ReportColumn(tablePepRes, "PeptidePeakFoundRatio"),
                               new ReportColumn(tablePepRes, "PeptideRetentionTime"),
                               new ReportColumn(tablePepRes, "RatioToStandard"),
                           }
                        }),
                new ReportSpec("Peptide RT Results",
                    new QueryDef 
                    {
                        Select = new[] {
                            new ReportColumn(tablePep,"Sequence"), 
                            new ReportColumn(tablePep,"Protein","Name"), 
                            new ReportColumn(tablePepRes, "ProteinResult","ReplicateName"), 
                            new ReportColumn(tablePep,"PredictedRetentionTime"), 
                            new ReportColumn(tablePepRes, "PeptideRetentionTime"), 
                            new ReportColumn(tablePepRes, "PeptidePeakFoundRatio"),
                    }}),
                new ReportSpec("Transition Results",
                    new QueryDef
                    {
                        Select = new[] {
                            new ReportColumn(tableTran,"Precursor","Peptide","Sequence"),
                            new ReportColumn(tableTran,"Precursor","Peptide","Protein","Name"), 
                            new ReportColumn(tableTranRes, "PrecursorResult","PeptideResult","ProteinResult","ReplicateName"), 
                            new ReportColumn(tableTran,"Precursor","Mz"), 
                            new ReportColumn(tableTran,"Precursor","Charge"), 
                            new ReportColumn(tableTran,"ProductMz"), 
                            new ReportColumn(tableTran,"ProductCharge"),
                            new ReportColumn(tableTran,"FragmentIon"),
                            new ReportColumn(tableTranRes, "RetentionTime"),
                            new ReportColumn(tableTranRes, "Area"),
                            new ReportColumn(tableTranRes, "Background"),
                            new ReportColumn(tableTranRes, "PeakRank"), 
                    }})
            };
        }

        public override ReportSpec EditItem(Control owner, ReportSpec item,
            IEnumerable<ReportSpec> existing, object tag)
        {
            PivotReportDlg editReport = new PivotReportDlg(existing ?? this);

            var databaseProvider = tag as IReportDatabaseProvider;
            if (databaseProvider != null)
                editReport.SetDatabase(databaseProvider.GetDatabase(owner));
            editReport.SetReportSpec(item);
            if (editReport.ShowDialog(owner) == DialogResult.OK)
            {
                return editReport.GetReportSpec();
            }

            return null;
        }

        public override ReportSpec CopyItem(ReportSpec item)
        {
            return (ReportSpec) item.ChangeName(string.Empty);
        }

        public override string Title { get { return "Edit Reports"; } }

        public override string Label { get { return "&Report:"; } }

        public Type SerialType { get { return typeof(ReportSpecList); } }

        public ICollection<ReportSpec> CreateEmptyList()
        {
            return new ReportSpecList();
        }
    }


    public sealed class GridColumnsList : XmlMappedList<string, GridColumns>
    {
    }

    public sealed class AnnotationDefList : SettingsList<AnnotationDef>, IListSerializer<AnnotationDef>
    {
        public override IEnumerable<AnnotationDef> GetDefaults(int revisionIndex)
        {
            return new AnnotationDef[0];
        }

        public override AnnotationDef EditItem(Control owner, AnnotationDef item,
            IEnumerable<AnnotationDef> existing, object tag)
        {
            var dlg = new DefineAnnotationDlg(existing ?? this);
            dlg.SetAnnotationDef(item);
            if (dlg.ShowDialog(owner) == DialogResult.OK)
            {
                return dlg.GetAnnotationDef();
            }

            return null;
        }

        public override AnnotationDef CopyItem(AnnotationDef item)
        {
            return (AnnotationDef)item.ChangeName(string.Empty);
        }

        public override string Title { get { return "Define Annotations"; } }

        public override string Label { get { return "&Annotations:"; } }

        public Type SerialType { get { return typeof(AnnotationDef); } }

        public ICollection<AnnotationDef> CreateEmptyList()
        {
            return new AnnotationDefList();
        }
    }

    public abstract class SettingsList<T>
        : SettingsListBase<T>, IItemEditor<T>
        where T : IKeyContainer<string>, IXmlSerializable
    {
        #region IItemEditor<T> Members

        public T NewItem(Control owner, IEnumerable<T> existing, object tag)
        {
            return EditItem(owner, default(T), existing, tag);
        }

        public abstract T EditItem(Control owner, T item, IEnumerable<T> existing, object tag);

        public abstract T CopyItem(T item);

        #endregion

        public override bool AllowReset { get { return true; } }

        public override bool ExcludeDefault { get { return false; } }
    }

    public abstract class SettingsListBase<T>
        : XmlMappedList<string, T>, IListDefaults<T>, IListEditor<T>, IListEditorSupport
        where T : IKeyContainer<string>, IXmlSerializable
    {
        public virtual void AddDefaults()
        {
            AddRange(GetDefaults());
        }

        public IEnumerable<T> GetDefaults()
        {
            return GetDefaults(RevisionIndexCurrent);
        }

        #region IListDefaults<TValue> Members

        public virtual int RevisionIndexCurrent { get { return 0; } }

        public abstract IEnumerable<T> GetDefaults(int revisionIndex);

        #endregion

        #region IListEditor<T> Members

        public IEnumerable<T> EditList(Control owner, object tag)
        {
            var dlg = new EditListDlg<SettingsListBase<T>, T>(this, tag);
            if (dlg.ShowDialog(owner) == DialogResult.OK)
                return dlg.GetAll();
            return null;
        }

        #endregion

        #region IListEditorSupport Members

        public abstract string Title { get; }

        public abstract string Label { get; }

        public virtual bool AllowReset { get { return false; } }

        public virtual bool ExcludeDefault { get { return true; } }

        #endregion

        #region IXmlSerializable

        protected override void ValidateLoad()
        {
            // If the current revision index has changed, since the list was saved,
            // merge in any new elements that were added between the old version
            // and the new.
            if (RevisionIndex != RevisionIndexCurrent)
            {
                MergeDifferences(GetDefaults(RevisionIndexCurrent), GetDefaults(RevisionIndex));
                RevisionIndex = RevisionIndexCurrent;
            }
        }

        private void MergeDifferences(IEnumerable<T> itemsNew, IEnumerable<T> itemsOld)
        {
            var itemsOldArray = itemsOld.ToArray();
            foreach (var item in itemsNew)
            {
                var keyNew = item.GetKey();
                int i = itemsOldArray.IndexOf(itemOld => Equals(keyNew, itemOld.GetKey()));
                // If the item did not exist, add it.
                if (i == -1)
                    Add(item);
                // If the item has changed in the latest version
                else if (!Equals(item, itemsOldArray[i]))
                {
                    // And the item exists unchanged in the settings, then replace it.
                    if (Remove(itemsOldArray[i]))
                        Add(item);
                }
            }
        }

        #endregion
    }
}
