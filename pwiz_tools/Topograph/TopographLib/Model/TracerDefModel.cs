using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class TracerDefModel : AbstractSetting<DbTracerDef> 
    {
        public static readonly ModelProperty PropName = Property<TracerDefModel, string>(m => m.Name, (m, v) => m.Name = v, e => e.Name,
                                              (e, v) => e.Name = v);
        public static readonly ModelProperty PropTracerSymbol = Property<TracerDefModel, string>(m => m.TracerSymbol, (m, v) => m.TracerSymbol = v, e => e.TracerSymbol,
                                              (e, v) => e.TracerSymbol = v);
        public static readonly ModelProperty PropDeltaMass = Property<TracerDefModel, double>(m => m.DeltaMass, (m, v) => m.DeltaMass = v,
                                                          e => e.DeltaMass, (e, v) => e.DeltaMass = v);
        public static readonly ModelProperty PropAtomCount = Property<TracerDefModel, int>(m => m.AtomCount, (m, v) => m.AtomCount = v, e => e.AtomCount,
                                              (e, v) => e.AtomCount = v);
        public static readonly ModelProperty PropAtomPercentEnrichment =                 Property<TracerDefModel, double>(m => m.AtomPercentEnrichment, (m, v) => m.AtomPercentEnrichment = v, e => e.AtomPercentEnrichment,
                                              (e, v) => e.AtomPercentEnrichment = v);

        public static readonly ModelProperty PropInitialEnrichment = Property<TracerDefModel, double>(m => m.InitialEnrichment, (m, v) => m.InitialEnrichment = v, e => e.InitialEnrichment,
                                          (e, v) => e.InitialEnrichment = v);
        public static readonly ModelProperty PropFinalEnrichment = Property<TracerDefModel, double>(m => m.FinalEnrichment, (m, v) => m.FinalEnrichment = v, e => e.FinalEnrichment,
                                              (e, v) => e.FinalEnrichment = v);
        public static readonly ModelProperty PropIsotopesEluteEarlier = Property<TracerDefModel, bool>(m => m.IsotopesEluteEarlier, (m, v) => m.IsotopesEluteEarlier = v, e => e.IsotopesEluteEarlier,
                                  (e, v) => e.IsotopesEluteEarlier = v);
        public static readonly ModelProperty PropIsotopesEluteLater = Property<TracerDefModel, bool>(m => m.IsotopesEluteLater, (m, v) => m.IsotopesEluteLater = v, e => e.IsotopesEluteLater,
                                              (e, v) => e.IsotopesEluteLater = v);






        public TracerDefModel(Workspace workspace, DbTracerDef dbTracerDef) : base(workspace, dbTracerDef)
        {
        }

        public TracerDefModel(Workspace workspace) : base(workspace)
        {
        }

        public WorkspaceVersion UpdateFromUi(DbTracerDef dbTracerDef, WorkspaceVersion workspaceVersion)
        {
            workspaceVersion = GetWorkspaceVersion(workspaceVersion, dbTracerDef);
            foreach (var mp in GetModelProperties())
            {
                mp.ModelSetter.Invoke(this, mp.EntityGetter.Invoke(dbTracerDef));
            }
            return workspaceVersion;
        }

        public override WorkspaceVersion GetWorkspaceVersion(WorkspaceVersion workspaceVersion, DbTracerDef dbTracerDef)
        {
            if (dbTracerDef == null)
            {
                return workspaceVersion.IncMassVersion();
            }
            if (dbTracerDef.TracerSymbol != TracerSymbol
                || dbTracerDef.DeltaMass != DeltaMass
                || dbTracerDef.AtomPercentEnrichment != AtomPercentEnrichment
                || dbTracerDef.AtomCount != AtomCount)
            {
                return workspaceVersion.IncMassVersion();
            }
            if (dbTracerDef.IsotopesEluteEarlier != IsotopesEluteEarlier
                || dbTracerDef.IsotopesEluteLater != IsotopesEluteLater)
            {
                return workspaceVersion.IncChromatogramPeakVersion();
            }
            if (dbTracerDef.Name != Name 
                || dbTracerDef.InitialEnrichment != InitialEnrichment
                || dbTracerDef.FinalEnrichment != FinalEnrichment)
            {
                return workspaceVersion.IncEnrichmentVersion();
            }
            return workspaceVersion;
        }

        public DbTracerDef ToDbTracerDef()
        {
            var result = new DbTracerDef();
            foreach (var modelProperty in GetModelProperties())
            {
                modelProperty.EntitySetter.Invoke(result, modelProperty.ModelGetter.Invoke(this));
            }
            return result;
        }

        protected override IEnumerable<ModelProperty> GetModelProperties()
        {
            yield return PropName;
            yield return PropTracerSymbol;
            yield return PropDeltaMass;
            yield return PropAtomCount;
            yield return PropAtomPercentEnrichment;
            yield return PropInitialEnrichment;
            yield return PropFinalEnrichment;
            yield return PropIsotopesEluteEarlier;
            yield return PropIsotopesEluteLater;
        }

        public String TracerSymbol { get; set; }
        public String Name { get; set; }
        public double DeltaMass { get; set; }
        public virtual int AtomCount { get; set; }
        public virtual double AtomPercentEnrichment { get; set; }
        public virtual double InitialEnrichment { get; set; }
        public virtual double FinalEnrichment { get; set; }
        public virtual bool IsotopesEluteEarlier { get; set; }
        public virtual bool IsotopesEluteLater { get; set; }
        protected override DbTracerDef ConstructEntity(ISession session)
        {
            return new DbTracerDef { Workspace = Workspace.LoadDbWorkspace(session)};
        }
    }
}
