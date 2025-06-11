using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using pwiz.Common.DataBinding;


namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    public class MethodModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("destinationFolderId")]
        public string DestinationFolderId { get; set; }

        [JsonProperty("templateMethodVersionId")]
        public string TemplateVersionId { get; set; }

        [JsonProperty("targets")]
        public Target[] Targets { get; set; }
    }

    public abstract class ParseableObject
    {
        protected string GetColumnName(string fieldName)
        {
            var columnAttribute = GetType().GetProperty(fieldName)?.GetCustomAttribute(typeof(ColumnNameAttribute)) as ColumnNameAttribute;
            return columnAttribute?.ColumnName ?? string.Empty;
        }

        public virtual void ParseObject(DsvStreamReader reader)
        {
            if (reader == null)
                return;
            foreach (var field in GetType().GetProperties())
            {
                var columnAttribute = field.GetCustomAttributes(true).ToList().OfType<ColumnNameAttribute>().FirstOrDefault();
                if (columnAttribute != null && reader.HasHeader(columnAttribute.ColumnName))
                    field.SetValue(this, reader[columnAttribute.ColumnName]);
            }
        }

    }

    public class Target : ParseableObject
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [ColumnName("peptide.seq")]
        [JsonProperty("name")]
        public string Name { get; set; }

        [ColumnName("protein.name")]
        [JsonProperty("group")]
        public string Group { get; set; }

        [ColumnName("precursor.retT")]
        [JsonProperty("retentionTime")]
        public string RetentionTime { get; set; }

        [ColumnName("precursor_charge", doNotParse: true)]
        [JsonProperty("polarity")]
        public string Polarity { get; set; }

        [ColumnName("rt_window", doNotParse: true)]
        [JsonProperty("startTime")]
        public string StartTime { get; set; }

        [JsonProperty("endTime")]
        public string EndTime { get; set; }

        [JsonProperty("transitions", Order = 8 )]
        public List<Transition> Transitions;

        public override void ParseObject(DsvStreamReader reader)
        {
            base.ParseObject(reader);

            Id = Guid.NewGuid().ToString();

            var polarityHeader = GetColumnName(@"Polarity");
            if (reader.TryGetColumn(polarityHeader, out var precursorCharge) && int.TryParse(precursorCharge, out var charge))
            {
                if (charge > 0)
                    Polarity = @"Positive";
                else 
                    Polarity = @"Negative";
            }
            
            var rtWindowHeader = GetColumnName(@"StartTime");
            if (reader.TryGetColumn(rtWindowHeader, out var rtWindowString) && !string.IsNullOrEmpty(RetentionTime))
            {
                if (double.TryParse(rtWindowString, out var rtWindow) && double.TryParse(RetentionTime, out var rt))
                {
                    StartTime = Math.Round(rt - rtWindow/2, 2).ToString(CultureInfo.InvariantCulture);
                    EndTime = Math.Round(rt + rtWindow/2, 2).ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        public bool IsSameTarget(DsvStreamReader reader)
        {
            var colAttribute =  GetType().GetProperty("Name")?.GetCustomAttribute(typeof(ColumnNameAttribute)) as ColumnNameAttribute;
            if (colAttribute != null)
            {
                var name = reader[colAttribute.ColumnName];
                return string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }

    public class Transition : ParseableObject
    {
        [ColumnName("precursor.mz")]
        [JsonProperty("precursorMz")]
        public string PrecursorMz { get; set; }

        [ColumnName("product.m_z")]
        [JsonProperty("productMz")]
        public string ProductMz { get; set; }

        [ColumnName("is_quant_ion")]
        [JsonProperty("isQuanIon")]
        public string IsQuanIon { get; set; }

        [ColumnName("")]
        [JsonProperty("dwellTime")]
        public string DwellTime { get; set; }

        [ColumnName("")]
        [JsonProperty("autoDwell")]
        public string AutoDwell { get; set; }

        [ColumnName("collision_energy")]
        [JsonProperty("collisionEnergy")]
        public string CollisionEnergy { get; set; }

        [ColumnName("cone_voltage")]
        [JsonProperty("coneVoltage")]
        public string ConeVoltage { get; set; }

        public override void ParseObject(DsvStreamReader reader)
        {
            base.ParseObject(reader);
            if (string.IsNullOrEmpty(IsQuanIon))
                IsQuanIon = "True";
        }
    }

    public class ColumnNameAttribute : Attribute
    {
        public ColumnNameAttribute(string columnName, bool doNotParse = false)
        {
            ColumnName = columnName;
        }
        public string ColumnName { get; }
    }
}