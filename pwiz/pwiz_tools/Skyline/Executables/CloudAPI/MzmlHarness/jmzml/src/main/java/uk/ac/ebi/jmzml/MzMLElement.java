package uk.ac.ebi.jmzml;

import org.apache.log4j.Logger;
import uk.ac.ebi.jmzml.model.mzml.*;
import uk.ac.ebi.jmzml.model.mzml.utilities.MzMLElementConfig;
import uk.ac.ebi.jmzml.model.mzml.utilities.MzMLElementProperties;
import uk.ac.ebi.jmzml.xml.jaxb.resolver.*;

import javax.xml.bind.JAXB;
import java.net.URL;
import java.util.HashMap;
import java.util.Map;

/**
 * @author Florian
 */
@SuppressWarnings("unused")
public enum MzMLElement {
// ToDo: define and document dependencies between flags/attributes
    // ToDo (for example: a element can not be ID mapped if it is not indexed,
    // ToDo: or an element can not be cached if it is not ID mapped)?
    // ToDo: !! idMapped can only be set to true if the element has an id
    // ToDo: implement according consistency checks

    // ToDo: complete xpath for all elements
    // ToDo: update indexed flag for elements that should be indexed
    // ToDo: check for which elements an id map should be generated

    SetupMzMLElement(), // call the default constructor that will load that data from the configuration file
                        // BEFORE the following enum values are defined!

    //                          tag name                        indexed     xpath                                                                                           idMapped    class-name                          refResolving    reference-resolver
    //***********************************************************************************************************************************************************************************************************************************************************
//    AdditionalSearchParams   // no real model class
//    AnalyzerComponent(          "analyzer",                     true,       "/mzML/instrumentConfigurationList/instrumentConfiguration/componentList/analyzer",             false,      AnalyzerComponent.class,            false,          null),
    AnalyzerComponent(getCfg().get(AnalyzerComponent.class.getName()).getTagName(),
                      getCfg().get(AnalyzerComponent.class.getName()).isIndexed(),
                      getCfg().get(AnalyzerComponent.class.getName()).getXpath(),
                      getCfg().get(AnalyzerComponent.class.getName()).isIdMapped(),
                      getCfg().get(AnalyzerComponent.class.getName()).getClazz(),
                      getCfg().get(AnalyzerComponent.class.getName()).isAutoRefResolving(),
                      getCfg().get(AnalyzerComponent.class.getName()).getRefResolverClass()),

//    BinaryDataArray(            "binaryDataArray",              false,      null /*multiple locations*/,                                                                    false,      BinaryDataArray.class,              false,          BinaryDataArrayRefResolver.class),
    BinaryDataArray(getCfg().get(BinaryDataArray.class.getName()).getTagName(),
                    getCfg().get(BinaryDataArray.class.getName()).isIndexed(),
                    getCfg().get(BinaryDataArray.class.getName()).getXpath(),
                    getCfg().get(BinaryDataArray.class.getName()).isIdMapped(),
                    getCfg().get(BinaryDataArray.class.getName()).getClazz(),
                    getCfg().get(BinaryDataArray.class.getName()).isAutoRefResolving(),
                    getCfg().get(BinaryDataArray.class.getName()).getRefResolverClass()),

//    BinaryDataArrayList(        "binaryDataArrayList",          false,      null /*multiple locations*/,                                                                    false,      BinaryDataArrayList.class,          false,          null),
    BinaryDataArrayList(getCfg().get(BinaryDataArrayList.class.getName()).getTagName(),
                        getCfg().get(BinaryDataArrayList.class.getName()).isIndexed(),
                        getCfg().get(BinaryDataArrayList.class.getName()).getXpath(),
                        getCfg().get(BinaryDataArrayList.class.getName()).isIdMapped(),
                        getCfg().get(BinaryDataArrayList.class.getName()).getClazz(),
                        getCfg().get(BinaryDataArrayList.class.getName()).isAutoRefResolving(),
                        getCfg().get(BinaryDataArrayList.class.getName()).getRefResolverClass()),

//    Chromatogram(               "chromatogram",                 true,       "/mzML/run/chromatogramList/chromatogram",                                                      true,       Chromatogram.class,                 false,         ChromatogramRefResolver.class),
    Chromatogram(getCfg().get(Chromatogram.class.getName()).getTagName(),
                 getCfg().get(Chromatogram.class.getName()).isIndexed(),
                 getCfg().get(Chromatogram.class.getName()).getXpath(),
                 getCfg().get(Chromatogram.class.getName()).isIdMapped(),
                 getCfg().get(Chromatogram.class.getName()).getClazz(),
                 getCfg().get(Chromatogram.class.getName()).isAutoRefResolving(),
                 getCfg().get(Chromatogram.class.getName()).getRefResolverClass()),

//    ChromatogramList(           "chromatogramList",             true,       "/mzML/run/chromatogramList",                                                                   false,      ChromatogramList.class,             false,         ChromatogramListRefResolver.class),
    ChromatogramList(getCfg().get(ChromatogramList.class.getName()).getTagName(),
                     getCfg().get(ChromatogramList.class.getName()).isIndexed(),
                     getCfg().get(ChromatogramList.class.getName()).getXpath(),
                     getCfg().get(ChromatogramList.class.getName()).isIdMapped(),
                     getCfg().get(ChromatogramList.class.getName()).getClazz(),
                     getCfg().get(ChromatogramList.class.getName()).isAutoRefResolving(),
                     getCfg().get(ChromatogramList.class.getName()).getRefResolverClass()),

//    Component(                  "component",                    false,      null /*multiple locations*/,                                                                    false,      Component.class,                    false,          null),
    Component(getCfg().get(Component.class.getName()).getTagName(),
              getCfg().get(Component.class.getName()).isIndexed(),
              getCfg().get(Component.class.getName()).getXpath(),
              getCfg().get(Component.class.getName()).isIdMapped(),
              getCfg().get(Component.class.getName()).getClazz(),
              getCfg().get(Component.class.getName()).isAutoRefResolving(),
              getCfg().get(Component.class.getName()).getRefResolverClass()),

//    ComponentList(              "componentList",                true,       "/mzML/instrumentConfigurationList/instrumentConfiguration/componentList",                      false,      ComponentList.class,                false,          null),
    ComponentList(getCfg().get(ComponentList.class.getName()).getTagName(),
                  getCfg().get(ComponentList.class.getName()).isIndexed(),
                  getCfg().get(ComponentList.class.getName()).getXpath(),
                  getCfg().get(ComponentList.class.getName()).isIdMapped(),
                  getCfg().get(ComponentList.class.getName()).getClazz(),
                  getCfg().get(ComponentList.class.getName()).isAutoRefResolving(),
                  getCfg().get(ComponentList.class.getName()).getRefResolverClass()),

//    CV(                         "cv",                           true,       "/mzML/cvList/cv",                                                                              true,       CV.class,                           false,          null),
    CV(getCfg().get(CV.class.getName()).getTagName(),
       getCfg().get(CV.class.getName()).isIndexed(),
       getCfg().get(CV.class.getName()).getXpath(),
       getCfg().get(CV.class.getName()).isIdMapped(),
       getCfg().get(CV.class.getName()).getClazz(),
       getCfg().get(CV.class.getName()).isAutoRefResolving(),
       getCfg().get(CV.class.getName()).getRefResolverClass()),

//    CVList(                     "cvList",                       true,       "/mzML/cvList",                                                                                 false,      CVList.class,                       false,          null),
    CVList(getCfg().get(CVList.class.getName()).getTagName(),
           getCfg().get(CVList.class.getName()).isIndexed(),
           getCfg().get(CVList.class.getName()).getXpath(),
           getCfg().get(CVList.class.getName()).isIdMapped(),
           getCfg().get(CVList.class.getName()).getClazz(),
           getCfg().get(CVList.class.getName()).isAutoRefResolving(),
           getCfg().get(CVList.class.getName()).getRefResolverClass()),

//    CVParam(                    "cvParam",                      false,      null /*multiple locations*/,                                                                    false,      CVParam.class,                      false,         CVParamRefResolver.class),
    CVParam(getCfg().get(CVParam.class.getName()).getTagName(),
            getCfg().get(CVParam.class.getName()).isIndexed(),
            getCfg().get(CVParam.class.getName()).getXpath(),
            getCfg().get(CVParam.class.getName()).isIdMapped(),
            getCfg().get(CVParam.class.getName()).getClazz(),
            getCfg().get(CVParam.class.getName()).isAutoRefResolving(),
            getCfg().get(CVParam.class.getName()).getRefResolverClass()),

//    DataProcessing(             "dataProcessing",               true,       "/mzML/dataProcessingList/dataProcessing",                                                      true,       DataProcessing.class,               false,          null),
    DataProcessing(getCfg().get(DataProcessing.class.getName()).getTagName(),
                   getCfg().get(DataProcessing.class.getName()).isIndexed(),
                   getCfg().get(DataProcessing.class.getName()).getXpath(),
                   getCfg().get(DataProcessing.class.getName()).isIdMapped(),
                   getCfg().get(DataProcessing.class.getName()).getClazz(),
                   getCfg().get(DataProcessing.class.getName()).isAutoRefResolving(),
                   getCfg().get(DataProcessing.class.getName()).getRefResolverClass()),

//    DataProcessingList(         "dataProcessingList",           true,       "/mzML/dataProcessingList",                                                                     false,      DataProcessingList.class,           false,          null),
    DataProcessingList(getCfg().get(DataProcessingList.class.getName()).getTagName(),
                       getCfg().get(DataProcessingList.class.getName()).isIndexed(),
                       getCfg().get(DataProcessingList.class.getName()).getXpath(),
                       getCfg().get(DataProcessingList.class.getName()).isIdMapped(),
                       getCfg().get(DataProcessingList.class.getName()).getClazz(),
                       getCfg().get(DataProcessingList.class.getName()).isAutoRefResolving(),
                       getCfg().get(DataProcessingList.class.getName()).getRefResolverClass()),

//    DetectorComponent(          "detectorComponent",            true,       "/mzML/instrumentConfigurationList/instrumentConfiguration/componentList/detectorComponent",    false,      DetectorComponent.class,            false,          null),
    DetectorComponent(getCfg().get(DetectorComponent.class.getName()).getTagName(),
                      getCfg().get(DetectorComponent.class.getName()).isIndexed(),
                      getCfg().get(DetectorComponent.class.getName()).getXpath(),
                      getCfg().get(DetectorComponent.class.getName()).isIdMapped(),
                      getCfg().get(DetectorComponent.class.getName()).getClazz(),
                      getCfg().get(DetectorComponent.class.getName()).isAutoRefResolving(),
                      getCfg().get(DetectorComponent.class.getName()).getRefResolverClass()),

//    FileDescription(            "fileDescription",              true,       "/mzML/fileDescription",                                                                        false,      FileDescription.class,              false,          null),
    FileDescription(getCfg().get(FileDescription.class.getName()).getTagName(),
                    getCfg().get(FileDescription.class.getName()).isIndexed(),
                    getCfg().get(FileDescription.class.getName()).getXpath(),
                    getCfg().get(FileDescription.class.getName()).isIdMapped(),
                    getCfg().get(FileDescription.class.getName()).getClazz(),
                    getCfg().get(FileDescription.class.getName()).isAutoRefResolving(),
                    getCfg().get(FileDescription.class.getName()).getRefResolverClass()),

//    Index(                      "index",                        true,       "/indexedmzML/indexList/index",                                                                 false,      Index.class,                        false,          null),
    Index(getCfg().get(Index.class.getName()).getTagName(),
          getCfg().get(Index.class.getName()).isIndexed(),
          getCfg().get(Index.class.getName()).getXpath(),
          getCfg().get(Index.class.getName()).isIdMapped(),
          getCfg().get(Index.class.getName()).getClazz(),
          getCfg().get(Index.class.getName()).isAutoRefResolving(),
          getCfg().get(Index.class.getName()).getRefResolverClass()),

//    IndexedmzML(                "indexedmzML",                  true,       "/indexedmzML",                                                                                 false,      IndexedmzML.class,                  false,          null),
    IndexedmzML(getCfg().get(IndexedmzML.class.getName()).getTagName(),
                getCfg().get(IndexedmzML.class.getName()).isIndexed(),
                getCfg().get(IndexedmzML.class.getName()).getXpath(),
                getCfg().get(IndexedmzML.class.getName()).isIdMapped(),
                getCfg().get(IndexedmzML.class.getName()).getClazz(),
                getCfg().get(IndexedmzML.class.getName()).isAutoRefResolving(),
                getCfg().get(IndexedmzML.class.getName()).getRefResolverClass()),

//    IndexList(                  "indexList",                    true,       "/indexedmzML/indexList/",                                                                      false,      IndexList.class,                    false,          null),
    IndexList(getCfg().get(IndexList.class.getName()).getTagName(),
              getCfg().get(IndexList.class.getName()).isIndexed(),
              getCfg().get(IndexList.class.getName()).getXpath(),
              getCfg().get(IndexList.class.getName()).isIdMapped(),
              getCfg().get(IndexList.class.getName()).getClazz(),
              getCfg().get(IndexList.class.getName()).isAutoRefResolving(),
              getCfg().get(IndexList.class.getName()).getRefResolverClass()),

//    InstrumentConfiguration(    "instrumentConfiguration",      true,       "/mzML/instrumentConfigurationList/instrumentConfiguration",                                    true,       InstrumentConfiguration.class,      false,         InstrumentConfigurationRefResolver.class),
    InstrumentConfiguration(getCfg().get(InstrumentConfiguration.class.getName()).getTagName(),
                            getCfg().get(InstrumentConfiguration.class.getName()).isIndexed(),
                            getCfg().get(InstrumentConfiguration.class.getName()).getXpath(),
                            getCfg().get(InstrumentConfiguration.class.getName()).isIdMapped(),
                            getCfg().get(InstrumentConfiguration.class.getName()).getClazz(),
                            getCfg().get(InstrumentConfiguration.class.getName()).isAutoRefResolving(),
                            getCfg().get(InstrumentConfiguration.class.getName()).getRefResolverClass()),

//    InstrumentConfigurationList("instrumentConfigurationList",  true,       "/mzML/instrumentConfigurationList",                                                            false,      InstrumentConfigurationList.class,  false,          null),
    InstrumentConfigurationList(getCfg().get(InstrumentConfigurationList.class.getName()).getTagName(),
                                getCfg().get(InstrumentConfigurationList.class.getName()).isIndexed(),
                                getCfg().get(InstrumentConfigurationList.class.getName()).getXpath(),
                                getCfg().get(InstrumentConfigurationList.class.getName()).isIdMapped(),
                                getCfg().get(InstrumentConfigurationList.class.getName()).getClazz(),
                                getCfg().get(InstrumentConfigurationList.class.getName()).isAutoRefResolving(),
                                getCfg().get(InstrumentConfigurationList.class.getName()).getRefResolverClass()),

//    MzML(                       "mzML",                         true,       "/mzML",                                                                                        false,      MzML.class,                         false,          null),
    MzML(getCfg().get(MzML.class.getName()).getTagName(),
         getCfg().get(MzML.class.getName()).isIndexed(),
         getCfg().get(MzML.class.getName()).getXpath(),
         getCfg().get(MzML.class.getName()).isIdMapped(),
         getCfg().get(MzML.class.getName()).getClazz(),
         getCfg().get(MzML.class.getName()).isAutoRefResolving(),
         getCfg().get(MzML.class.getName()).getRefResolverClass()),

//    Offset(                     "offset",                       true,       "/indexedmzML/indexList/index/offset",                                                          false,      Offset.class,                       false,          null),
    Offset(getCfg().get(Offset.class.getName()).getTagName(),
           getCfg().get(Offset.class.getName()).isIndexed(),
           getCfg().get(Offset.class.getName()).getXpath(),
           getCfg().get(Offset.class.getName()).isIdMapped(),
           getCfg().get(Offset.class.getName()).getClazz(),
           getCfg().get(Offset.class.getName()).isAutoRefResolving(),
           getCfg().get(Offset.class.getName()).getRefResolverClass()),

//    ParamGroup(                 "paramGroup",                   false,      null /*multiple locations*/,                                                                    false,      ParamGroup.class,                   false,          null),
    ParamGroup(getCfg().get(ParamGroup.class.getName()).getTagName(),
               getCfg().get(ParamGroup.class.getName()).isIndexed(),
               getCfg().get(ParamGroup.class.getName()).getXpath(),
               getCfg().get(ParamGroup.class.getName()).isIdMapped(),
               getCfg().get(ParamGroup.class.getName()).getClazz(),
               getCfg().get(ParamGroup.class.getName()).isAutoRefResolving(),
               getCfg().get(ParamGroup.class.getName()).getRefResolverClass()),

//    Precursor(                  "precursor",                    true,       "/mzML/run/spectrumList/spectrum/precursorList/precursor",                                      false,      Precursor.class,                    false,         PrecursorRefResolver.class),
    Precursor(getCfg().get(Precursor.class.getName()).getTagName(),
              getCfg().get(Precursor.class.getName()).isIndexed(),
              getCfg().get(Precursor.class.getName()).getXpath(),
              getCfg().get(Precursor.class.getName()).isIdMapped(),
              getCfg().get(Precursor.class.getName()).getClazz(),
              getCfg().get(Precursor.class.getName()).isAutoRefResolving(),
              getCfg().get(Precursor.class.getName()).getRefResolverClass()),

//    PrecursorList(              "precursorList",                true,       "/mzML/run/spectrumList/spectrum/precursorList",                                                false,      PrecursorList.class,                false,          null),
    PrecursorList(getCfg().get(PrecursorList.class.getName()).getTagName(),
                  getCfg().get(PrecursorList.class.getName()).isIndexed(),
                  getCfg().get(PrecursorList.class.getName()).getXpath(),
                  getCfg().get(PrecursorList.class.getName()).isIdMapped(),
                  getCfg().get(PrecursorList.class.getName()).getClazz(),
                  getCfg().get(PrecursorList.class.getName()).isAutoRefResolving(),
                  getCfg().get(PrecursorList.class.getName()).getRefResolverClass()),

//    ProcessingMethod(           "processingMethod",             true,       "/mzML/dataProcessingList/dataProcessing/processingMethod",                                     false,      ProcessingMethod.class,             false,         ProcessingMethodRefResolver.class),
    ProcessingMethod(getCfg().get(ProcessingMethod.class.getName()).getTagName(),
                     getCfg().get(ProcessingMethod.class.getName()).isIndexed(),
                     getCfg().get(ProcessingMethod.class.getName()).getXpath(),
                     getCfg().get(ProcessingMethod.class.getName()).isIdMapped(),
                     getCfg().get(ProcessingMethod.class.getName()).getClazz(),
                     getCfg().get(ProcessingMethod.class.getName()).isAutoRefResolving(),
                     getCfg().get(ProcessingMethod.class.getName()).getRefResolverClass()),

//    Product(                    "product",                      true,       "/mzML/run/spectrumList/spectrum/productList/product",                                          false,      Product.class,                      false,          null),
    Product(getCfg().get(Product.class.getName()).getTagName(),
            getCfg().get(Product.class.getName()).isIndexed(),
            getCfg().get(Product.class.getName()).getXpath(),
            getCfg().get(Product.class.getName()).isIdMapped(),
            getCfg().get(Product.class.getName()).getClazz(),
            getCfg().get(Product.class.getName()).isAutoRefResolving(),
            getCfg().get(Product.class.getName()).getRefResolverClass()),

//    ProductList(                "productList",                  true,       "/mzML/run/spectrumList/spectrum/productList",                                                  false,      ProductList.class,                  false,          null),
    ProductList(getCfg().get(ProductList.class.getName()).getTagName(),
                getCfg().get(ProductList.class.getName()).isIndexed(),
                getCfg().get(ProductList.class.getName()).getXpath(),
                getCfg().get(ProductList.class.getName()).isIdMapped(),
                getCfg().get(ProductList.class.getName()).getClazz(),
                getCfg().get(ProductList.class.getName()).isAutoRefResolving(),
                getCfg().get(ProductList.class.getName()).getRefResolverClass()),

//    ReferenceableParamGroup(    "referenceableParamGroup",      true,       "/mzML/referenceableParamGroupList/referenceableParamGroup",                                    true,       ReferenceableParamGroup.class,      false,          null),
    ReferenceableParamGroup(getCfg().get(ReferenceableParamGroup.class.getName()).getTagName(),
                            getCfg().get(ReferenceableParamGroup.class.getName()).isIndexed(),
                            getCfg().get(ReferenceableParamGroup.class.getName()).getXpath(),
                            getCfg().get(ReferenceableParamGroup.class.getName()).isIdMapped(),
                            getCfg().get(ReferenceableParamGroup.class.getName()).getClazz(),
                            getCfg().get(ReferenceableParamGroup.class.getName()).isAutoRefResolving(),
                            getCfg().get(ReferenceableParamGroup.class.getName()).getRefResolverClass()),

//    ReferenceableParamGroupList("referenceableParamGroupList",  true,       "/mzML/referenceableParamGroupList",                                                            false,      ReferenceableParamGroupList.class,  false,          null),
    ReferenceableParamGroupList(getCfg().get(ReferenceableParamGroupList.class.getName()).getTagName(),
                                getCfg().get(ReferenceableParamGroupList.class.getName()).isIndexed(),
                                getCfg().get(ReferenceableParamGroupList.class.getName()).getXpath(),
                                getCfg().get(ReferenceableParamGroupList.class.getName()).isIdMapped(),
                                getCfg().get(ReferenceableParamGroupList.class.getName()).getClazz(),
                                getCfg().get(ReferenceableParamGroupList.class.getName()).isAutoRefResolving(),
                                getCfg().get(ReferenceableParamGroupList.class.getName()).getRefResolverClass()),

//    ReferenceableParamGroupRef( "referenceableParamGroupRef",   false,      null /*multiple locations*/,                                                                    false,      ReferenceableParamGroupRef.class,   true,         ReferenceableParamGroupRefResolver.class),
    ReferenceableParamGroupRef(getCfg().get(ReferenceableParamGroupRef.class.getName()).getTagName(),
                               getCfg().get(ReferenceableParamGroupRef.class.getName()).isIndexed(),
                               getCfg().get(ReferenceableParamGroupRef.class.getName()).getXpath(),
                               getCfg().get(ReferenceableParamGroupRef.class.getName()).isIdMapped(),
                               getCfg().get(ReferenceableParamGroupRef.class.getName()).getClazz(),
                               getCfg().get(ReferenceableParamGroupRef.class.getName()).isAutoRefResolving(),
                               getCfg().get(ReferenceableParamGroupRef.class.getName()).getRefResolverClass()),

//    Run(                        "run",                          true,       "/mzML/run",                                                                                    true,       Run.class,                          false,         RunRefResolver.class),
    Run(getCfg().get(Run.class.getName()).getTagName(),
        getCfg().get(Run.class.getName()).isIndexed(),
        getCfg().get(Run.class.getName()).getXpath(),
        getCfg().get(Run.class.getName()).isIdMapped(),
        getCfg().get(Run.class.getName()).getClazz(),
        getCfg().get(Run.class.getName()).isAutoRefResolving(),
        getCfg().get(Run.class.getName()).getRefResolverClass()),

//    Sample(                     "sample",                       true,       "/mzML/sampleList/sample",                                                                      true,       Sample.class,                       false,          null),
    Sample(getCfg().get(Sample.class.getName()).getTagName(),
           getCfg().get(Sample.class.getName()).isIndexed(),
           getCfg().get(Sample.class.getName()).getXpath(),
           getCfg().get(Sample.class.getName()).isIdMapped(),
           getCfg().get(Sample.class.getName()).getClazz(),
           getCfg().get(Sample.class.getName()).isAutoRefResolving(),
           getCfg().get(Sample.class.getName()).getRefResolverClass()),

//    SampleList(                 "sampleList",                   true,       "/mzML/sampleList",                                                                             false,      SampleList.class,                   false,          null),
    SampleList(getCfg().get(SampleList.class.getName()).getTagName(),
               getCfg().get(SampleList.class.getName()).isIndexed(),
               getCfg().get(SampleList.class.getName()).getXpath(),
               getCfg().get(SampleList.class.getName()).isIdMapped(),
               getCfg().get(SampleList.class.getName()).getClazz(),
               getCfg().get(SampleList.class.getName()).isAutoRefResolving(),
               getCfg().get(SampleList.class.getName()).getRefResolverClass()),

//    Scan(                       "scan",                         true,       "/mzML/run/spectrumList/spectrum/scanList/scan",                                                false,      Scan.class,                         false,         ScanRefResolver.class),
    Scan(getCfg().get(Scan.class.getName()).getTagName(),
         getCfg().get(Scan.class.getName()).isIndexed(),
         getCfg().get(Scan.class.getName()).getXpath(),
         getCfg().get(Scan.class.getName()).isIdMapped(),
         getCfg().get(Scan.class.getName()).getClazz(),
         getCfg().get(Scan.class.getName()).isAutoRefResolving(),
         getCfg().get(Scan.class.getName()).getRefResolverClass()),

//    ScanList(                   "scanList",                     true,       "/mzML/run/spectrumList/spectrum/scanList",                                                     false,      ScanList.class,                     false,          null),
    ScanList(getCfg().get(ScanList.class.getName()).getTagName(),
             getCfg().get(ScanList.class.getName()).isIndexed(),
             getCfg().get(ScanList.class.getName()).getXpath(),
             getCfg().get(ScanList.class.getName()).isIdMapped(),
             getCfg().get(ScanList.class.getName()).getClazz(),
             getCfg().get(ScanList.class.getName()).isAutoRefResolving(),
             getCfg().get(ScanList.class.getName()).getRefResolverClass()),

//    ScanSettings(               "scanSettings",                 true,       "/mzML/scanSettingsList/scanSettings",                                                          true,       ScanSettings.class,                 false,          null),
    ScanSettings(getCfg().get(ScanSettings.class.getName()).getTagName(),
                 getCfg().get(ScanSettings.class.getName()).isIndexed(),
                 getCfg().get(ScanSettings.class.getName()).getXpath(),
                 getCfg().get(ScanSettings.class.getName()).isIdMapped(),
                 getCfg().get(ScanSettings.class.getName()).getClazz(),
                 getCfg().get(ScanSettings.class.getName()).isAutoRefResolving(),
                 getCfg().get(ScanSettings.class.getName()).getRefResolverClass()),

//    ScanSettingsList(           "scanSettingsList",             true,       "/mzML/scanSettingsList",                                                                       false,      ScanSettingsList.class,             false,          null),
    ScanSettingsList(getCfg().get(ScanSettingsList.class.getName()).getTagName(),
                     getCfg().get(ScanSettingsList.class.getName()).isIndexed(),
                     getCfg().get(ScanSettingsList.class.getName()).getXpath(),
                     getCfg().get(ScanSettingsList.class.getName()).isIdMapped(),
                     getCfg().get(ScanSettingsList.class.getName()).getClazz(),
                     getCfg().get(ScanSettingsList.class.getName()).isAutoRefResolving(),
                     getCfg().get(ScanSettingsList.class.getName()).getRefResolverClass()),

//    ScanWindowList(             "scanWindowList",               true,       "/mzML/run/spectrumList/spectrum/scanList/scan/scanWindowList",                                 false,      ScanWindowList.class,               false,          null),
    ScanWindowList(getCfg().get(ScanWindowList.class.getName()).getTagName(),
                   getCfg().get(ScanWindowList.class.getName()).isIndexed(),
                   getCfg().get(ScanWindowList.class.getName()).getXpath(),
                   getCfg().get(ScanWindowList.class.getName()).isIdMapped(),
                   getCfg().get(ScanWindowList.class.getName()).getClazz(),
                   getCfg().get(ScanWindowList.class.getName()).isAutoRefResolving(),
                   getCfg().get(ScanWindowList.class.getName()).getRefResolverClass()),

//    SelectedIonList(            "selectedIonList",              true,       "/mzML/run/spectrumList/spectrum/precursorList/precursor/selectedIonList",                      false,      SelectedIonList.class,              false,          null),
    SelectedIonList(getCfg().get(SelectedIonList.class.getName()).getTagName(),
                    getCfg().get(SelectedIonList.class.getName()).isIndexed(),
                    getCfg().get(SelectedIonList.class.getName()).getXpath(),
                    getCfg().get(SelectedIonList.class.getName()).isIdMapped(),
                    getCfg().get(SelectedIonList.class.getName()).getClazz(),
                    getCfg().get(SelectedIonList.class.getName()).isAutoRefResolving(),
                    getCfg().get(SelectedIonList.class.getName()).getRefResolverClass()),

//    Software(                   "software",                     true,       "/mzML/softwareList/software",                                                                  true,       Software.class,                     false,          null),
    Software(getCfg().get(Software.class.getName()).getTagName(),
             getCfg().get(Software.class.getName()).isIndexed(),
             getCfg().get(Software.class.getName()).getXpath(),
             getCfg().get(Software.class.getName()).isIdMapped(),
             getCfg().get(Software.class.getName()).getClazz(),
             getCfg().get(Software.class.getName()).isAutoRefResolving(),
             getCfg().get(Software.class.getName()).getRefResolverClass()),

//    SoftwareList(               "softwareList",                 true,       "/mzML/softwareList",                                                                           false,      SoftwareList.class,                 false,          null),
    SoftwareList(getCfg().get(SoftwareList.class.getName()).getTagName(),
                 getCfg().get(SoftwareList.class.getName()).isIndexed(),
                 getCfg().get(SoftwareList.class.getName()).getXpath(),
                 getCfg().get(SoftwareList.class.getName()).isIdMapped(),
                 getCfg().get(SoftwareList.class.getName()).getClazz(),
                 getCfg().get(SoftwareList.class.getName()).isAutoRefResolving(),
                 getCfg().get(SoftwareList.class.getName()).getRefResolverClass()),

//    SoftwareRef(                "softwareRef",                  false,      null /*multiple locations*/,                                                                    false,      SoftwareRef.class,                  false,         SoftwareRefResolver.class),
    SoftwareRef(getCfg().get(SoftwareRef.class.getName()).getTagName(),
                getCfg().get(SoftwareRef.class.getName()).isIndexed(),
                getCfg().get(SoftwareRef.class.getName()).getXpath(),
                getCfg().get(SoftwareRef.class.getName()).isIdMapped(),
                getCfg().get(SoftwareRef.class.getName()).getClazz(),
                getCfg().get(SoftwareRef.class.getName()).isAutoRefResolving(),
                getCfg().get(SoftwareRef.class.getName()).getRefResolverClass()),

//    SourceComponent(            "sourceComponent",              true,       "/mzML/instrumentConfigurationList/instrumentConfiguration/componentList/sourceComponent",      false,      SourceComponent.class,              false,          null),
    SourceComponent(getCfg().get(SourceComponent.class.getName()).getTagName(),
                    getCfg().get(SourceComponent.class.getName()).isIndexed(),
                    getCfg().get(SourceComponent.class.getName()).getXpath(),
                    getCfg().get(SourceComponent.class.getName()).isIdMapped(),
                    getCfg().get(SourceComponent.class.getName()).getClazz(),
                    getCfg().get(SourceComponent.class.getName()).isAutoRefResolving(),
                    getCfg().get(SourceComponent.class.getName()).getRefResolverClass()),

//    SourceFile(                 "sourceFile",                   true,       "/mzML/fileDescription/sourceFileList/sourceFile",                                              true,       SourceFile.class,                   false,          null),
    SourceFile(getCfg().get(SourceFile.class.getName()).getTagName(),
               getCfg().get(SourceFile.class.getName()).isIndexed(),
               getCfg().get(SourceFile.class.getName()).getXpath(),
               getCfg().get(SourceFile.class.getName()).isIdMapped(),
               getCfg().get(SourceFile.class.getName()).getClazz(),
               getCfg().get(SourceFile.class.getName()).isAutoRefResolving(),
               getCfg().get(SourceFile.class.getName()).getRefResolverClass()),

//    SourceFileList(             "sourceFileList",               true,       "/mzML/fileDescription/sourceFileList",                                                         false,      SourceFileList.class,               false,          null),
    SourceFileList(getCfg().get(SourceFileList.class.getName()).getTagName(),
                   getCfg().get(SourceFileList.class.getName()).isIndexed(),
                   getCfg().get(SourceFileList.class.getName()).getXpath(),
                   getCfg().get(SourceFileList.class.getName()).isIdMapped(),
                   getCfg().get(SourceFileList.class.getName()).getClazz(),
                   getCfg().get(SourceFileList.class.getName()).isAutoRefResolving(),
                   getCfg().get(SourceFileList.class.getName()).getRefResolverClass()),

//    SourceFileRefList(          "sourceFileRefList",            false,      null /*multiple locations*/,                                                                    false,      SourceFileRefList.class,            false,          null),
    SourceFileRefList(getCfg().get(SourceFileRefList.class.getName()).getTagName(),
                      getCfg().get(SourceFileRefList.class.getName()).isIndexed(),
                      getCfg().get(SourceFileRefList.class.getName()).getXpath(),
                      getCfg().get(SourceFileRefList.class.getName()).isIdMapped(),
                      getCfg().get(SourceFileRefList.class.getName()).getClazz(),
                      getCfg().get(SourceFileRefList.class.getName()).isAutoRefResolving(),
                      getCfg().get(SourceFileRefList.class.getName()).getRefResolverClass()),

//    SourceFileRef(              "sourceFileRef",                false,      null /*multiple locations*/,                                                                    false,      SourceFileRef.class,                false,         SourceFileRefResolver.class),
    SourceFileRef(getCfg().get(SourceFileRef.class.getName()).getTagName(),
                  getCfg().get(SourceFileRef.class.getName()).isIndexed(),
                  getCfg().get(SourceFileRef.class.getName()).getXpath(),
                  getCfg().get(SourceFileRef.class.getName()).isIdMapped(),
                  getCfg().get(SourceFileRef.class.getName()).getClazz(),
                  getCfg().get(SourceFileRef.class.getName()).isAutoRefResolving(),
                  getCfg().get(SourceFileRef.class.getName()).getRefResolverClass()),

//    Spectrum(                   "spectrum",                     true,       "/mzML/run/spectrumList/spectrum",                                                              true,       Spectrum.class,                     false,         SpectrumRefResolver.class),
    Spectrum(getCfg().get(Spectrum.class.getName()).getTagName(),
             getCfg().get(Spectrum.class.getName()).isIndexed(),
             getCfg().get(Spectrum.class.getName()).getXpath(),
             getCfg().get(Spectrum.class.getName()).isIdMapped(),
             getCfg().get(Spectrum.class.getName()).getClazz(),
             getCfg().get(Spectrum.class.getName()).isAutoRefResolving(),
             getCfg().get(Spectrum.class.getName()).getRefResolverClass()),

//    SpectrumList(               "spectrumList",                 true,       "/mzML/run/spectrumList",                                                                       false,      SpectrumList.class,                 false,         SpectrumListRefResolver.class),
    SpectrumList(getCfg().get(SpectrumList.class.getName()).getTagName(),
                 getCfg().get(SpectrumList.class.getName()).isIndexed(),
                 getCfg().get(SpectrumList.class.getName()).getXpath(),
                 getCfg().get(SpectrumList.class.getName()).isIdMapped(),
                 getCfg().get(SpectrumList.class.getName()).getClazz(),
                 getCfg().get(SpectrumList.class.getName()).isAutoRefResolving(),
                 getCfg().get(SpectrumList.class.getName()).getRefResolverClass()),

//    TargetList(                 "targetList",                   true,       "/mzML/scanSettingsList/scanSettings/targetList",                                               false,      TargetList.class,                   false,          null),
    TargetList(getCfg().get(TargetList.class.getName()).getTagName(),
               getCfg().get(TargetList.class.getName()).isIndexed(),
               getCfg().get(TargetList.class.getName()).getXpath(),
               getCfg().get(TargetList.class.getName()).isIdMapped(),
               getCfg().get(TargetList.class.getName()).getClazz(),
               getCfg().get(TargetList.class.getName()).isAutoRefResolving(),
               getCfg().get(TargetList.class.getName()).getRefResolverClass()),

//    UserParam(                  "userParam",                    false,      null /*multiple locations*/,                                                                    false,      UserParam.class,                    false,         UserParamRefResolver.class);
    UserParam(getCfg().get(UserParam.class.getName()).getTagName(),
              getCfg().get(UserParam.class.getName()).isIndexed(),
              getCfg().get(UserParam.class.getName()).getXpath(),
              getCfg().get(UserParam.class.getName()).isIdMapped(),
              getCfg().get(UserParam.class.getName()).getClazz(),
              getCfg().get(UserParam.class.getName()).isAutoRefResolving(),
              getCfg().get(UserParam.class.getName()).getRefResolverClass());



    private String tagName;
    private boolean indexed;
    private String xpath;
    private boolean idMapped;
    private Class clazz;
    private boolean autoRefResolving;
    private Class refResolverClass;



    /**
     * This should be called first in order to retrieve configuration from a file and populate cfgMap.
     */
    private <T extends MzMLObject> MzMLElement() {
        loadProperties();
    }

    private <T extends MzMLObject> MzMLElement(String tagName,
                                               boolean indexed,
                                               String xpath,
                                               boolean idMapped,
                                               Class<T> clazz,
                                               boolean autoRefResolving,
                                               Class refResolverClass) {
        this.tagName = tagName;
        this.indexed = indexed;
        this.xpath = xpath;
        this.idMapped = idMapped;
        this.clazz = clazz;
        this.autoRefResolving = autoRefResolving;
        this.refResolverClass = refResolverClass;
    }


    private static Map<String, MzMLElementConfig> cfgMap;

    private static Map<String, MzMLElementConfig> getCfg() {
        if (cfgMap == null) {
            cfgMap = new HashMap<String, MzMLElementConfig>();
        }
        return cfgMap;
    }

    /**
     * Read the configuration info from the properties file. Note: this simply loads the information into a hashmap.
     * Actual setting of values is done through the constructors.
     */
    public static void loadProperties() {

        Logger logger = Logger.getLogger(MzMLElement.class);

        //check to see if we have a project-specific configuration file
        URL xmlFileURL = MzMLElement.class.getClassLoader().getResource("MzMLElement.cfg.xml");
        //if not, use default config
        if (xmlFileURL == null) {
            xmlFileURL = MzMLElement.class.getClassLoader().getResource("defaultMzMLElement.cfg.xml");
        }
        logger.warn("MzIdentML Configuration file: " + xmlFileURL.toString());

        MzMLElementProperties props = JAXB.unmarshal(xmlFileURL, MzMLElementProperties.class);
        Map<String, MzMLElementConfig> localCfg = getCfg();
        for (MzMLElementConfig cfg : props.getConfigurations()) {
            Class clazz = cfg.getClazz();
            if (clazz != null) {
                localCfg.put(clazz.getName(), cfg);
            }
        }
    }


    public String getTagName() {
        return tagName;
    }

    public boolean isIndexed() {
        return indexed;
    }

    public String getXpath() {
        return xpath;
    }

    public boolean isIdMapped() {
        return idMapped;
    }

    @SuppressWarnings("unchecked")
    public <T extends MzMLObject> Class<T> getClazz() {
        return clazz;
    }

    public static MzMLElement getType(Class clazz) {
        for (MzMLElement type : MzMLElement.values()) {
            if (type.getClazz() == clazz) {
                return type;
            }
        }
        return null;
    }

    public static MzMLElement getType(String xpath) {
        for (MzMLElement type : MzMLElement.values()) {
            if (type.getXpath() != null && type.getXpath().equals(xpath)) {
                return type;
            }
        }
        return null;
    }

    public boolean isAutoRefResolving() {
        return autoRefResolving;
    }

    @SuppressWarnings("unchecked")
    public <R extends AbstractReferenceResolver> Class<R> getRefResolverClass() {
        return refResolverClass;
    }

    @Override
    public String toString() {
        return "MzMLElement{" +
                ", xpath='" + xpath + '\'' +
                ", clazz=" + clazz +
                '}';
    }
}
