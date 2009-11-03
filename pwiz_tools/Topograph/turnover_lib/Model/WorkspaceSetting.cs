using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class WorkspaceSetting : EntityModel<DbSetting>
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

        protected override void Load(DbSetting entity)
        {
            _name = entity.Name;
            _value = entity.Value;
        }

        protected override DbSetting UpdateDbEntity(ISession session)
        {
            var dbSetting = base.UpdateDbEntity(session);
            dbSetting.Name = Name;
            dbSetting.Value = Value;
            return dbSetting;
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
    }
}
