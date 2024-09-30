using System;

namespace pwiz.Skyline.Model.Lib.BlibData
{
    public class DbScoreTypes : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbScoreTypes); }
        }
        public virtual string ScoreType { get; set; }
        public virtual string ProbabilityType { get; set; }
    }
}