using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class WorkspaceSetting : AbstractSetting<DbSetting>
    {
        public const string QueryPrefix = "query:";
        private String _value;
        private String _name;
        public WorkspaceSetting(Workspace workspace, DbSetting dbSetting) : base(workspace, dbSetting)
        {
        }

        public WorkspaceSetting(Workspace workspace) : base (workspace)
        {
        }

        protected override IEnumerable<ModelProperty> GetModelProperties()
        {
            foreach (var modelProperty in base.GetModelProperties())
            {
                yield return modelProperty;
            }
            yield return
                Property<WorkspaceSetting, String>(
                m => m.Name, (m, v) => m.Name = v, 
                e => e.Name, (e, v) => e.Name = v);
            yield return
                Property<WorkspaceSetting, String>(
                m => m.Value, (m, v) => m.Value = v, 
                e => e.Value, (e, v) => e.Value = v);
        }

        protected override DbSetting ConstructEntity(ISession session)
        {
            return new DbSetting
                                {
                                    Name = Name,
                                    Value = Value,
                                    Workspace = Workspace.LoadDbWorkspace(session)
                                };
        }

        public String Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (Parent != null && _name != value)
                {
                    throw new Exception("Can't change the name of a setting");
                }
                _name = value;
            }
        }

        public String Value
        {
            get
            {
                return _value;
            }
            set
            {
                SetIfChanged(ref _value, value);
            }
        }

        public override WorkspaceVersion GetWorkspaceVersion(WorkspaceVersion workspaceVersion, DbSetting entity)
        {
            if (Name == SettingEnum.mass_accuracy.ToString())
            {
                if (entity == null || Value != entity.Value)
                {
                    return workspaceVersion.IncChromatogramPeakVersion();
                }
            }
            if (Name == SettingEnum.err_on_side_of_lower_abundance.ToString())
            {
                if (entity == null || Value != entity.Value)
                {
                    return workspaceVersion.IncEnrichmentVersion();
                }
            }
            if (Name == SettingEnum.max_isotope_retention_time_shift.ToString())
            {
                if (entity == null || Value != entity.Value)
                {
                    return workspaceVersion.IncChromatogramPeakVersion();
                }
            }
            return workspaceVersion;
        }
    }
}
