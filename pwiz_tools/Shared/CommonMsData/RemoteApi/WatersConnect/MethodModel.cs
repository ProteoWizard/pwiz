using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

        [JsonProperty("creationMode")]
        public string CreationMode { get; set; }

        [JsonProperty("scheduleType")]
        public string ScheduleType { get; set; }

        [JsonProperty("auditEntry")]
        public AuditEntryType AuditEntry { get; set; }

        [JsonProperty("compounds")]
        public Compound[] Compounds { get; set; }
    }

    public class AuditEntryType
    {
        [JsonProperty("details")]
        public string Details { get; set; }
    }

    public abstract class ParseableObject
    {
        // Additional information that might be useful during parsing
        public static readonly Dictionary<string, string> ParsingContext = new Dictionary<string, string>();
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
                
                if (columnAttribute != null && reader.HasHeader(columnAttribute.ColumnName) && !columnAttribute.DoNotParse)
                {
                    if (TryParseType(field.PropertyType, reader[columnAttribute.ColumnName], out var result))
                        field.SetValue(this, result);
                    else
                        throw new FormatException(string.Format(@"Cannot parse value {0} of type {1}",
                            reader[columnAttribute.ColumnName], field.PropertyType.FullName));
                }
            }
        }
        public static bool TryParseType(Type propertyType, string fieldValue, out object result)
        {
            result = null;
            if (propertyType == typeof(string))
            {
                result = fieldValue;
                return true;
            }
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(propertyType);
            if (underlyingType != null)
            {
                if (string.IsNullOrEmpty(fieldValue))
                {
                    result = null;
                    return true;
                }
                // Use the underlying type for parsing
                propertyType = underlyingType;
            }
            // Get the static TryParse(string, out T) method for the type
            var tryParseMethod = propertyType.GetMethod(
                "TryParse",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), propertyType.MakeByRefType() },
                null);

            if (tryParseMethod != null)
            {
                // Prepare parameters: fieldValue and an uninitialized value
                var parameters = new [] { fieldValue, Activator.CreateInstance(propertyType) };
                bool success = (bool)tryParseMethod.Invoke(null, parameters);
                if (success)
                {
                    result = parameters[1];
                    return true;
                }
            }
            return false;
        }

    }

    public class Compound : ParseableObject
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [ColumnName("peptide.seq")]
        [JsonProperty("name")]
        public string Name { get; set; }

        [ColumnName("protein.name")]
        [JsonProperty("group")]
        public string Group { get; set; }

        [ColumnName("compound.info", doNotParse:true)]
        [JsonProperty("information")]
        public string Information { get; set; }

        [ColumnName("precursor.retT")]
        [JsonProperty("retentionTime")]
        public float? RetentionTime { get; set; }

        [ColumnName("compound.internal_standard")]
        [JsonProperty("internalStandard")]
        public bool IsInternalStandard { get; set; }

        [JsonProperty("startTime")]
        public float? StartTime { get; set; }

        [ColumnName("rt_window")]
        [JsonProperty("endTime")]
        public float? EndTime { get; set; }

        [JsonProperty("adducts", Order = 8)]
        public List<AdductInfo> Adducts;

        private static readonly Regex _adductRegEx = new Regex(@"\.\[.+\]$");
        public override void ParseObject(DsvStreamReader reader)
        {
            base.ParseObject(reader);
            Name = _adductRegEx.Replace(Name, "");

            var infoHeader = GetColumnName(@"Information");
            if (reader.TryGetColumn(infoHeader, out var info))
            {
                reader.TryGetColumn("document.name", out var documentName);
                Information = string.Format(CultureInfo.CurrentCulture, "Compound from Skyline document {0} - {1}", documentName, info );
            }

            if (RetentionTime.HasValue && RetentionTime.Value == 0)
                RetentionTime = null;
            if (ParsingContext.ContainsKey(@"scheduledMethod") && EndTime.HasValue && RetentionTime.HasValue)
            {
                StartTime = Math.Max(0, RetentionTime.Value - EndTime.Value/2);  //rt_window is stored in the EndTime field
                EndTime = RetentionTime + EndTime / 2; 
            }
            else
            {
                StartTime = EndTime = null;
            }
        }
        
        /// <summary>
        /// Determines if the current line in the reader belongs to the same compound
        /// </summary>
        public bool IsSameCompound(DsvStreamReader reader)
        {
            var colAttribute =  GetType().GetProperty("Name")?.GetCustomAttribute(typeof(ColumnNameAttribute)) as ColumnNameAttribute;
            if (colAttribute != null)
            {
                var name = _adductRegEx.Replace(reader[colAttribute.ColumnName], ""); 
                return string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }

    public class AdductInfo : ParseableObject
    {
        [ColumnName("adduct.info")]
        [JsonProperty("name")]
        public string Name { get; set; }

        [ColumnName("precursor_charge", doNotParse: true)]
        [JsonProperty("polarity")]
        public string Polarity { get; set; }

        [JsonProperty("transitions")]
        public IList<Transition> Transitions { get; set; }

        public override void ParseObject(DsvStreamReader reader)
        {
            base.ParseObject(reader);
            var polarityHeader = GetColumnName(@"Polarity");
            if (reader.TryGetColumn(polarityHeader, out var precursorCharge) &&
                int.TryParse(precursorCharge, out var charge))
            {
                if (charge > 0)
                    Polarity = @"Positive";
                else
                    Polarity = @"Negative";
            }
        }
        public bool IsSameAdduct(DsvStreamReader reader)
        {
            var colAttribute = GetType().GetProperty("Name")?.GetCustomAttribute(typeof(ColumnNameAttribute)) as ColumnNameAttribute;
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
        public float PrecursorMz { get; set; }

        [ColumnName("product.m_z")]
        [JsonProperty("productMz")]
        public float ProductMz { get; set; }

        [ColumnName("is_quant_ion")]
        [JsonProperty("isQuanIon")]
        public bool IsQuanIon { get; set; }

        [JsonProperty("dwellTime")]
        public float? DwellTime { get; set; }

        [JsonProperty("autoDwell")]
        public bool? AutoDwell { get; set; }

        [ColumnName("collision_energy")]
        [JsonProperty("collisionEnergy")]
        public float CollisionEnergy { get; set; }

        [ColumnName("cone_voltage")]
        [JsonProperty("coneVoltage")]
        public float ConeVoltage { get; set; }

    }

    public class ColumnNameAttribute : Attribute
    {
        public ColumnNameAttribute(string columnName, bool doNotParse = false)
        {
            ColumnName = columnName;
            DoNotParse = doNotParse;
        }
        public string ColumnName { get; }
        public bool DoNotParse { get; }
    }
}