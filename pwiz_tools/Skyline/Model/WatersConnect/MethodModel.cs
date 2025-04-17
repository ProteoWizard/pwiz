using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace pwiz.Skyline.Model.WatersConnect
{
    public class MethodModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("destinationFolderId")]
        public string DestinationFolderId { get; set; }

        [JsonProperty("templateMethodId")]
        public string TemplateId { get; set; }
    }

    public class Target
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [ColumnName("peptide.seq")]
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("group")]
        public string Group { get; set; }

        [ColumnName("precursor.retT")]
        [JsonProperty("retentionTime")]
        public double RetentionTime { get; set; }

        [ColumnName("collision_energy")]
        [JsonProperty("collisionEnergy")]
        public double CollisionEnergy { get; set; }

        [ColumnName("cone_voltage")]
        [JsonProperty("coneVoltage")]
        public double ConeVoltage { get; set; }

        [JsonProperty("polarity")]
        public string Polarity { get; set; }

        [JsonProperty("startTime")]
        public double StartTime { get; set; }

        [JsonProperty("endTime")]
        public double EndTime { get; set; }

        [JsonProperty("transitions")]
        public List<Transition> Transitions;
    }

    public class Transition
    {
        [ColumnName("precursor.mz")]
        [JsonProperty("precursorMz")]
        public string PrecursorMz { get; set; }

        [ColumnName("product.m_z")]
        [JsonProperty("productMz")]
        public double ProductMz { get; set; }

        [ColumnName("")]
        [JsonProperty("isQuanIon")]
        public double IsQuanIon { get; set; }

        [ColumnName("")]
        [JsonProperty("dwellTime")]
        public double DwellTime { get; set; }

        [ColumnName("")]
        [JsonProperty("autoDwell")]
        public double AutoDwell { get; set; }
    }

    public class ColumnNameAttribute : Attribute
    {
        public ColumnNameAttribute(string columnName)
        {
            ColumnName = columnName;
        }
        public string ColumnName { get; }
        public int ColumnIndex { get; private set; } = -1;

        public void InitIndex(string headers)
        {
            if (string.IsNullOrEmpty(headers))
                return;

            var headerList = headers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < headerList.Length; i++)
            {
                if (headerList[i].Equals(ColumnName))
                {
                    ColumnIndex = i;
                    break;
                }
            }
        }
    }
}