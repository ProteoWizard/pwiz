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
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Properties
{    
    // This class allows you to handle specific events on the settings class:
    //  The SettingChanging event is raised before a setting's value is changed.
    //  The PropertyChanged event is raised after a setting's value is changed.
    //  The SettingsLoaded event is raised after the setting values are loaded.
    //  The SettingsSaving event is raised before the setting values are saved.
    public sealed partial class Settings
    {
        /// <devdoc>
        /// Holds the original values of properties before they were modified by 
        /// this instance of Skyline.  It uses the same IEqualityComparer as
        /// <see cref="SettingsPropertyCollection"/>
        /// </devdoc>
        private readonly IDictionary<string, object> _originalSerializedValues
            = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

        public Settings()
        {
            // this.SettingChanging += this.SettingChangingEventHandler;
            
            SettingsSaving += SettingsSavingEventHandler;
            
        }

        public static void Init()
        {
            defaultInstance = ((Settings)(Synchronized(new Settings())));
        }

        public static void Release()
        {
            defaultInstance = null;
        }

        /*
        private void SettingChangingEventHandler(object sender, System.Configuration.SettingChangingEventArgs e)
        {
            // Add code to handle the SettingChangingEvent event here.
        }        
        */

        // This holds any exception that is generated during Settings.Default.Save.  Unfortunately,
        // System.Xml silently catches these exceptions, leaving us to wonder why settings were
        // not saved.  Now we catch the exception ourselves, and save it here, where it can be
        // rethrown after the Save call completes.
        public Exception SaveException { get; set; }

        private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SpectralLibraryList.RemoveDocumentLocalLibraries();
        }

        
        /// <summary>
        /// Reload settings that may have been changed by other instances of Skyline, but preserve
        /// the values of any settings that have been modified by this instance.
        /// </summary>
        public void ReloadAndMerge()
        {
            lock (this)
            {
                var modifiedValues = new List<KeyValuePair<string, object>>();
                foreach (var propertyName in _originalSerializedValues.Keys)
                {
                    object currentValue = this[propertyName];
                    if (IsModifiedFromOriginal(propertyName, currentValue))
                    {
                        modifiedValues.Add(new KeyValuePair<string, object>(propertyName, currentValue));
                    }
                }
                _originalSerializedValues.Clear();
                Reload();
                foreach (var pair in modifiedValues)
                {
                    this[pair.Key] = pair.Value;
                }
            }
        }

        /// <devdoc>
        /// Overridden so that the first time that a property is retrieved its original
        /// value gets remembered in <see cref="_originalSerializedValues"/>.
        /// </devdoc>
        public override object this[string propertyName]
        {
            get
            {
                lock (this)
                {
                    object value = base[propertyName];
                    RememberOriginalValue(propertyName, value);
                    return value;
                }
            }
            set
            {
                lock (this)
                {
                    if (!_originalSerializedValues.ContainsKey(propertyName))
                    {
                        RememberOriginalValue(propertyName, base[propertyName]);
                    }
                    base[propertyName] = value;
                }
            }
        }

        public LockMassParameters LockmassParameters
        {
            get
            {
                return new LockMassParameters(
                    LockMassPositive == 0 ? (double?) null : LockMassPositive,
                    LockMassNegative == 0 ? (double?) null : LockMassNegative,
                    LockMassTolerance == 0 ? (double?) null : LockMassTolerance);
            }
            set
            {
                LockMassPositive = (value == null) ? 0 : value.LockmassPositive ?? 0;
                LockMassNegative = (value == null) ? 0 : value.LockmassNegative ?? 0;
                LockMassTolerance = (value == null) ? 0 : value.LockmassTolerance ?? 0;
            }
        }

        /// <devdoc>
        /// If this is the first time we are accessing the property, remember its serialized value
        /// in <see cref="_originalSerializedValues"/>
        /// </devdoc>
        private void RememberOriginalValue(string propertyName, object value)
        {
            if (!_originalSerializedValues.ContainsKey(propertyName))
            {
                _originalSerializedValues.Add(propertyName, GetSerializedValue(propertyName, value));
            }
        }

        /// <devdoc>
        /// Returns the serialized form (usually a string) of the given property value.
        /// </devdoc>
        private object GetSerializedValue(string propertyName, object value)
        {
            var settingsProperty = Properties[propertyName];
            if (null == settingsProperty)
            {
                return null;
            }
            if (value is SpectralLibraryList)
            {
                // If it's a SpectralLibraryList, then remove DocumentLocal libraries before serializing.
                var spectralLibraryList = (SpectralLibraryList)value;
                var filteredSpectralLibraryList = new SpectralLibraryList();
                filteredSpectralLibraryList.AddRange(spectralLibraryList.Where(librarySpec => !librarySpec.IsDocumentLocal));
                value = filteredSpectralLibraryList;
            }
            var settingsPropertyValue = new SettingsPropertyValue(settingsProperty)
            {
                PropertyValue = value
            };
            return settingsPropertyValue.SerializedValue;
        }

        private bool IsModifiedFromOriginal(string propertyName, object currentValue)
        {
            object originalSerializedValue;
            if (!_originalSerializedValues.TryGetValue(propertyName, out originalSerializedValue))
            {
                return true;
            }
            return !Equals(originalSerializedValue, GetSerializedValue(propertyName, currentValue));
        }

        [UserScopedSettingAttribute]
        public ToolList ToolList
        {
            get
            {
                if (this["ToolList"] == null) // Not L10N
                {
                    var list = new ToolList();
                    list.AddDefaults();
                    ToolList = list;
                }
                return (ToolList)(this["ToolList"]); // Not L10N
            }
            set
            {
                this["ToolList"] = value; // Not L10N
            }
        }

        [UserScopedSettingAttribute]
        public SerializableDictionary<ProgramPathContainer,string> ToolFilePaths
        {
            get
            {
                if (this["ToolFilePaths"] == null) // Not L10N
                {
                    ToolFilePaths = new SerializableDictionary<ProgramPathContainer, string>();
                }
                return (SerializableDictionary<ProgramPathContainer, string>)(this["ToolFilePaths"]); // Not L10N
            }
            set
            {
                this["ToolFilePaths"] = value; // Not L10N
            }
        }

        [UserScopedSettingAttribute]
        public UniqueList<GraphTypeSummary> AreaGraphTypes
        {
            get
            {
                if (this["AreaGraphTypes"] == null) // Not L10N
                {
                    AreaGraphTypes = ShowPeakAreaGraph
                        ? new UniqueList<GraphTypeSummary> { Helpers.ParseEnum(AreaGraphType, GraphTypeSummary.replicate) }
                        : new UniqueList<GraphTypeSummary>();
                }

                return (UniqueList<GraphTypeSummary>)this["AreaGraphTypes"]; // Not L10N
            }
            set
            {
                value.CollectionChanged += (sender, args) =>
                {
                    if (AreaGraphTypes.Any())
                        AreaGraphType = AreaGraphTypes.First().ToString();
                };

                this["AreaGraphTypes"] = value; // Not L10N
            }
        }

        [UserScopedSettingAttribute]
        public UniqueList<GraphTypeSummary> RTGraphTypes
        {
            get
            {
                if (this["RTGraphTypes"] == null) // Not L10N
                {
                    RTGraphTypes = ShowRetentionTimeGraph
                        ? new UniqueList<GraphTypeSummary> { Helpers.ParseEnum(RTGraphType, GraphTypeSummary.replicate) }
                        : new UniqueList<GraphTypeSummary>();
                }

                return (UniqueList<GraphTypeSummary>)this["RTGraphTypes"]; // Not L10N
            }
            set
            {
                value.CollectionChanged += (sender, args) =>
                {
                    if (RTGraphTypes.Any())
                        RTGraphType = RTGraphTypes.First().ToString();
                };

                this["RTGraphTypes"] = value; // Not L10N
            }
        }

        [UserScopedSettingAttribute]
        public UniqueList<GraphTypeSummary> MassErrorGraphTypes
        {
            get
            {
                if (this["MassErrorGraphTypes"] == null) // Not L10N
                {
                    MassErrorGraphTypes = ShowMassErrorGraph
                        ? new UniqueList<GraphTypeSummary> { Helpers.ParseEnum(MassErrorGraphType, GraphTypeSummary.replicate) }
                        : new UniqueList<GraphTypeSummary>();
                }

                return (UniqueList<GraphTypeSummary>)this["MassErrorGraphTypes"]; // Not L10N
   
            }
            set
            {
                value.CollectionChanged += (sender, args) =>
                {
                    if (MassErrorGraphTypes.Any())
                        MassErrorGraphType = MassErrorGraphTypes.First().ToString();
                };

                this["MassErrorGraphTypes"] = value; // Not L10N
            }
        }

        [UserScopedSettingAttribute]
        public List<string> MruList
        {
            get
            {
                if (this["MruList"] == null) // Not L10N
                    MruList = new List<string>();
                return (List<string>)(this["MruList"]); // Not L10N
            }
            set
            {
                this["MruList"] = value; // Not L10N
            }
        }

        [UserScopedSettingAttribute]
        public List<string> StackTraceList
        {
            get
            {
                if (this["StackTraceList"] == null) // Not L10N
                    StackTraceList = new List<string>();
                return (List<string>)(this["StackTraceList"]); // Not L10N
            }
            set { this["StackTraceList"] = value; } // Not L10N
        }

        [UserScopedSettingAttribute]
        public ViewSpecList ViewSpecList
        {
            get
            {
                if (this["ViewSpecList"] == null) // Not L10N
                {
                    ViewSpecList = new ViewSpecList(new ViewSpec[0]);
                }
                return (ViewSpecList)this["ViewSpecList"]; // Not L10N
            }
            set { this["ViewSpecList"] = value; } // Not L10N
        }

        [UserScopedSettingAttribute]
        public PersistedViews PersistedViews
        {
            get
            {
                var persistedViews = (PersistedViews) this["PersistedViews"]; // Not L10N
                if (persistedViews == null)
                {
                    persistedViews = new PersistedViews((ReportSpecList)this["ReportSpecList"],// Not L10N
                        (ViewSpecList)this["ViewSpecList"], ToolList);// Not L10N
                    this["persistedViews"] = persistedViews; // Not L10N
                }
                return persistedViews;
            }
        }

        [UserScopedSettingAttribute]
        public MethodTemplateList ExportMethodTemplateList
        {
            get
            {
                if (this["ExportMethodTemplateList"] == null) // Not L10N
                    ExportMethodTemplateList = new MethodTemplateList();
                return (MethodTemplateList)(this["ExportMethodTemplateList"]); // Not L10N
            }
            set
            {
                this["ExportMethodTemplateList"] = value; // Not L10N
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

        [UserScopedSettingAttribute]
        public GridColumnsList GridColumnsList
        {
            get
            {
                if (this["GridColumnsList"] == null) // Not L10N
                {
                    var list = new GridColumnsList();
                    GridColumnsList = list;
                }
                return ((GridColumnsList)(this["GridColumnsList"])); // Not L10N
            }
            set
            {
                this["GridColumnsList"] = value; // Not L10N
            }
        }
        [UserScopedSettingAttribute]
        public SerializableDictionary<string, string> ResultsGridActiveViews
        {
            get
            {
                if (this["ResultsGridActiveViews"] == null) // Not L10N
                {
                    var list = new SerializableDictionary<string, string>();
                    ResultsGridActiveViews = list;
                }
                return (SerializableDictionary<string, string>)(this["ResultsGridActiveViews"]); // Not L10N
            }
            set { this["ResultsGridActiveViews"] = value; } // Not L10N
        }

        [UserScopedSettingAttribute]
        public List<string> CustomMoleculeTransitionInsertColumnsList
        {
            get
            {
                if (this["CustomMoleculeTransitionInsertColumnsList"] == null) // Not L10N
                {
                    var list = new List<string>();
                    CustomMoleculeTransitionInsertColumnsList = list;
                }
                return (List<string>)this["CustomMoleculeTransitionInsertColumnsList"]; // Not L10N
            }
            set
            {
                this["CustomMoleculeTransitionInsertColumnsList"] = value; // Not L10N
            }
        }
        [UserScopedSettingAttribute]
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
                exclusion = new PeptideExcludeRegex("Unknown", string.Empty); // Not L10N
            return exclusion;
        }

        [UserScopedSettingAttribute]
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

        [UserScopedSettingAttribute]
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
        [UserScopedSettingAttribute]
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

        [UserScopedSettingAttribute]
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

        [UserScopedSettingAttribute]
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

        [UserScopedSettingAttribute]
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

        [UserScopedSettingAttribute]
        public CompensationVoltageList CompensationVoltageList
        {
            get
            {
                CompensationVoltageList list = (CompensationVoltageList)this[typeof(CompensationVoltageList).Name];
                if (list == null)
                {
                    list = new CompensationVoltageList();
                    list.AddDefaults();
                    CompensationVoltageList = list;
                }
                else
                {
                    list.EnsureDefault();
                }
                return list;
            }
            set
            {
                this[typeof(CompensationVoltageList).Name] = value;
            }
        }

        public CompensationVoltageParameters GetCompensationVoltageByName(string name)
        {
            CompensationVoltageParameters regression;
            return (CompensationVoltageList.TryGetValue(name, out regression) &&
                    regression.GetKey() == CompensationVoltageList.GetDefault().GetKey())
                ? null
                : regression;
        }

        public OptimizationLibrary GetOptimizationLibraryByName(string name)
        {
            OptimizationLibrary library;
            if (!OptimizationLibraryList.TryGetValue(name, out library))
                library = OptimizationLibraryList.GetDefault();
            return library;
        }

        [UserScopedSettingAttribute]
        public OptimizationLibraryList OptimizationLibraryList
        {
            get
            {
                OptimizationLibraryList list = (OptimizationLibraryList)this[typeof(OptimizationLibraryList).Name];
                if (list == null)
                {
                    list = new OptimizationLibraryList();
                    list.AddDefaults();
                    OptimizationLibraryList = list;
                }
                else
                {
                    list.EnsureDefault();
                }
                return list;
            }
            set
            {
                this[typeof(OptimizationLibraryList).Name] = value;
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

        [UserScopedSettingAttribute]
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

        [UserScopedSettingAttribute]
        public RetentionTimeList RetentionTimeList
        {
            get
            {
                RetentionTimeList list = (RetentionTimeList)this[typeof(RetentionTimeList).Name];
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

        public RetentionScoreCalculatorSpec GetCalculatorByName(string name)
        {
            RetentionScoreCalculatorSpec calc;
            if(!RTScoreCalculatorList.TryGetValue(name, out calc))
                return null;
            return calc;
        }
        
        [UserScopedSettingAttribute]
        public RTScoreCalculatorList RTScoreCalculatorList
        {
            get
            {
                RTScoreCalculatorList list = (RTScoreCalculatorList)this[typeof(RTScoreCalculatorList).Name];

                if (list == null)
                {
                    list = new RTScoreCalculatorList();
                    list.AddDefaults();
                    RTScoreCalculatorList = list;
                }
                else
                {
                    list.EnsureDefault();
                }
                return list;
            }
            set
            {
                this[typeof(RTScoreCalculatorList).Name] = value;
            }
        }
        
        public IonMobilityPredictor GetDriftTimePredictorByName(string name)
        {
            // Null return is valid for this list, and means no ion mobility
            // calculation should be applied.
            IonMobilityPredictor predictor;
            if (DriftTimePredictorList.TryGetValue(name, out predictor))
            {
                if (predictor.GetKey() == DriftTimePredictorList.GetDefault().GetKey())
                    predictor = null;
            }
            return predictor;
        }

        [UserScopedSettingAttribute]
        public DriftTimePredictorList DriftTimePredictorList
        {
            get
            {
                DriftTimePredictorList list = (DriftTimePredictorList)this[typeof(DriftTimePredictorList).Name];
                if (list == null)
                {
                    list = new DriftTimePredictorList();
                    list.AddDefaults();
                    DriftTimePredictorList = list;
                }
                else
                {
                    list.EnsureDefault();
                }
                return list;
            }
            set
            {
                this[typeof(DriftTimePredictorList).Name] = value;
            }
        }

        public IonMobilityLibrarySpec GetIonMobilityLibraryByName(string name)
        {
            IonMobilityLibrarySpec dtLib;
            return !IonMobilityLibraryList.TryGetValue(name, out dtLib) ? null : dtLib;
        }

        [UserScopedSettingAttribute]
        public IonMobilityLibraryList IonMobilityLibraryList
        {
            get
            {
                IonMobilityLibraryList list = (IonMobilityLibraryList)this[typeof(IonMobilityLibraryList).Name];

                if (list == null)
                {
                    list = new IonMobilityLibraryList();
                    list.AddDefaults();
                    IonMobilityLibraryList = list;
                }
                else
                {
                    list.EnsureDefault();
                }
                return list;
            }
            set
            {
                this[typeof(IonMobilityLibraryList).Name] = value;
            }
        }

        
        public PeakScoringModelSpec GetScoringModelByName(string name)
        {
            PeakScoringModelSpec model;
            if (!PeakScoringModelList.TryGetValue(name, out model))
                return null;
            return model;
        }
        
        [UserScopedSettingAttribute]
        public PeakScoringModelList PeakScoringModelList
        {
            get
            {
                PeakScoringModelList list = (PeakScoringModelList)this[typeof(PeakScoringModelList).Name];

                if (list == null)
                {
                    list = new PeakScoringModelList();
                    list.AddDefaults();
                    PeakScoringModelList = list;
                }
                else
                {
                    list.EnsureDefault();
                }
                return list;
            }
            set
            {
                this[typeof(PeakScoringModelList).Name] = value;
            }
        }
        

        public MeasuredIon GetMeasuredIonByName(string name)
        {
            MeasuredIon ion;
            if (!MeasuredIonList.TryGetValue(name, out ion))
                return null;
            return ion;
        }

        [UserScopedSettingAttribute]
        public MeasuredIonList MeasuredIonList
        {
            get
            {
                MeasuredIonList list = (MeasuredIonList)this[typeof(MeasuredIonList).Name];
                if (list == null)
                {
                    list = new MeasuredIonList();
                    list.AddDefaults();
                    MeasuredIonList = list;
                }
                return list;
            }
            set
            {
                this[typeof(MeasuredIonList).Name] = value;
            }
        }

        public IsotopeEnrichments GetIsotopeEnrichmentsByName(string name)
        {
            IsotopeEnrichments enrichments;
            if (!IsotopeEnrichmentsList.TryGetValue(name, out enrichments))
                return null;
            return enrichments;
        }

        [UserScopedSettingAttribute]
        public IsotopeEnrichmentsList IsotopeEnrichmentsList 
        {
            get
            {
                IsotopeEnrichmentsList list = (IsotopeEnrichmentsList)this[typeof(IsotopeEnrichmentsList).Name];
                if (list == null)
                {
                    list = new IsotopeEnrichmentsList();
                    list.AddDefaults();
                    IsotopeEnrichmentsList = list;
                }
                return list;
            }
            set
            {
                this[typeof(IsotopeEnrichmentsList).Name] = value;
            }
        }

        [UserScopedSettingAttribute]
        public IsolationSchemeList IsolationSchemeList
        {
            get
            {
                IsolationSchemeList list = (IsolationSchemeList)this[typeof(IsolationSchemeList).Name];
                if (list == null)
                {
                    list = new IsolationSchemeList();
                    list.AddDefaults();
                    IsolationSchemeList = list;
                }
                return list;
            }
            set
            {
                this[typeof(IsolationSchemeList).Name] = value;
            }
        }

        [UserScopedSettingAttribute]
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

        [UserScopedSettingAttribute]
        public ReportSpecList ReportSpecList
        {
            get
            {
                return (ReportSpecList)this[typeof(ReportSpecList).Name];
            }
            set
            {
                this[typeof(ReportSpecList).Name] = value;
            }
        }

        [UserScopedSettingAttribute]
        public AnnotationDefList AnnotationDefList
        {
            get
            {
                var list = (AnnotationDefList)this["AnnotationDefList"]; // Not L10N
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
                this["AnnotationDefList"] = value; // Not L10N
            }
        }

        [UserScopedSetting]
        public GroupComparisonDefList GroupComparisonDefList
        {
            get
            {
                var list = (GroupComparisonDefList) this["GroupComparisonDefList"]; // Not L10N
                if (list == null)
                {
                    list = new GroupComparisonDefList();
                    list.AddDefaults();
                    GroupComparisonDefList = list;
                }
                return list;
            }
            set
            {
                this["GroupComparisonDefList"] = value; // Not L10N
            }
        }

        [UserScopedSettingAttribute]
        public ServerList ServerList
        {
            get
            {
                var list = (ServerList)this["ServerList"]; // Not L10N
                if (list == null)
                {
                    list = new ServerList();
                    list.AddDefaults();
                    ServerList = list;
                }
                return list;
            }
            set
            {
                this["ServerList"] = value; // Not L10N
            }
        }

        [UserScopedSetting]
        public RemoteAccountList RemoteAccountList
        {
            get 
            {
                var list = (RemoteAccountList)this["RemoteAccountList"]; // Not L10N
                if (list == null)
                {
                    list = new RemoteAccountList();
                    list.AddDefaults();
                    RemoteAccountList = list;
                }
                return list;
            }
            set
            {
                this["RemoteAccountList"] = value; // Not L10N
            }
        }

        [UserScopedSetting]
        public bool EnableChorus
        {
            get
            {
                var value = this["EnableChorus"]; // Not L10N
                if (value == null)
                {
                    return false;
                }
                return (bool) value;
            } // Not L10N
            set { this["EnableChorus"] = value; }   // Not L10N
        }

        [UserScopedSetting]
        public CalibrationCurveOptions CalibrationCurveOptions
        {
            get { 
                var calibrationCurveOptions = (CalibrationCurveOptions) this["CalibrationCurveOptions"]; // Not L10N
                if (calibrationCurveOptions == null)
                {
                    calibrationCurveOptions = new CalibrationCurveOptions();
                    CalibrationCurveOptions = calibrationCurveOptions;
                }
                return calibrationCurveOptions;
            }
            set
            {
                this["CalibrationCurveOptions"] = value; // Not L10N
            }
        }

        [UserScopedSetting]
        public ColorSchemeList ColorSchemes 
        {
            get
            {
                var colorSchemes = (ColorSchemeList)this["ColorSchemes"]; // Not L10N
                if (colorSchemes == null)
                {
                    colorSchemes = new ColorSchemeList();
                    colorSchemes.AddDefaults();
                    ColorSchemes = colorSchemes;
                }
                return colorSchemes;
            }
            set { this["ColorSchemes"] = value; }  // Not L10N
        }
    }

    /// <summary>
    /// Class wrapper around <see cref="XmlMappedList{TKey,TValue}"/> to
    /// improve XML serialization naming of the list.
    /// </summary>
    public sealed class MethodTemplateList : XmlMappedList<string, MethodTemplateFile>
    {        
    }

    public sealed class ToolList : SettingsList<ToolDescription>
    {
        public override IEnumerable<ToolDescription> GetDefaults(int revisionIndex)
        {
            return new[]
                       {
                           new ToolDescription("SRM Collider", // Not L10N
                               "http://www.srmcollider.org/srmcollider/srmcollider.py", // Not L10N
                               ReportSpecList.SRM_COLLIDER_REPORT_NAME)
                       };
        }

        public static readonly ToolDescription DEPRECATED_QUASAR = new ToolDescription("QuaSAR", // Not L10N
                                                                              "http://genepattern.broadinstitute.org/gp/pages/index.jsf?lsid=QuaSAR", // Not L10N
                                                                              string.Empty);

        // All list editing for tools is handled by the ConfigureToolsDlg
        public override string Title { get { throw new InvalidOperationException(); } }
        public override string Label { get { throw new InvalidOperationException(); } }
        public override ToolDescription EditItem(Control owner, ToolDescription item, IEnumerable<ToolDescription> existing, object tag)
        {   throw new InvalidOperationException(); }
        public override ToolDescription CopyItem(ToolDescription item)
        {   throw new InvalidOperationException(); }

        public static ToolList CopyTools(IEnumerable<ToolDescription> list)
        {
            var listCopy = new ToolList();
            listCopy.AddRange(from t in list
                              where !Equals(t, ToolDescription.EMPTY)
                              select new ToolDescription(t));
            return listCopy;
        }
    }

    public sealed class EnzymeList : SettingsList<Enzyme>
    {
        public static Enzyme GetDefault()
        {
            return new Enzyme("Trypsin", "KR", "P"); // Not L10N
        }

        public override IEnumerable<Enzyme> GetDefaults(int revisionIndex)
        {
            // ReSharper disable NonLocalizedString
            return new[]
                {
                    GetDefault(),
                    new Enzyme("Trypsin (semi)", "KR", "P", null, null, true), // Added post-3.6 (without forcing a new list version)
                    new Enzyme("Trypsin/P", "KR", ""),
                    new Enzyme("TrypsinK", "K", "P"), 
                    new Enzyme("TrypsinR", "R", "P"), 
                    new Enzyme("Chymotrypsin", "FWYL", "P"),
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
                // ReSharper restore NonLocalizedString
        }

        public override Enzyme EditItem(Control owner, Enzyme item, IEnumerable<Enzyme> existing, object tag)
        {
            using (EditEnzymeDlg editEnzyme = new EditEnzymeDlg(existing ?? this) { Enzyme = item })
            {
                if (editEnzyme.ShowDialog(owner) == DialogResult.OK)
                    return editEnzyme.Enzyme;

                return null;
            }
        }

        public override Enzyme CopyItem(Enzyme item)
        {
            return (Enzyme) item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.EnzymeList_Title_Edit_Enzymes; } }

        public override string Label { get { return Resources.EnzymeList_Label_Enzymes; } }
    }

    public sealed class PeptideExcludeList : SettingsList<PeptideExcludeRegex>
    {
        public override IEnumerable<PeptideExcludeRegex> GetDefaults(int revisionIndex)
        {
            // ReSharper disable NonLocalizedString
            return new[]
                {
                    new PeptideExcludeRegex("Cys", "[C]"),
                    new PeptideExcludeRegex("Met", "[M]"),
                    new PeptideExcludeRegex("His", "[H]"),
                    new PeptideExcludeRegex("NXT/NXS", "N.[TS]"),
                    new PeptideExcludeRegex("RP/KP", "[RK]P")
                };
            // ReSharper restore NonLocalizedString
        }

        public override PeptideExcludeRegex EditItem(Control owner, PeptideExcludeRegex item,
            IEnumerable<PeptideExcludeRegex> existing, object tag)
        {
            using (EditExclusionDlg editExclusion = new EditExclusionDlg(existing ?? this) { Exclusion = item })
            {
                if (editExclusion.ShowDialog(owner) == DialogResult.OK)
                    return editExclusion.Exclusion;

                return null;
            }
        }

        public override PeptideExcludeRegex CopyItem(PeptideExcludeRegex item)
        {
            return (PeptideExcludeRegex) item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.PeptideExcludeList_Title_Edit_Exclusions; } }

        public override string Label { get { return Resources.PeptideExcludeList_Label_Exclusions; } }
    }

    public sealed class ServerList : SettingsList<Server>
    {
        public override IEnumerable<Server>  GetDefaults(int revisionIndex)
        {
            yield break;
        }

        public override string Title { get { return Resources.ServerList_Title_Edit_Servers; } }

        public override string Label { get { return Resources.ServerList_Label__Servers; } }

        public override Server EditItem(Control owner, Server item, IEnumerable<Server> existing, object tag)
        {
            using (EditServerDlg editServer = new EditServerDlg(existing ?? this) {Server = item})
            {
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

        public override Server CopyItem(Server item)
        {
            return new Server(item.URI, item.Username, item.Password);
        }
    }

    public sealed class SpectralLibraryList : SettingsListNotifying<LibrarySpec>
    {
        public override IEnumerable<LibrarySpec> GetDefaults(int revisionIndex)
        {
            return new LibrarySpec[0];
        }

        public override LibrarySpec EditItem(Control owner, LibrarySpec item,
            IEnumerable<LibrarySpec> existing, object tag)
        {
            using (EditLibraryDlg editLibrary = new EditLibraryDlg(existing ?? this) { LibrarySpec = item })
            {
                if (editLibrary.ShowDialog(owner) == DialogResult.OK)
                    return editLibrary.LibrarySpec;

                return null;
            }
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

        public override string Title { get { return Resources.SpectralLibraryList_Title_Edit_Libraries; } }

        public override string Label { get { return Resources.SpectralLibraryList_Label_Libraries; } }

        protected override IXmlElementHelper<LibrarySpec>[] GetXmlElementHelpers()
        {
            return PeptideLibraries.LibrarySpecXmlHelpers;
        }
    }

    public sealed class BackgroundProteomeList : SettingsList<BackgroundProteomeSpec>
    {
        private static readonly BackgroundProteomeSpec NONE = new BackgroundProteomeSpec(ELEMENT_NONE, string.Empty);

        public override string GetDisplayName(BackgroundProteomeSpec item)
        {
            // Use the localized text in the UI
            return Equals(item, NONE) ? Resources.SettingsList_ELEMENT_NONE_None : base.GetDisplayName(item);
        }

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
            using (var editBackgroundProteomeDlg = new BuildBackgroundProteomeDlg(existing ?? this) { BackgroundProteomeSpec = item })
            {
                if (editBackgroundProteomeDlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return editBackgroundProteomeDlg.BackgroundProteomeSpec;
                }
                return null;
            }
        }

        public override BackgroundProteomeSpec CopyItem(BackgroundProteomeSpec item)
        {
            return (BackgroundProteomeSpec) item.ChangeName(string.Empty);
        }

        public override String Title { get { return Resources.BackgroundProteomeList_Title_Edit_Background_Proteomes; } }
        public override String Label { get { return Resources.BackgroundProteomeList_Label_Background_Proteomes; } }

        protected override IXmlElementHelper<BackgroundProteomeSpec>[] GetXmlElementHelpers()
        {
            return BackgroundProteomeSpec.BackgroundProteomeHelpers;
        }

        public BackgroundProteomeSpec GetBackgroundProteomeSpec(String name)
        {
            BackgroundProteomeSpec spec;
            if (TryGetValue(name, out spec))
                return spec;
            return null;
        }

        public override int ExcludeDefaults
        {
            get { return 1; }
        }
    }

    public sealed class StaticModList : SettingsList<StaticMod>
    {
        public const string LEGACY_DEFAULT_NAME = "Carbamidomethyl Cysteine"; // Not L10N
        public const string DEFAULT_NAME = "Carbamidomethyl (C)"; // Not L10N

        private static readonly StaticMod[] DEFAULT_MODS =
        {
            new StaticMod(UniModData.DEFAULT.Name, UniModData.DEFAULT.AAs, UniModData.DEFAULT.Terminus, false,
                UniModData.DEFAULT.Formula, UniModData.DEFAULT.LabelAtoms,
                RelativeRT.Matching, null, null, UniModData.DEFAULT.Losses, UniModData.DEFAULT.ID,
                UniModData.DEFAULT.ShortName, null)
        };

        public static StaticMod[] GetDefaultsOn()
        {
            return DEFAULT_MODS;
        }

        public override IEnumerable<StaticMod> GetDefaults(int revisionIndex)
        {
            return GetDefaultsOn();
        }

        public override StaticMod EditItem(Control owner, StaticMod item,
            IEnumerable<StaticMod> existing, object tag)
        {
            using (EditStaticModDlg editMod = new EditStaticModDlg(item, existing ?? this, false))
            {
                if (editMod.ShowDialog(owner) == DialogResult.OK)
                    return editMod.Modification;

                return null;
            }
        }

        public override StaticMod CopyItem(StaticMod item)
        {
            return (StaticMod) item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.StaticModList_Title_Edit_Structural_Modifications; } }

        public override string Label { get { return Resources.StaticModList_Label_Modifications; } }
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
            using (EditStaticModDlg editMod = new EditStaticModDlg(item, existing ?? this, true)
                                           {
                                               Text = Resources.HeavyModList_EditItem_Edit_Isotope_Modification
                                           })
            {
                if (editMod.ShowDialog(owner) == DialogResult.OK)
                    return editMod.Modification;

                return null;
            }
        }

        public override StaticMod CopyItem(StaticMod item)
        {
            return (StaticMod) item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.HeavyModList_Title_Edit_Isotope_Modifications; } }

        public override string Label { get { return Resources.StaticModList_Label_Modifications; } }
    }

    public sealed class CollisionEnergyList : SettingsList<CollisionEnergyRegression>
    {
        public static readonly CollisionEnergyRegression NONE =
            new CollisionEnergyRegression(ELEMENT_NONE, new [] { new ChargeRegressionLine(2, 0, 0) });

        public override int RevisionIndexCurrent { get { return 8; } }

        public static CollisionEnergyRegression GetDefault()
        {
            return NONE;
        }

        public static CollisionEnergyRegression GetDefault3_6()
        {
            var thermoRegressions = new[]
                                {
                                    new ChargeRegressionLine(2, 0.0339, 2.3597),
                                    new ChargeRegressionLine(3, 0.0295, 1.5123)
                                };
            return new CollisionEnergyRegression("Thermo TSQ Quantiva", thermoRegressions); // Not L10N
        }

        public static CollisionEnergyRegression GetDefault0_6()
        {
            var thermoRegressions = new[]
                                {
                                    new ChargeRegressionLine(2, 0.03, 2.905),
                                    new ChargeRegressionLine(3, 0.038, 2.281)
                                };
            return new CollisionEnergyRegression("Thermo TSQ Vantage", thermoRegressions); // Not L10N
        }

        protected override void ValidateLoad()
        {
            base.ValidateLoad();

            // Make sure NONE is at the beginning of the list
            if (!ReferenceEquals(NONE, this[0]))
            {
                Remove(NONE);
                InsertItem(0, NONE);
            }
        }

        public override IEnumerable<CollisionEnergyRegression> GetDefaults(int revisionIndex)
        {
            switch (revisionIndex)
            {
                case 0: // v0.5
                    return new[]
                        {
                            new CollisionEnergyRegression("Thermo", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.034, 3.314),
                                    new ChargeRegressionLine(3, 0.044, 3.314)
                                }), 
                            new CollisionEnergyRegression("ABI", new[] // Not L10N
                                { new ChargeRegressionLine(2, 0.0431, 4.7556), }),
                            new CollisionEnergyRegression("Agilent", new[] // Not L10N
                                { new ChargeRegressionLine(2, 0.036, -4.8), }),
                            new CollisionEnergyRegression("Waters", new[] // Not L10N
                                { new ChargeRegressionLine(2, 0.034, 3.314), }),
                        };
                case 1:    // v0.6
                    return new[]
                        {
                            new CollisionEnergyRegression("Thermo TSQ Vantage", new[] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.03, 2.786),
                                    new ChargeRegressionLine(3, 0.038, 2.183)
                                }),
                            new CollisionEnergyRegression("Thermo TSQ Ultra", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.035, 1.643),
                                    new ChargeRegressionLine(3, 0.037, 3.265)
                                }), 
                            new CollisionEnergyRegression("ABI 4000 QTrap", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.057, 4.453),
                                    new ChargeRegressionLine(3, 0.03, 7.692)
                                }), 
                            new CollisionEnergyRegression("Agilent", new[] // Not L10N
                                { new ChargeRegressionLine(2, 0.036, -4.8), }),
                            new CollisionEnergyRegression("Waters", new[] // Not L10N
                                { new ChargeRegressionLine(2, 0.034, 3.314), }),
                        };
                case 2:    // v0.6 - fix
                    return new[]
                        {
                            GetDefault0_6(), 
                            new CollisionEnergyRegression("Thermo TSQ Ultra", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.036, 0.954),
                                    new ChargeRegressionLine(3, 0.037, 3.525)
                                }), 
                            new CollisionEnergyRegression("ABI 4000 QTrap", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.057, -4.265),
                                    new ChargeRegressionLine(3, 0.031, 7.082)
                                }), 
                            new CollisionEnergyRegression("Agilent", new[] // Not L10N
                                { new ChargeRegressionLine(2, 0.036, -4.8), }),
                            new CollisionEnergyRegression("Waters", new[] // Not L10N
                                { new ChargeRegressionLine(2, 0.034, 3.314), }),
                        };

                case 3:    // v1.1
                    return new[]
                        {
                            GetDefault0_6(), 
                            new CollisionEnergyRegression("Thermo TSQ Ultra", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.036, 0.954),
                                    new ChargeRegressionLine(3, 0.037, 3.525)
                                }), 
                            new CollisionEnergyRegression("ABI 4000 QTrap", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.057, -4.265),
                                    new ChargeRegressionLine(3, 0.031, 7.082)
                                }), 
                            new CollisionEnergyRegression("Agilent 6460", new[] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.051, -15.563),
                                    new ChargeRegressionLine(3, 0.037, -9.784)
                                }),
                            new CollisionEnergyRegression("Waters Xevo", new[] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.037, -1.066),
                                    new ChargeRegressionLine(3, 0.036, -1.328)
                                }),
                        };
                case 4:    // v1.2
                    return new[]
                        {
                            GetDefault0_6(), 
                            new CollisionEnergyRegression("Thermo TSQ Ultra", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.036, 0.954),
                                    new ChargeRegressionLine(3, 0.037, 3.525)
                                }), 
                            new CollisionEnergyRegression("ABI 4000 QTrap", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.057, -4.265),
                                    new ChargeRegressionLine(3, 0.031, 7.082)
                                }), 
                            new CollisionEnergyRegression("ABI 5500 QTrap", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.036, 8.857),
                                    new ChargeRegressionLine(3, 0.0544, -2.4099)
                                }), 
                            new CollisionEnergyRegression("Agilent 6460", new[] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.051, -15.563),
                                    new ChargeRegressionLine(3, 0.037, -9.784)
                                }),
                            new CollisionEnergyRegression("Waters Xevo", new[] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.037, -1.066),
                                    new ChargeRegressionLine(3, 0.036, -1.328)
                                }),
                        };
                case 5:    // v2.5
                    return new[]
                        {
                            GetDefault0_6(), 
                            new CollisionEnergyRegression("Thermo TSQ Ultra", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.036, 0.954),
                                    new ChargeRegressionLine(3, 0.037, 3.525)
                                }), 
                            new CollisionEnergyRegression("ABI 4000 QTrap", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.057, -4.265),
                                    new ChargeRegressionLine(3, 0.031, 7.082)
                                }), 
                            new CollisionEnergyRegression("ABI 5500 QTrap", new [] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.036, 8.857),
                                    new ChargeRegressionLine(3, 0.0544, -2.4099)
                                }), 
                            new CollisionEnergyRegression("Agilent QQQ", new[] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.031, 1),
                                    new ChargeRegressionLine(3, 0.036, -4.8),
                                }, 3, 3),
                            new CollisionEnergyRegression("Waters Xevo", new[] // Not L10N
                                {
                                    new ChargeRegressionLine(2, 0.037, -1.066),
                                    new ChargeRegressionLine(3, 0.036, -1.328)
                                }),
                        };
                case 6:    // v2.5.1 - add Shimadzu
                    {
                        var list5 = GetDefaults(5).ToList();
                        list5.Add(new CollisionEnergyRegression("Shimadzu QQQ", new[] // Not L10N
                        {
                            new ChargeRegressionLine(2, 0.04, -0.5082),
                            new ChargeRegressionLine(3, 0.037, -0.8368), 
                        }));
                        return list5.ToArray();
                    }
                case 7:    // v3.6 patch - add Thermo TSQ Quantiva
                    {
                        var list6 = GetDefaults(6).ToList();
                        list6.Insert(0, GetDefault3_6());
                        return list6.ToArray();
                    }
                default:    // v3.7.1 - add None as default
                {
                    var list6 = GetDefaults(7).ToList();
                    list6.Insert(0, GetDefault());
                    return list6.ToArray();
                }
            }
        }

        public override CollisionEnergyRegression EditItem(Control owner, CollisionEnergyRegression item,
            IEnumerable<CollisionEnergyRegression> existing, object tag)
        {
            using (EditCEDlg editCE = new EditCEDlg(existing ?? this) { Regression = item })
            {
                if (editCE.ShowDialog(owner) == DialogResult.OK)
                    return editCE.Regression;

                return null;
            }
        }

        public override CollisionEnergyRegression CopyItem(CollisionEnergyRegression item)
        {
            return (CollisionEnergyRegression) item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.CollisionEnergyList_Title_Edit_Collision_Energy_Regressions; } }

        public override string Label { get { return Resources.CollisionEnergyList_Label_Collision_Energy_Regression; } }

        public override int ExcludeDefaults { get { return 1; } }
    }

    public sealed class OptimizationLibraryList : SettingsList<OptimizationLibrary>
    {
        public override string GetDisplayName(OptimizationLibrary item)
        {
            // Use the localized text in the UI
            return Equals(item, OptimizationLibrary.NONE) ? Resources.SettingsList_ELEMENT_NONE_None : base.GetDisplayName(item);
        }

        public static OptimizationLibrary GetDefault()
        {
            return OptimizationLibrary.NONE;
        }

        public override OptimizationLibrary EditItem(Control owner, OptimizationLibrary item,
            IEnumerable<OptimizationLibrary> existing, object tag)
        {
            using (EditOptimizationLibraryDlg editOptimizationLibraryDlg = new EditOptimizationLibraryDlg(Program.ActiveDocument, item, existing ?? this))
            {
                return editOptimizationLibraryDlg.ShowDialog(owner) == DialogResult.OK ? editOptimizationLibraryDlg.Library : null;
            }
        }

        public override OptimizationLibrary CopyItem(OptimizationLibrary item)
        {
            return (OptimizationLibrary)item.ChangeName(string.Empty);
        }

        public override IEnumerable<OptimizationLibrary> GetDefaults(int revisionIndex)
        {
            return new[] { GetDefault() };
        }

        public void EnsureDefault()
        {
            // Make sure the choice of no library is present.
            OptimizationLibrary defaultElement = GetDefault();
            if (Count == 0 || this[0].GetKey() != defaultElement.GetKey())
                Insert(0, defaultElement);
        }

        public override string Title { get { return Resources.OptimizationLibraryList_Title_Edit_Optimization_Databases; } }

        public override string Label { get { return Resources.OptimizationLibraryList_Label_Optimization_Database; } }

        public override int ExcludeDefaults { get { return 1; } }
    }

    public sealed class DeclusterPotentialList : SettingsList<DeclusteringPotentialRegression>
    {
        private static readonly DeclusteringPotentialRegression NONE =
            new DeclusteringPotentialRegression(ELEMENT_NONE, 0, 0);

        public override string GetDisplayName(DeclusteringPotentialRegression item)
        {
            // Use the localized text in the UI
            return Equals(item, NONE) ? Resources.SettingsList_ELEMENT_NONE_None : base.GetDisplayName(item);
        }

        public static DeclusteringPotentialRegression GetDefault()
        {
            return NONE;
        }

        public override IEnumerable<DeclusteringPotentialRegression> GetDefaults(int revisionIndex)
        {
            return new[]
            {
               GetDefault(),
               new DeclusteringPotentialRegression("ABI", 0.0729, 31.117), // Not L10N
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
            using (EditDPDlg editDP = new EditDPDlg(existing ?? this) { Regression = item })
            {
                if (editDP.ShowDialog(owner) == DialogResult.OK)
                    return editDP.Regression;

                return null;
            }
        }

        public override DeclusteringPotentialRegression CopyItem(DeclusteringPotentialRegression item)
        {
            return (DeclusteringPotentialRegression) item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.DeclusterPotentialList_Title_Edit_Declustering_Potential_Regressions; } }

        public override string Label { get { return Resources.DeclusterPotentialList_Label_Declustering_Potential_Regressions; } }

        public override int ExcludeDefaults { get { return 1; } }
    }

    public sealed class CompensationVoltageList : SettingsList<CompensationVoltageParameters>
    {
        private static readonly CompensationVoltageParameters NONE = new CompensationVoltageParameters(ELEMENT_NONE, 0, 0, 0, 0, 0);

        public override string GetDisplayName(CompensationVoltageParameters item)
        {
            // Use the localized text in the UI
            return Equals(item, NONE) ? Resources.SettingsList_ELEMENT_NONE_None : base.GetDisplayName(item);
        }

        public static CompensationVoltageParameters GetDefault()
        {
            return NONE;
        }

        public override IEnumerable<CompensationVoltageParameters> GetDefaults(int revisionIndex)
        {
            return new[]
            {
               GetDefault(),
               new CompensationVoltageParameters("ABI", 6, 30, 3, 3, 3), // Not L10N
            };
        }

        public void EnsureDefault()
        {
            // Make sure the choice of no retention time regression is present.
            CompensationVoltageParameters defaultElement = GetDefault();
            if (Count == 0 || this[0].GetKey() != defaultElement.GetKey())
                Insert(0, defaultElement);
        }

        public override CompensationVoltageParameters EditItem(Control owner, CompensationVoltageParameters item,
            IEnumerable<CompensationVoltageParameters> existing, object tag)
        {
            using (var editCov = new EditCoVDlg(item, existing ?? this))
            {
                return editCov.ShowDialog(owner) == DialogResult.OK ? editCov.Parameters : null;
            }
        }

        public override CompensationVoltageParameters CopyItem(CompensationVoltageParameters item)
        {
            return (CompensationVoltageParameters)item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.CompensationVoltageList_Title_Edit_Compensation_Voltage_Parameter_Sets; } }
        public override string Label { get { return Resources.CompensationVoltageList_Label_Compensation__Voltage_Parameters_; } }
        public override int ExcludeDefaults { get { return 1; } }
    }
    
    public sealed class RTScoreCalculatorList : SettingsListNotifying<RetentionScoreCalculatorSpec>
    {
        private static readonly RetentionScoreCalculator[] DEFAULTS =
        {
            new RetentionScoreCalculator(RetentionTimeRegression.SSRCALC_100_A),
            new RetentionScoreCalculator(RetentionTimeRegression.SSRCALC_300_A)
        };

        /// <summary>
        /// <see cref="RetentionTimeRegression"/> objects depend on calculators. If a user deletes or changes a calculator,
        /// the <see cref="RetentionTimeRegression"/> objects that depend on it may need to be removed.
        /// </summary>
        public override bool AcceptList(Control owner, IList<RetentionScoreCalculatorSpec> listNew)
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
                var message = TextUtil.LineSeparate(Resources.RTScoreCalculatorList_AcceptList_The_regressions,
                                                    TextUtil.LineSeparate(listMissingCalc.Select(reg => reg.Name)),
                                                    Resources.RTScoreCalculatorList_AcceptList_will_be_deleted_because_the_calculators_they_depend_on_have_changed_Do_you_want_to_continue);
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

        public override RetentionScoreCalculatorSpec EditItem(Control owner, RetentionScoreCalculatorSpec item,
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

        public override RetentionScoreCalculatorSpec CopyItem(RetentionScoreCalculatorSpec item)
        {
            return (RetentionScoreCalculatorSpec) item.ChangeName(string.Empty);
        }

        public override IEnumerable<RetentionScoreCalculatorSpec> GetDefaults(int revisionIndex)
        {
            return DEFAULTS;
        }

        public void EnsureDefault()
        {
            // Make sure the default calculators are present.
            var defaultCalculators = GetDefaults().ToArray();
            int len = defaultCalculators.Length;
            if (Count < len || !ArrayUtil.ReferencesEqual(defaultCalculators, this.Take(len).ToArray()))
            {
                foreach (var calc in defaultCalculators)
                    Remove(calc);
                foreach (var calc in defaultCalculators.Reverse())
                    Insert(0, calc);
            }
        }

        protected override IXmlElementHelper<RetentionScoreCalculatorSpec>[] GetXmlElementHelpers()
        {
            return RetentionTimeRegression.CalculatorXmlHelpers;
        }

        public void Initialize(IProgressMonitor loadMonitor)
        {
            foreach (var calc in this.ToArray())
                Initialize(loadMonitor, calc);
        }

        public RetentionScoreCalculatorSpec Initialize(IProgressMonitor loadMonitor, RetentionScoreCalculatorSpec calc)
        {
            if (calc == null)
                return null;

            try
            {
                var calcInit = calc.Initialize(loadMonitor);
                if (!Equals(calc.Name, XmlNamedElement.NAME_INTERNAL) && !ReferenceEquals(calcInit, calc))
                    SetValue(calcInit);
                calc = calcInit;
            }
            catch (CalculatorException)
            {
                //Consider: Should we really fail silently?
            }
            return calc;
        }

        public override string Title { get { return Resources.RTScoreCalculatorList_Title_Edit_Retention_Time_Calculators; } }

        public override string Label { get { return Resources.RTScoreCalculatorList_Label_Retention_Time_Calculators; } }

        public override int ExcludeDefaults { get { return DEFAULTS.Length; } }

        public bool CanEditItem(RetentionScoreCalculatorSpec item)
        {
            return item != null && !GetDefaults().Contains(item);
        }
    }
    
    public sealed class IonMobilityLibraryList : SettingsListNotifying<IonMobilityLibrarySpec>
    {
        /// <summary>
        /// <see cref="IonMobilityPredictor"/> objects depend on ion mobility libraries. If a user deletes or changes a library,
        /// the <see cref="IonMobilityPredictor"/> objects that depend on it may need to be removed.
        /// </summary>
        public override bool AcceptList(Control owner, IList<IonMobilityLibrarySpec> listNew)
        {
            var listMissingLib = new List<IonMobilityPredictor>();
            var listChangedLib = new List<IonMobilityPredictor>();
            foreach (var driftTimePredictor in Settings.Default.DriftTimePredictorList.ToArray())
            {
                var predictor = driftTimePredictor;

                // Not all ion mobility predictors use a library
                if (predictor.IonMobilityLibrary == null)
                    continue;

                if (listNew.Contains(calc => Equals(calc, predictor.IonMobilityLibrary)))
                {
                    var libChanged = listNew.FirstOrDefault(calc =>
                        Equals(calc, predictor.IonMobilityLibrary.ChangeName(calc.Name)));

                    if (libChanged == null)
                        listMissingLib.Add(predictor);
                    else
                        listChangedLib.Add(predictor.ChangeLibrary(libChanged));
                }
            }

            if (listMissingLib.Count > 0)
            {
                var message = TextUtil.LineSeparate(Resources.IonMobilityLibraryList_AcceptList_The_drift_time_predictors_,
                                                    TextUtil.LineSeparate(listMissingLib.Select(reg => reg.Name)),
                                                    Resources.IonMobilityLibraryList_AcceptList_will_be_deleted_because_the_libraries_they_depend_on_have_changed__Do_you_want_to_continue_);
                if (DialogResult.Yes != MultiButtonMsgDlg.Show(owner, message, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true))
                {
                    return false;
                }
            }

            foreach (var predictor in listChangedLib)
            {
                Settings.Default.DriftTimePredictorList.SetValue(predictor);
            }

            return true;
        }

        public override IonMobilityLibrarySpec EditItem(Control owner, IonMobilityLibrarySpec item,
            IEnumerable<IonMobilityLibrarySpec> existing, object tag)
        {
            var calc = item as IonMobilityLibrary;
            if (item == null || calc != null)
            {
                using (var editIonMobilityLibraryDlg = new EditIonMobilityLibraryDlg(calc, existing))
                {
                    if (editIonMobilityLibraryDlg.ShowDialog(owner) == DialogResult.OK)
                    {
                        return editIonMobilityLibraryDlg.IonMobilityLibrary;
                    }
                }
            }

            return null;
        }

        public override IonMobilityLibrarySpec CopyItem(IonMobilityLibrarySpec item)
        {
            return (IonMobilityLibrarySpec)item.ChangeName(string.Empty);
        }

        public override IEnumerable<IonMobilityLibrarySpec> GetDefaults(int revisionIndex)
        {
            return new[] { IonMobilityLibrary.NONE };
        }

        public void EnsureDefault()
        {
            // Make sure the default libraries are present.
            var defaultLibraries = GetDefaults().ToArray();
            int len = defaultLibraries.Length;
            if (Count < len || !ArrayUtil.ReferencesEqual(defaultLibraries, this.Take(len).ToArray()))
            {
                foreach (var library in defaultLibraries)
                    Remove(library);
                foreach (var library in defaultLibraries.Reverse())
                    Insert(0, library);
            }
        }

        private static readonly IXmlElementHelper<IonMobilityLibrarySpec>[] DRIFT_TIME_HELPERS =
        {
            new XmlElementHelperSuper<IonMobilityLibrary, IonMobilityLibrarySpec>(),
        };


        protected override IXmlElementHelper<IonMobilityLibrarySpec>[] GetXmlElementHelpers()
        {
            return DRIFT_TIME_HELPERS;
        }

        public override string Title { get { return Resources.IonMobilityLibraryList_Title_Edit_Ion_Mobility_Libraries; } }

        public override string Label { get { return Resources.IonMobilityLibraryList_Label_Ion_Mobility_Libraries_; } }

        public bool CanEditItem(IonMobilityLibrarySpec item)
        {
            return item != null && !GetDefaults().Contains(item);
        }
    }

    public sealed class PeakScoringModelList : SettingsListNotifying<PeakScoringModelSpec>
    {
        private static readonly PeakScoringModelSpec[] DEFAULTS =
        {
            LegacyScoringModel.DEFAULT_UNTRAINED_MODEL
        };

        public override string GetDisplayName(PeakScoringModelSpec item)
        {
            // Use the localized text in the UI
            return ReferenceEquals(item, DEFAULTS[0]) ? LegacyScoringModel.DEFAULT_NAME : base.GetDisplayName(item);
        }

        public override PeakScoringModelSpec EditItem(Control owner, PeakScoringModelSpec item,
            IEnumerable<PeakScoringModelSpec> existing, object tag)
        {
            using (var editModel = new EditPeakScoringModelDlg(existing ?? this))
            {
                if (editModel.SetScoringModel(owner, item, tag as IFeatureScoreProvider))
                {
                    if (editModel.ShowDialog(owner) == DialogResult.OK)
                        return (PeakScoringModelSpec)editModel.PeakScoringModel;
                }

                return null;
            }
        }

        public void EnsureDefault()
        {
            // Make sure the default scoring models are present.
            var defaultScoringModels = GetDefaults().ToArray();
            int len = defaultScoringModels.Length;
            if (Count < len || !ArrayUtil.ReferencesEqual(defaultScoringModels, this.Take(len).ToArray()))
            {
                foreach (var scoringModel in defaultScoringModels)
                    Remove(scoringModel);
                foreach (var scoringModel in defaultScoringModels.Reverse())
                    Insert(0, scoringModel);
            }
        }

        private static readonly IXmlElementHelper<PeakScoringModelSpec>[] MODEL_HELPERS =
        {
            new XmlElementHelperSuper<LegacyScoringModel, PeakScoringModelSpec>(),
            new XmlElementHelperSuper<MProphetPeakScoringModel, PeakScoringModelSpec>(),
        };

        protected override IXmlElementHelper<PeakScoringModelSpec>[] GetXmlElementHelpers()
        {
            return MODEL_HELPERS;
        }

        public override PeakScoringModelSpec CopyItem(PeakScoringModelSpec item)
        {
            return (PeakScoringModelSpec)item.ChangeName(string.Empty);
        }

        public override IEnumerable<PeakScoringModelSpec> GetDefaults(int revisionIndex)
        {
            return DEFAULTS;
        }

        public override string Title { get { return Resources.PeakScoringModelList_Title_Edit_Peak_Scoring_Models; } }

        public override string Label { get { return Resources.PeakScoringModelList_Label_Peak_Scoring_Models; } }

        public override int ExcludeDefaults { get { return DEFAULTS.Length; } }
    }
    
    public sealed class RetentionTimeList : SettingsList<RetentionTimeRegression>
    {
        private static readonly RetentionTimeRegression NONE =
            new RetentionTimeRegression(ELEMENT_NONE, null, 0, 0, 0, new MeasuredRetentionTime[0]);

        public override string GetDisplayName(RetentionTimeRegression item)
        {
            // Use the localized text in the UI
            return Equals(item, NONE) ? Resources.SettingsList_ELEMENT_NONE_None : base.GetDisplayName(item);
        }

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
            using (EditRTDlg editRT = new EditRTDlg(existing ?? this) { Regression = item })
            {
                if (editRT.ShowDialog(owner) == DialogResult.OK)
                    return editRT.Regression;
            }

            return null;
        }

        public override RetentionTimeRegression CopyItem(RetentionTimeRegression item)
        {
            return (RetentionTimeRegression) item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.RetentionTimeList_Title_Edit_Retention_Time_Regressions; } }

        public override string Label { get { return Resources.RetentionTimeList_Label_Retention_Time_Regression; } }

        public override int ExcludeDefaults { get { return 1; } }
    }

    public sealed class DriftTimePredictorList : SettingsList<IonMobilityPredictor>
    {
        private static readonly IonMobilityPredictor NONE =
            new IonMobilityPredictor(ELEMENT_NONE, null, null, null, 0, 0, 0, 0);

        public override string GetDisplayName(IonMobilityPredictor item)
        {
            // Use the localized text in the UI
            return Equals(item, NONE) ? Resources.SettingsList_ELEMENT_NONE_None : base.GetDisplayName(item);
        }

        public static IonMobilityPredictor GetDefault()
        {
            return NONE;
        }

        public override IEnumerable<IonMobilityPredictor> GetDefaults(int revisionIndex)
        {
            return new[] { GetDefault() };
        }

        public void EnsureDefault()
        {
            // Make sure the choice of no ion mobility regression is present.
            IonMobilityPredictor defaultElement = GetDefault();
            if (Count == 0 || this[0].GetKey() != defaultElement.GetKey())
                Insert(0, defaultElement);
        }

        public override IonMobilityPredictor EditItem(Control owner, IonMobilityPredictor item,
            IEnumerable<IonMobilityPredictor> existing, object tag)
        {
            using (EditDriftTimePredictorDlg editDT = new EditDriftTimePredictorDlg(existing ?? this) { Predictor = item })
            {
                if (editDT.ShowDialog(owner) == DialogResult.OK)
                    return editDT.Predictor;
            }

            return null;
        }

        public override IonMobilityPredictor CopyItem(IonMobilityPredictor item)
        {
            return (IonMobilityPredictor)item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.DriftTimePredictorList_Title_Edit_Drift_Time_Predictors; } }

        public override string Label { get { return Resources.DriftTimePredictorList_Label_Drift_Time_Predictor_; } }

        public override int ExcludeDefaults { get { return 1; } }
    }

    public sealed class MeasuredIonList : SettingsList<MeasuredIon>
    {
        public static readonly MeasuredIon NTERM_PROLINE =
            new MeasuredIon("N-terminal to Proline", "P", null, SequenceTerminus.N, 3); // Not L10N
        public static readonly MeasuredIon NTERM_PROLINE_LEGACY =
            new MeasuredIon("N-terminal to Proline (legacy)", "P", null, SequenceTerminus.N, 1); // Not L10N

        public static readonly MeasuredIon CTERM_GLU_ASP =
            new MeasuredIon("C-terminal to Glu or Asp", "ED", null, SequenceTerminus.C, 3); // Not L10N
        public static readonly MeasuredIon CTERM_GLU_ASP_LEGACY =
            new MeasuredIon("C-terminal to Glu or Asp (legacy)", "ED", null, SequenceTerminus.C, 1); // Not L10N
        // iTRAQ chemical formulas from http://tools.lifetechnologies.com/content/sfs/manuals/ITRAQchemistry_guide.pdf#page=39
        public static readonly MeasuredIon ITRAQ_114 = CreateMeasuredIon("iTRAQ-114", "C5C'H13N2");// Not L10N
        public static readonly MeasuredIon ITRAQ_115 = CreateMeasuredIon("iTRAQ-115", "C5C'H13NN'");// Not L10N
        public static readonly MeasuredIon ITRAQ_116 = CreateMeasuredIon("iTRAQ-116", "C4C'2H13NN'");// Not L10N
        public static readonly MeasuredIon ITRAQ_117 = CreateMeasuredIon("iTRAQ-117", "C3C'3H13NN'");// Not L10N
        // TMT chemical formulas from http://pubs.acs.org/doi/pdf/10.1021/ac500140s#page=2
        public static readonly MeasuredIon TMT_126 = CreateMeasuredIon("TMT-126", "C8H16N");// Not L10N
        public static readonly MeasuredIon TMT_127_L = CreateMeasuredIon("TMT-127L", "C8H16N'");// Not L10N
        public static readonly MeasuredIon TMT_127_H = CreateMeasuredIon("TMT-127H", "C7C'H16N");// Not L10N
        public static readonly MeasuredIon TMT_128_L = CreateMeasuredIon("TMT-128L", "C7C'1H16N'");// Not L10N
        public static readonly MeasuredIon TMT_128_H = CreateMeasuredIon("TMT-128H", "C6C'2H16N");// Not L10N
        public static readonly MeasuredIon TMT_129_L = CreateMeasuredIon("TMT-129L", "C6C'2H16N'");// Not L10N
        public static readonly MeasuredIon TMT_129_H = CreateMeasuredIon("TMT-129H", "C5C'3H16N");// Not L10N
        public static readonly MeasuredIon TMT_130_L = CreateMeasuredIon("TMT-130L", "C5C'3H16N'");// Not L10N
        public static readonly MeasuredIon TMT_130_H = CreateMeasuredIon("TMT-130H", "C4C'4H16N");// Not L10N
        public static readonly MeasuredIon TMT_131 = CreateMeasuredIon("TMT-131", "C4C'4H16N'");// Not L10N

        private static MeasuredIon CreateMeasuredIon(string name, string formula)
        {
            return new MeasuredIon(name, formula, null, null, Adduct.SINGLY_PROTONATED);
        }

        public override int RevisionIndexCurrent { get { return 1; } }

        public override IEnumerable<MeasuredIon> GetDefaults(int revisionIndex)
        {
            var listDefaults = new List<MeasuredIon>(new[] {NTERM_PROLINE, CTERM_GLU_ASP});
            if (revisionIndex < 1)
                return listDefaults;

            listDefaults.AddRange(new[]
            {
                ITRAQ_114, ITRAQ_115, ITRAQ_116, ITRAQ_117,
                TMT_126, TMT_127_L, TMT_127_H, TMT_128_L, TMT_128_H, TMT_129_L, TMT_129_H, TMT_130_L, TMT_130_H, TMT_131
            });
            return listDefaults;
        }

        public override MeasuredIon EditItem(Control owner, MeasuredIon item,
            IEnumerable<MeasuredIon> existing, object tag)
        {
            using (EditMeasuredIonDlg editIon = new EditMeasuredIonDlg(existing ?? this) { MeasuredIon = item })
            {
                if (editIon.ShowDialog(owner) == DialogResult.OK)
                    return editIon.MeasuredIon;
            }

            return null;
        }

        public override MeasuredIon CopyItem(MeasuredIon item)
        {
            return (MeasuredIon)item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.MeasuredIonList_Title_Edit_Special_Ions; } }

        public override string Label { get { return Resources.MeasuredIonList_Label_Special_ion; } }
    }

    public sealed class IsotopeEnrichmentsList : SettingsList<IsotopeEnrichments>
    {
        public static IsotopeEnrichments GetDefault()
        {
            return IsotopeEnrichments.DEFAULT;
        }

        public override IEnumerable<IsotopeEnrichments> GetDefaults(int revisionIndex)
        {
            return new[] { GetDefault() };
        }

        public override IsotopeEnrichments EditItem(Control owner, IsotopeEnrichments item,
            IEnumerable<IsotopeEnrichments> existing, object tag)
        {
            using (EditIsotopeEnrichmentDlg editEnrichment = new EditIsotopeEnrichmentDlg(existing ?? this) { Enrichments = item })
            {
                if (editEnrichment.ShowDialog(owner) == DialogResult.OK)
                    return editEnrichment.Enrichments;
            }

            return null;
        }

        public override IsotopeEnrichments CopyItem(IsotopeEnrichments item)
        {
            return (IsotopeEnrichments)item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.IsotopeEnrichmentsList_Title_Edit_Isotope_Labeling_Enrichments; } }

        public override string Label { get { return Resources.IsotopeEnrichmentsList_Label_Isotope_labeling_entrichment; } }        
    }

    public sealed class IsolationSchemeList : SettingsList<IsolationScheme>
    {
        public override int RevisionIndexCurrent { get { return 2; } }

        public override IEnumerable<IsolationScheme> GetDefaults(int revisionIndex)
        {
            var isolationSchemeList = new List<IsolationScheme>
            {
                new IsolationScheme(null, new IsolationWindow[0], IsolationScheme.SpecialHandlingType.ALL_IONS) // Not L10N
            };
            
            if (revisionIndex == 0)
                return isolationSchemeList;

            if (revisionIndex > 1)
            {
                isolationSchemeList.Add(new IsolationScheme(Resources.IsolationSchemeList_GetDefaults_Results_only));
                isolationSchemeList.Add(new IsolationScheme(Resources.IsolationSchemeList_GetDefaults_Results__0_5_margin_, 0.5, null, true));
            }

            AddScheme(isolationSchemeList, Resources.IsolationSchemeList_GetDefaults_SWATH__15_m_z_, 0.5,
                396, 410, 5,
                410, 424, 5,
                424, 438, 5,
                438, 452, 5,
                452, 466, 5,
                466, 480, 5,
                480, 494, 5,
                494, 508, 5,
                508, 522, 5,
                522, 536, 5,
                536, 550, 5,
                550, 564, 5,
                564, 578, 5,
                578, 592, 5,
                592, 606, 5,
                606, 620, 5,
                620, 634, 5,
                634, 648, 5,
                648, 662, 5,
                662, 676, 5,
                676, 690, 5,
                690, 704, 5,
                704, 718, 5,
                718, 732, 5,
                732, 746, 5,
                746, 760, 5,
                760, 774, 5,
                774, 788, 5,
                788, 802, 5,
                802, 816, 5,
                816, 830, 5,
                830, 844, 5,
                844, 858, 5,
                858, 872, 5,
                872, 886, 5,
                886, 900, 5,
                900, 914, 5,
                914, 928, 5,
                928, 942, 5,
                942, 956, 5,
                956, 970, 5,
                970, 984, 5,
                984, 998, 5,
                998, 1012, 10,
                1012, 1026, 10,
                1026, 1040, 10,
                1040, 1054, 10,
                1054, 1068, 10,
                1068, 1082, 10,
                1082, 1096, 10,
                1096, 1110, 10,
                1110, 1124, 10,
                1124, 1138, 10,
                1138, 1152, 10,
                1152, 1166, 10,
                1166, 1180, 10,
                1180, 1194, 10,
                1194, 1208, 10,
                1208, 1222, 10,
                1222, 1236, 10,
                1236, 1249, 10);

            AddScheme(isolationSchemeList, Resources.IsolationSchemeList_GetDefaults_SWATH__25_m_z_, 0.5,
                400, 424, 5,
                424, 448, 5,
                448, 472, 5,
                472, 496, 5,
                496, 520, 5,
                520, 544, 5,
                544, 568, 5,
                568, 592, 5,
                592, 616, 5,
                616, 640, 5,
                640, 664, 5,
                664, 688, 5,
                688, 712, 5,
                712, 736, 5,
                736, 760, 5,
                760, 784, 5,
                784, 808, 5,
                808, 832, 5,
                832, 856, 5,
                856, 880, 5,
                880, 904, 5,
                904, 928, 5,
                928, 952, 5,
                952, 976, 5,
                976, 1000, 10,
                1000, 1024, 10,
                1024, 1048, 10,
                1048, 1072, 10,
                1072, 1096, 10,
                1096, 1120, 10,
                1120, 1144, 10,
                1144, 1168, 10,
                1168, 1192, 10,
                1192, 1216, 10,
                1216, 1240, 10);

            AddScheme(isolationSchemeList, Resources.IsolationSchemeList_GetDefaults_SWATH__VW_64_, 0.5,
                400, 409, 5,
                409, 416, 5,
                416, 423, 5,
                423, 430, 5,
                430, 437, 5,
                437, 444, 5,
                444, 451, 5,
                451, 458, 5,
                458, 465, 5,
                465, 471, 5,
                471, 477, 5,
                477, 483, 5,
                483, 489, 5,
                489, 495, 5,
                495, 501, 5,
                501, 507, 5,
                507, 514, 5,
                514, 521, 5,
                521, 528, 5,
                528, 535, 5,
                535, 542, 5,
                542, 549, 5,
                549, 556, 5,
                556, 563, 5,
                563, 570, 5,
                570, 577, 5,
                577, 584, 5,
                584, 591, 5,
                591, 598, 5,
                598, 605, 5,
                605, 612, 5,
                612, 619, 5,
                619, 626, 5,
                626, 633, 5,
                633, 640, 5,
                640, 647, 5,
                647, 654, 5,
                654, 663, 5,
                663, 672, 5,
                672, 681, 5,
                681, 690, 5,
                690, 699, 5,
                699, 708, 5,
                708, 722, 5,
                722, 736, 5,
                736, 750, 5,
                750, 764, 5,
                764, 778, 5,
                778, 792, 5,
                792, 806, 5,
                806, 825, 10,
                825, 844, 10,
                844, 863, 10,
                863, 882, 10,
                882, 901, 10,
                901, 920, 10,
                920, 939, 10,
                939, 968, 10,
                968, 997, 10,
                997, 1026, 10,
                1026, 1075, 10,
                1075, 1124, 10,
                1124, 1173, 10,
                1173, 1249, 10);

            AddScheme(isolationSchemeList, Resources.IsolationSchemeList_GetDefaults_SWATH__VW_100_, 0.5,
                400, 406, 5,
                406, 412, 5,
                412, 418, 5,
                418, 424, 5,
                424, 430, 5,
                430, 436, 5,
                436, 442, 5,
                442, 448, 5,
                448, 454, 5,
                454, 459, 5,
                459, 464, 5,
                464, 469, 5,
                469, 474, 5,
                474, 479, 5,
                479, 484, 5,
                484, 489, 5,
                489, 494, 5,
                494, 499, 5,
                499, 504, 5,
                504, 509, 5,
                509, 514, 5,
                514, 519, 5,
                519, 524, 5,
                524, 529, 5,
                529, 534, 5,
                534, 539, 5,
                539, 544, 5,
                544, 549, 5,
                549, 554, 5,
                554, 559, 5,
                559, 564, 5,
                564, 569, 5,
                569, 574, 5,
                574, 579, 5,
                579, 584, 5,
                584, 589, 5,
                589, 594, 5,
                594, 599, 5,
                599, 604, 5,
                604, 609, 5,
                609, 614, 5,
                614, 619, 5,
                619, 624, 5,
                624, 629, 5,
                629, 634, 5,
                634, 639, 5,
                639, 644, 5,
                644, 649, 5,
                649, 654, 5,
                654, 660, 5,
                660, 666, 5,
                666, 672, 5,
                672, 678, 5,
                678, 684, 5,
                684, 690, 5,
                690, 696, 5,
                696, 702, 5,
                702, 708, 5,
                708, 714, 5,
                714, 720, 5,
                720, 726, 5,
                726, 732, 5,
                732, 738, 5,
                738, 744, 5,
                744, 750, 5,
                750, 756, 5,
                756, 763, 5,
                763, 770, 5,
                770, 777, 5,
                777, 784, 5,
                784, 791, 5,
                791, 798, 5,
                798, 805, 5,
                805, 812, 5,
                812, 819, 5,
                819, 826, 5,
                826, 834, 5,
                834, 842, 5,
                842, 850, 5,
                850, 858, 5,
                858, 867, 5,
                867, 876, 5,
                876, 885, 5,
                885, 894, 5,
                894, 903, 5,
                903, 914, 5,
                914, 925, 5,
                925, 936, 5,
                936, 950, 5,
                950, 964, 5,
                964, 978, 5,
                978, 992, 5,
                992, 1011, 10,
                1011, 1030, 10,
                1030, 1054, 10,
                1054, 1078, 10,
                1078, 1117, 10,
                1117, 1156, 10,
                1156, 1200, 10,
                1200, 1249, 10);

            return isolationSchemeList;
        }

        public override int ExcludeDefaults { get { return 1; } }

        public override IsolationScheme EditItem(Control owner, IsolationScheme item,
            IEnumerable<IsolationScheme> existing, object tag)
        {
            using (var editIsolationScheme = new EditIsolationSchemeDlg(existing ?? this) { IsolationScheme = item })
            {
                if (editIsolationScheme.ShowDialog(owner) == DialogResult.OK)
                    return editIsolationScheme.IsolationScheme;
            }

            return null;
        }

        public override IsolationScheme CopyItem(IsolationScheme item)
        {
            return (IsolationScheme)item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.IsolationSchemeList_Title_Edit_Isolation_Scheme; } }

        public override string Label { get { return Resources.IsolationSchemeList_Label_Isolation_scheme; } }

        private void AddScheme(IList<IsolationScheme> isolationSchemeList, string name, double margin,
            params double[] values)
        {
            var isolationWindows = new IsolationWindow[values.Length/3];
            for (int i = 0; i < isolationWindows.Length; i++)
            {
                int j = i*3;
                isolationWindows[i] = new IsolationWindow(values[j], values[j+1], null, margin, null, values[j+2]);
            }
            isolationSchemeList.Add(new IsolationScheme(name, isolationWindows));
        }
    }

    public sealed class SrmSettingsList : SerializableSettingsList<SrmSettings>
    {
        public const string EXT_SETTINGS = ".skys"; // Not L10N
        private static readonly SrmSettings DEFAULT = new SrmSettings
            (
                "<placeholder>",    // Not L10N
                new PeptideSettings
                (
                    EnzymeList.GetDefault(),
                    new DigestSettings(0, false),
                    new PeptidePrediction(null, null, true, PeptidePrediction.DEFAULT_MEASURED_RT_WINDOW, false, IonMobilityWindowWidthCalculator.EMPTY),
                    new PeptideFilter
                    (
                        25,  // ExcludeNTermAAs
                         8,  // MinPeptideLength
                        25,  // MaxPeptideLength
                        new PeptideExcludeRegex[0], // Exclusions
                        true, // AutoSelect
                        PeptideFilter.PeptideUniquenessConstraint.none // Peptide uniqueness constraint measured against background proteome
                    ),
                    new PeptideLibraries
                    (
                        PeptidePick.library,    // PeptidePick
                        null,                   // PeptideRankId
                        null,                   // PeptideCount
                        false,                  // HasDocumentLibrary
                        new LibrarySpec[0],     // LibrarySpecs
                        new Library[0]          // Libraries
                    ), 
                    new PeptideModifications
                    (
                        StaticModList.GetDefaultsOn(),
                        PeptideModifications.DEFAULT_MAX_VARIABLE_MODS,  // MaxVariableMods
                        PeptideModifications.DEFAULT_MAX_NEUTRAL_LOSSES, // MaxNeutralLosses
                        new[] {new TypedModifications(IsotopeLabelType.heavy, HeavyModList.GetDefaultsOn())},
                        new[] {IsotopeLabelType.heavy}
                    ),
                    new PeptideIntegration(null), 
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
                        null,
                        OptimizationLibraryList.GetDefault(),
                        OptimizedMethodType.None
                    ),
                    new TransitionFilter
                    (
                        new[] { Adduct.DOUBLY_PROTONATED,  }, // PeptidePrecursorCharges
                        new[] { Adduct.SINGLY_PROTONATED,  }, // PeptideProductCharges
                        new[] { IonType.y }, // PeptideFragmentTypes
                        new[] { Adduct.M_PLUS_H, }, // SmallMoleculePrecursorCharges
                        new[] { Adduct.M_PLUS, }, // SmallMoleculeProductCharges
                        Transition.MOLECULE_ION_TYPES, // SmallMoleculeFragmentTypes
                        TransitionFilter.DEFAULT_START_FINDER,  // FragmentRangeFirst
                        TransitionFilter.DEFAULT_END_FINDER,    // FragmentRangeLast
                        new[] {MeasuredIonList.NTERM_PROLINE},  // MeasuredIon
                        0,      // PrecursorMzWindow
                        false,  // ExclusionUseDIAWindow
                        true    // AutoSelect
                    ),
                    new TransitionLibraries
                    (
                        0.5,    // IonMatchTolerance
                        0,      // MinIonCount
                        3,      // IonCount
                        TransitionLibraryPick.all  // Pick
                    ),
                    new TransitionIntegration(), 
                    new TransitionInstrument
                    (
                        50,   // MinMz
                        1500, // MaxMz
                        false, // IsDynamicMin
                        TransitionInstrument.DEFAULT_MZ_MATCH_TOLERANCE, // MzMatchTolerance
                        null,  // MaxTransitions
                        null,  // MaxInclusions
                        null,  // MinTime
                        null   // MaxTime
                    ),
                    TransitionFullScan.DEFAULT
                ),
                DataSettings.DEFAULT,
                DocumentRetentionTimes.EMPTY
            );

        public static string DefaultName
        {
            get { return Resources.SrmSettingsList_DefaultName_Default; }
        }

        /// <summary>
        /// For tests written before v0.7, these settings mimic the v0.6 settings
        /// </summary>
        public static SrmSettings GetDefault0_6()
        {
            return GetDefault().ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(new[] {MeasuredIonList.NTERM_PROLINE_LEGACY}));
        }

        public static SrmSettings GetDefault()
        {
            return (SrmSettings)DEFAULT.ChangeName(DefaultName);
        }

        public override IEnumerable<SrmSettings> GetDefaults(int revisionIndex)
        {
            return new[] { GetDefault() };
        }

        public override int ExcludeDefaults { get { return 1; } }

        public override string Title { get { return Resources.SrmSettingsList_Title_Edit_Settings; } }

        public override string Label { get { return Resources.SrmSettingsList_Label_Saved_Settings; } }

        public override Type SerialType { get { return typeof(SrmSettingsList); } }

        public override ICollection<SrmSettings> CreateEmptyList()
        {
            return new SrmSettingsList();
        }
    }

    /// <summary>
    /// This class exists to keep serialized shared .skyr files from adding
    /// defaults changing with revisionIndex intended for only the user.config file.
    /// </summary>
    [XmlRoot("ReportSpecList")]
    public class SkyrFileList : ReportSpecList
    {
        public override IEnumerable<ReportSpec> GetDefaults(int revisionIndex)
        {
            return new ReportSpec[0];
        }
    }

    public class ReportSpecList : SerializableSettingsList<ReportSpec>, IItemEditor<ReportSpec>
    {
        public const string EXT_REPORTS = ".skyr"; // Not L10N
        // CONSIDER: Consider localizing tool report names which is not possible at the moment.
        public static string SRM_COLLIDER_REPORT_NAME
        {
            get { return "SRM Collider Input"; } // Not L10N
        }

        public static string QUASAR_REPORT_NAME
        {
            get { return "QuaSAR Input"; } // Not L10N
        }

        public override int RevisionIndexCurrent { get { return 2; } }

        public override IEnumerable<ReportSpec> GetDefaults(int revisionIndex)
        {
            Type tablePep = typeof (DbPeptide);
            Type tablePepRes = typeof (DbPeptideResult);
            Type tableTran = typeof (DbTransition);
            Type tableTranRes = typeof (DbTransitionResult);

            var listDefaults = new List<ReportSpec>(new[]
                       { // ReSharper disable NonLocalizedString
                    new ReportSpec(
                        Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results,
                                          new QueryDef
                                              {
                                                  Select = new[]
                                                               {
                                        new ReportColumn(tablePep, "Sequence"),
                                        new ReportColumn(tablePep, "Protein", "Name"),
                                        new ReportColumn(tablePepRes, "ProteinResult", "ReplicateName"),
                                        new ReportColumn(tablePepRes, "PeptidePeakFoundRatio"),
                                        new ReportColumn(tablePepRes, "PeptideRetentionTime"),
                                        new ReportColumn(tablePepRes, "RatioToStandard"),
                                                               }
                                              }),
                    new ReportSpec(
                        Resources.ReportSpecList_GetDefaults_Peptide_RT_Results,
                                          new QueryDef
                                              {
                                                  Select = new[]
                                                               {
                                        new ReportColumn(tablePep, "Sequence"),
                                        new ReportColumn(tablePep, "Protein", "Name"),
                                        new ReportColumn(tablePepRes, "ProteinResult", "ReplicateName"),
                                        new ReportColumn(tablePep, "PredictedRetentionTime"),
                                        new ReportColumn(tablePepRes, "PeptideRetentionTime"),
                                        new ReportColumn(tablePepRes, "PeptidePeakFoundRatio"),
                                                               }
                                              }),
                    new ReportSpec(
                        Resources.ReportSpecList_GetDefaults_Transition_Results,
                                          new QueryDef
                                              {
                                                  Select = new[]
                                                               {
                                        new ReportColumn(tableTran, "Precursor", "Peptide", "Sequence"),
                                        new ReportColumn(tableTran, "Precursor", "Peptide", "Protein", "Name"),
                                        new ReportColumn(tableTranRes, "PrecursorResult", "PeptideResult", "ProteinResult", "ReplicateName"),
                                        new ReportColumn(tableTran, "Precursor", "Mz"),
                                        new ReportColumn(tableTran, "Precursor", "Charge"),
                                        new ReportColumn(tableTran, "ProductMz"),
                                        new ReportColumn(tableTran, "ProductCharge"),
                                        new ReportColumn(tableTran, "FragmentIon"),
                                        new ReportColumn(tableTranRes, "RetentionTime"),
                                        new ReportColumn(tableTranRes, "Area"),
                                        new ReportColumn(tableTranRes, "Background"),
                                        new ReportColumn(tableTranRes, "PeakRank"),
                                                               }
                                              }),
                        // ReSharper restore NonLocalizedString
                        });
            
            if (revisionIndex < 1)
                return listDefaults;

            // ReSharper disable NonLocalizedString
            listDefaults.AddRange(new[]
                                    {
                                        (ReportSpec) DeserializeItem(
@"<report name=""SRM Collider Input"">
    <table name=""T1"">DbTransition</table>
    <select>
      <column name=""T1"">Precursor.Peptide.Sequence</column>
      <column name=""T1"">Precursor.ModifiedSequence</column>
      <column name=""T1"">Precursor.Charge</column>
      <column name=""T1"">Precursor.Mz</column>
      <column name=""T1"">ProductMz</column>
      <column name=""T1"">LibraryIntensity</column>
      <column name=""T1"">ProductCharge</column>
      <column name=""T1"">FragmentIon</column>
    </select>
  </report>").ChangeName(SRM_COLLIDER_REPORT_NAME),
                                    });

            if (revisionIndex < 2)
                return listDefaults;

            listDefaults.Add((ReportSpec)DeserializeItem(
@"<report name=""Peak Boundaries"">
    <table name=""T1"">DbPrecursorResult</table>
    <table name=""T2"">DbPrecursor</table>
    <select>
      <column name=""T1"">PeptideResult.ProteinResult.FileName</column>
      <column name=""T2"">Peptide.ModifiedSequence</column>
      <column name=""T1"">MinStartTime</column>
      <column name=""T1"">MaxEndTime</column>
      <column name=""T2"">Charge</column>
      <column name=""T2"">IsDecoy</column>
    </select>
  </report>").ChangeName(Resources.ReportSpecList_GetDefaults_Peak_Boundaries));
            // ReSharper restore NonLocalizedString

            return listDefaults;
        }

        public ReportSpec NewItem(Control owner, IEnumerable<ReportSpec> existing, object tag)
        {
            return EditItem(owner, null, existing, tag);
        }

        public ReportSpec EditItem(Control owner, ReportSpec item,
            IEnumerable<ReportSpec> existing, object tag)
        {
            throw new InvalidOperationException();
        }

        public ReportSpec CopyItem(ReportSpec item)
        {
            return (ReportSpec) item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.ReportSpecList_Title_Edit_Reports; } }

        public override string Label { get { return Resources.ReportSpecList_Label_Report; } }

        public override Type SerialType { get { return typeof(ReportSpecList); } }

        /// <summary>
        /// Returns the Type <see cref="SkyrFileList"/> to keep from adding changing
        /// defaults to exported .skyr files.
        /// </summary>
        public override Type DeserialType { get { return typeof(SkyrFileList); } }

        public override ICollection<ReportSpec> CreateEmptyList()
        {
            return new ReportSpecList {RevisionIndex = RevisionIndexCurrent};
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
            using (var dlg = new DefineAnnotationDlg(existing ?? this))
            {
                dlg.SetAnnotationDef(item);
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.GetAnnotationDef();
                }

                return null;
            }
        }

        public override AnnotationDef CopyItem(AnnotationDef item)
        {
            return (AnnotationDef)item.ChangeName(string.Empty);
        }

        public override string Title { get { return Resources.AnnotationDefList_Title_Define_Annotations; } }

        public override string Label { get { return Resources.AnnotationDefList_Label_Annotations; } }

        public Type SerialType { get { return typeof(AnnotationDef); } }

        public Type DeserialType { get { return SerialType; } }

        public ICollection<AnnotationDef> CreateEmptyList()
        {
            return new AnnotationDefList();
        }
    }

    public abstract class SettingsListNotifying<TItem> : SettingsList<TItem>
        where TItem : IKeyContainer<string>, IXmlSerializable
    {
        public event EventHandler<EventArgs> ListChanged;

        private void FireListChanged()
        {
            if (ListChanged != null)
                ListChanged(this, new EventArgs());
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            FireListChanged();
        }

        protected override void InsertItem(int index, TItem item)
        {
            base.InsertItem(index, item);
            FireListChanged();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            FireListChanged();
        }

        protected override void SetItem(int index, TItem item)
        {
            base.SetItem(index, item);
            FireListChanged();
        }        
    }

    public abstract class SerializableSettingsList<TItem> : SettingsListBase<TItem>, IListSerializer<TItem>
        where TItem : IKeyContainer<string>, IXmlSerializable
    {
        #region Implementation of IListSerializer<TItem>

        public abstract Type SerialType { get; }

        public virtual Type DeserialType { get { return SerialType; } }

        public abstract ICollection<TItem> CreateEmptyList();

        #endregion

        public bool ImportFile(string fileName, Func<IList<string>, IList<string>> whichToNotOverWrite)
        {
            if (String.IsNullOrEmpty(fileName))
                return false;

            IList<TItem> loadedItems;
            try
            {
                using (var stream = File.OpenRead(fileName))
                {
                    loadedItems = DeserializeItems(stream);
                }
            }
            catch (Exception exception)
            {
                throw new IOException(String.Format(Resources.SerializableSettingsList_ImportFile_Failure_loading__0__, fileName), exception);
            }
            // Check for and warn about existing reports.
            var existing = (from item in loadedItems
                            where ContainsKey(item.GetKey())
                            select item.GetKey()).ToList();

            IList<string> toSkip = new string[0];
            if (existing.Count > 0)
            {
                toSkip = whichToNotOverWrite(existing);
                if (toSkip == null)
                    return false;
            }
            foreach (TItem item in loadedItems)
            {
                // Skip anything still in the toSkip list
                if (toSkip.Contains(item.GetKey()))
                    continue;
                RemoveKey(item.GetKey());
                Add(item);
            }
            return true;
        }

        protected virtual IList<TItem> DeserializeItems(Stream stream)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(DeserialType);
            return (SerializableSettingsList<TItem>)xmlSerializer.Deserialize(stream);
        }

        //Todo
        public void RemoveKey(string name)
        {
            for (int i = 0; i < Count; i++)
            {
                if (Equals(name, this[i].GetKey()))
                {
                    RemoveAt(i);
                    break;
                }
            }
        }

        public static TItem DeserializeItem(string s)
        {
            s = XmlUtil.XML_DIRECTIVE + s;

            XmlSerializer ser = new XmlSerializer(typeof(TItem));
            using (TextReader reader = new StringReader(s))
            {
                return (TItem)ser.Deserialize(reader);
            }
        }
    }

    public abstract class SettingsList<TItem>
        : SettingsListBase<TItem>, IItemEditor<TItem>
        where TItem : IKeyContainer<string>, IXmlSerializable
    {
        public static string ELEMENT_NONE { get { return "None"; } }    // Not L10N - serialized to XML

        #region IItemEditor<T> Members

        public TItem NewItem(Control owner, IEnumerable<TItem> existing, object tag)
        {
            return EditItem(owner, default(TItem), existing, tag);
        }

        public abstract TItem EditItem(Control owner, TItem item, IEnumerable<TItem> existing, object tag);

        public abstract TItem CopyItem(TItem item);

        #endregion

        public override bool AllowReset { get { return true; } }
    }

    public abstract class SettingsListBase<TItem>
        : XmlMappedList<string, TItem>, IListDefaults<TItem>, IListEditor<TItem>, IListEditorSupport
        where TItem : IKeyContainer<string>, IXmlSerializable
    {
        public virtual void AddDefaults()
        {
            AddRange(GetDefaults());
        }

        public IEnumerable<TItem> GetDefaults()
        {
            return GetDefaults(RevisionIndexCurrent);
        }

        #region IListDefaults<TValue> Members

        public virtual int RevisionIndexCurrent { get { return 0; } }

        public abstract IEnumerable<TItem> GetDefaults(int revisionIndex);

        public virtual string GetDisplayName(TItem item)
        {
            return item.GetKey();
        }

        #endregion

        #region IListEditor<T> Members

        public IEnumerable<TItem> EditList(Control owner, object tag)
        {
            using (var dlg = new EditListDlg<SettingsListBase<TItem>, TItem>(this, tag))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    var listNew = dlg.GetAllEdited().ToArray();
                    if (AcceptList(owner, listNew))
                        return listNew;
                }
                return null;
            }
        }

        public virtual bool AcceptList(Control owner, IList<TItem> listNew)
        {
            return true;
        }

        public virtual int ExcludeDefaults { get { return 0; } }

        #endregion

        #region IListEditorSupport Members

        public abstract string Title { get; }

        public abstract string Label { get; }

        public virtual bool AllowReset { get { return false; } }

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

        private void MergeDifferences(IEnumerable<TItem> itemsNew, IEnumerable<TItem> itemsOld)
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
