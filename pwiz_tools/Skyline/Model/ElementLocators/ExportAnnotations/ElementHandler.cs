using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.ElementLocators.ExportAnnotations
{

    public abstract class ElementHandler
    {
        private IDictionary<string, ImportablePropertyInfo> _importableProperties;
        private IDictionary<string, AnnotationDef> _annotationDefs;
        private IDictionary<ElementRef, SkylineObject> _elementMap;
        public ElementHandler(SkylineDataSchema dataSchema)
        {
            DataSchema = dataSchema;
            // ReSharper disable VirtualMemberCallInConstructor
            _importableProperties = ListImportableProperties().ToDictionary(pd => pd.Name, pd=>new ImportablePropertyInfo(pd));
            // ReSharper restore VirtualMemberCallInConstructor
            _annotationDefs = ListAnnotationDefs().ToDictionary(annotationDef => annotationDef.Name);
        }

        public SkylineDataSchema DataSchema { get; private set; }
        protected SrmDocument SrmDocument { get { return DataSchema.Document; } }
        public abstract ElementRef ElementRefPrototype { get; }
        public string Name { get { return ElementRefPrototype.ElementType; } }
        public AnnotationDef.AnnotationTargetSet AnnotationTargets
        {
            get
            {
                return ElementRefPrototype.AnnotationTargets;
            }
        }

        public ImportablePropertyInfo FindProperty(string name)
        {
            ImportablePropertyInfo pd;
            _importableProperties.TryGetValue(name, out pd);
            return pd;
        }

        public IEnumerable<ImportablePropertyInfo> Properties { get { return _importableProperties.Values; } }

        public AnnotationDef FindAnnotation(string name)
        {
            _annotationDefs.TryGetValue(name, out var annotationDef);
            return annotationDef;
        }
        public IEnumerable<AnnotationDef> Annotations { get { return _annotationDefs.Values; } }
        public abstract IEnumerable<SkylineObject> ListElements();

        protected abstract IEnumerable<PropertyDescriptor> ListImportableProperties();

        protected IEnumerable<AnnotationDef> ListAnnotationDefs()
        {
            var targets = AnnotationTargets;
            return SrmDocument.Settings.DataSettings.AnnotationDefs.Where(def =>
                targets.Intersect(def.AnnotationTargets).Any());
        }

        public virtual SkylineObject FindElement(ElementRef elementRef)
        {
            if (_elementMap == null)
            {
                _elementMap = ListElements().ToDictionary(e => e.GetElementRef());
            }
            _elementMap.TryGetValue(elementRef, out var element);
            return element;
        }
        public static IList<ElementHandler> GetElementHandlers(SkylineDataSchema dataSchema)
        {
            return new ElementHandler[]
            {
                    new MoleculeGroupHandler(dataSchema),
                    new MoleculeHandler(dataSchema),
                    new PrecursorHandler(dataSchema),
                    new TransitionHandler(dataSchema),
                    new ReplicateHandler(dataSchema),
                    new ResultFileHandler(dataSchema),
                    new MoleculeResultHandler(dataSchema),
                    new PrecursorResultHandler(dataSchema),
                    new TransitionResultHandler(dataSchema)
            };
        }
    }

    public abstract class AbstractElementHandler<TSkylineObject> : ElementHandler where TSkylineObject : SkylineObject
    {
        public AbstractElementHandler(SkylineDataSchema dataSchema) : base(dataSchema)
        {

        }

        public override IEnumerable<SkylineObject> ListElements()
        {
            return ListAllElements();
        }

        public abstract IEnumerable<TSkylineObject> ListAllElements();
        protected override IEnumerable<PropertyDescriptor> ListImportableProperties()
        {
            return TypeDescriptor.GetProperties(typeof(TSkylineObject))
                .OfType<PropertyDescriptor>()
                .Where(pd => pd.Attributes[typeof(ImportableAttribute)] != null);
        }
    }

    public class MoleculeGroupHandler : AbstractElementHandler<Protein>
    {
        public MoleculeGroupHandler(SkylineDataSchema dataSchema) : base(dataSchema)
        {
        }

        public override IEnumerable<Protein> ListAllElements()
        {
            return SrmDocument.MoleculeGroups.Select(mg =>
                new Protein(DataSchema, new IdentityPath(mg.PeptideGroup)));
        }

        public override ElementRef ElementRefPrototype
        {
            get { return MoleculeGroupRef.PROTOTYPE; }
        }
    }

    public class MoleculeHandler : AbstractElementHandler<Databinding.Entities.Peptide>
    {
        public MoleculeHandler(SkylineDataSchema dataSchema) : base(dataSchema)
        {

        }

        public override IEnumerable<Databinding.Entities.Peptide> ListAllElements()
        {
            return SrmDocument.MoleculeGroups.SelectMany(mg => mg.Molecules.Select(m =>
                new Databinding.Entities.Peptide(DataSchema, new IdentityPath(mg.PeptideGroup, m.Peptide))));
        }

        public override ElementRef ElementRefPrototype => MoleculeRef.PROTOTYPE;
    }

    public class PrecursorHandler : AbstractElementHandler<Precursor>
    {
        public PrecursorHandler(SkylineDataSchema dataSchema) : base(dataSchema)
        {

        }

        public override IEnumerable<Precursor> ListAllElements()
        {
            return SrmDocument.MoleculeGroups.SelectMany(mg => mg.Molecules.SelectMany(m =>
                m.TransitionGroups.Select(tg =>
                    new Precursor(DataSchema, new IdentityPath(mg.PeptideGroup, m.Peptide, tg.TransitionGroup)))));
        }

        public override ElementRef ElementRefPrototype => PrecursorRef.PROTOTYPE;
    }

    public class TransitionHandler : AbstractElementHandler<Databinding.Entities.Transition>
    {
        public TransitionHandler(SkylineDataSchema dataSchema) : base(dataSchema)
        {

        }

        public override IEnumerable<Databinding.Entities.Transition> ListAllElements()
        {
            return SrmDocument.MoleculeGroups.SelectMany(mg => mg.Molecules.SelectMany(m =>
                m.TransitionGroups.SelectMany(tg => tg.Transitions.Select(t =>
                    new Databinding.Entities.Transition(DataSchema,
                        new IdentityPath(mg.PeptideGroup, m.Peptide, tg.TransitionGroup, t.Transition))))));
        }

        public override ElementRef ElementRefPrototype => TransitionRef.PROTOTYPE;
    }

    public class ReplicateHandler : AbstractElementHandler<Replicate>
    {
        public ReplicateHandler(SkylineDataSchema dataSchema) : base(dataSchema)
        {

        }

        public override IEnumerable<Replicate> ListAllElements()
        {
            int replicateCount = SrmDocument.Settings.HasResults
                ? SrmDocument.Settings.MeasuredResults.Chromatograms.Count
                : 0;
            return Enumerable.Range(0, replicateCount)
                .Select(replicateIndex => new Replicate(DataSchema, replicateIndex));
        }

        public override ElementRef ElementRefPrototype
        {
            get { return ReplicateRef.PROTOTYPE; }
        }
    }

    public class ResultFileHandler : AbstractElementHandler<ResultFile>
    {
        public ResultFileHandler(SkylineDataSchema dataSchema) : base(dataSchema)
        {

        }

        public override ElementRef ElementRefPrototype
        {
            get { return ResultFileRef.PROTOTYPE; }
        }

        public override IEnumerable<ResultFile> ListAllElements()
        {
            int replicateCount = SrmDocument.Settings.HasResults
                ? SrmDocument.Settings.MeasuredResults.Chromatograms.Count
                : 0;
            for (int replicateIndex = 0; replicateIndex < replicateCount; replicateIndex++)
            {
                var replicate = new Replicate(DataSchema, replicateIndex);
                foreach (var chromFileInfo in replicate.ChromatogramSet.MSDataFileInfos)
                {
                    yield return new ResultFile(replicate, chromFileInfo.FileId, 0);
                }
            }
        }
    }

    public class MoleculeResultHandler : AbstractElementHandler<PeptideResult>
    {
        public MoleculeResultHandler(SkylineDataSchema dataSchema) : base(dataSchema)
        {

        }

        public override ElementRef ElementRefPrototype
        {
            get { return MoleculeResultRef.PROTOTYPE; }
        }

        public override IEnumerable<PeptideResult> ListAllElements()
        {
            return new MoleculeHandler(DataSchema).ListAllElements().SelectMany(peptide => peptide.Results.Values);
        }
    }

    public class PrecursorResultHandler : AbstractElementHandler<PrecursorResult>
    {
        public PrecursorResultHandler(SkylineDataSchema dataSchema) : base(dataSchema)
        {
        }

        public override ElementRef ElementRefPrototype
        {
            get { return PrecursorResultRef.PROTOTYPE; }
        }

        public override IEnumerable<PrecursorResult> ListAllElements()
        {
            return new PrecursorHandler(DataSchema).ListAllElements().SelectMany(precursor => precursor.Results.Values);
        }
    }

    public class TransitionResultHandler : AbstractElementHandler<TransitionResult>
    {
        public TransitionResultHandler(SkylineDataSchema dataSchema) : base(dataSchema)
        {
        }

        public override ElementRef ElementRefPrototype { get { return TransitionResultRef.PROTOTYPE; } }

        public override IEnumerable<TransitionResult> ListAllElements()
        {
            return new TransitionHandler(DataSchema).ListAllElements()
                .SelectMany(transition => transition.Results.Values);
        }
    }

    public class ImportablePropertyInfo
    {
        public ImportablePropertyInfo(PropertyDescriptor propertyDescriptor)
        {
            PropertyDescriptor = propertyDescriptor;
            var importableAttribute = (ImportableAttribute) propertyDescriptor.Attributes[typeof(ImportableAttribute)];
            if (null != importableAttribute.Formatter)
            {
                Formatter = (IPropertyFormatter) Activator.CreateInstance(importableAttribute.Formatter);
            }
            else
            {
                if (propertyDescriptor.PropertyType.IsValueType)
                {
                    DefaultValue = Activator.CreateInstance(propertyDescriptor.PropertyType);
                }
                else
                {
                    DefaultValue = null;
                }
            }
        }

        public PropertyDescriptor PropertyDescriptor { get; private set; }

        public IPropertyFormatter Formatter { get; private set; }

        public object DefaultValue { get; private set; }

        public string FormatPropertyValue(CultureInfo cultureInfo, object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (Formatter != null)
            {
                return Formatter.FormatValue(cultureInfo, value);
            }
            if (Equals(value, DefaultValue))
            {
                return string.Empty;
            }
            if (value is double d)
            {
                return d.ToString(Formats.RoundTrip, cultureInfo);
            }
            return (string)Convert.ChangeType(value, typeof(string), cultureInfo);
        }

        public object ParsePropertyValue(CultureInfo cultureInfo, string strValue)
        {
            if (Formatter != null)
            {
                return Formatter.ParseValue(cultureInfo, strValue);
            }
            if (string.IsNullOrEmpty(strValue))
            {
                return DefaultValue;
            }
            var targetType = PropertyDescriptor.PropertyType;
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                targetType = targetType.GetGenericArguments()[0];
            }
            return Convert.ChangeType(strValue, targetType, cultureInfo);
        }
    }
}
