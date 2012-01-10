using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Data
{
    public class DbChangeLog : DbEntity<DbChangeLog>
    {
        public DbChangeLog()
        {
        }
        private void Init(Workspace workspace)
        {
            InstanceIdGuid = workspace.InstanceId;
            TimeStamp = DateTime.Now;
        }
        
        public DbChangeLog(Peptide peptide)
        {
            Init(peptide.Workspace);
            PeptideId = peptide.Id;
        }
        public DbChangeLog(Workspace workspace, DbPeptide dbPeptide)
        {
            Init(workspace);
            PeptideId = dbPeptide.Id;
        }
        public DbChangeLog(MsDataFile msDataFile)
        {
            Init(msDataFile.Workspace);
            MsDataFileId = msDataFile.Id;
        }
        public DbChangeLog(Workspace workspace)
        {
            Init(workspace);
            WorkspaceId = workspace.DbWorkspaceId;
        }
        public DbChangeLog(PeptideAnalysis peptideAnalysis)
        {
            Init(peptideAnalysis.Workspace);
            PeptideAnalysisId = peptideAnalysis.Id;
        }
        public DbChangeLog(Workspace workspace, DbPeptideAnalysis dbPeptideAnalysis)
        {
            Init(workspace);
            PeptideAnalysisId = dbPeptideAnalysis.Id;
        }
        
        
        public virtual DateTime TimeStamp { get; set; }
        public virtual byte[] InstanceIdBytes { get; set; }
        public virtual Guid? InstanceIdGuid { get
        {
            if (InstanceIdBytes == null)
            {
                return null;
            }
            return new Guid(InstanceIdBytes);
        } 
            set
            {
                InstanceIdBytes = value == null ? null : value.Value.ToByteArray();
            }
        }
        public virtual long? PeptideAnalysisId { get; set; }
        public virtual long? PeptideId { get; set; }
        public virtual long? MsDataFileId { get; set; }
        public virtual long? WorkspaceId { get; set; }
    }
}
