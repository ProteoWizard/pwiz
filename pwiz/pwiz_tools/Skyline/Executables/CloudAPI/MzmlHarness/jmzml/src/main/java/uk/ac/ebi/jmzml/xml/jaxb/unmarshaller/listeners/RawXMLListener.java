/*
 * Date: 22/7/2008
 * Author: rcote
 * File: uk.ac.ebi.jmzml.xml.jaxb.unmarshaller.listeners.RawXMLListener
 *
 * jmzml is Copyright 2008 The European Bioinformatics Institute
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 *
 *
 */

package uk.ac.ebi.jmzml.xml.jaxb.unmarshaller.listeners;

import org.apache.log4j.Logger;
import uk.ac.ebi.jmzml.MzMLElement;
import uk.ac.ebi.jmzml.model.mzml.*;
import uk.ac.ebi.jmzml.model.mzml.params.*;
import uk.ac.ebi.jmzml.model.mzml.utilities.ParamGroupUpdater;
import uk.ac.ebi.jmzml.xml.io.MzMLObjectCache;
import uk.ac.ebi.jmzml.xml.jaxb.resolver.AbstractReferenceResolver;
import uk.ac.ebi.jmzml.xml.xxindex.MzMLIndexer;

import javax.xml.bind.Unmarshaller;
import java.lang.reflect.Constructor;
import java.util.ArrayList;
import java.util.List;

public class RawXMLListener extends Unmarshaller.Listener {


    private static final Logger log = Logger.getLogger(RawXMLListener.class);
    private final MzMLIndexer index;
    private final MzMLObjectCache cache;

    public RawXMLListener(MzMLIndexer index, MzMLObjectCache cache) {
        this.index = index;
        this.cache = cache;
    }

    @Override
    public void afterUnmarshal(Object target, Object parent) {

        log.debug("Handling " + target.getClass() + " in afterUnmarshal.");
        // retrieve the enum type for this class (for the meta data about this class/element)
        MzMLElement ele = MzMLElement.getType(target.getClass());

        // now perform the automatic reference resolving, if configured to do so
        referenceResolving(target, parent, ele);

        // Here we handle all the referenced ParamGroups
        // Whenever we encounter a ParamGroup, there could be referenced params.
        // Since some params could be used more than in one location, they are not
        // duplicated in each XML snippet that needs them, but localized in a central
        // part of the XML (namely in referenceableParamGroup) and only referenced. 
        // Here we have to fetch those referenced params from the central location and add them to the local element.

        try {

            ///// ///// ///// ///// ///// ///// ///// ///// ///// /////
            // Update all ParamGroup Subclasses

            if (target instanceof BinaryDataArray) {
                ParamGroupUpdater.updateParamGroupSubclasses((BinaryDataArray) target, BinaryDataArrayCVParam.class, BinaryDataArrayUserParam.class);
            }

            if (target instanceof Chromatogram) {
                ParamGroupUpdater.updateParamGroupSubclasses((Chromatogram) target, ChromatogramCVParam.class, ChromatogramUserParam.class);
            }

            if (target instanceof Component) {
                ParamGroupUpdater.updateParamGroupSubclasses((Component) target, ComponentCVParam.class, ComponentUserParam.class);
            }

            if (target instanceof InstrumentConfiguration) {
                ParamGroupUpdater.updateParamGroupSubclasses((InstrumentConfiguration) target, InstrumentConfigurationCVParam.class, InstrumentConfigurationUserParam.class);
            }

            if (target instanceof ProcessingMethod) {
                ParamGroupUpdater.updateParamGroupSubclasses((ProcessingMethod) target, ProcessingMethodCVParam.class, ProcessingMethodUserParam.class);
            }

            if (target instanceof Run) {
                ParamGroupUpdater.updateParamGroupSubclasses((Run) target, RunCVParam.class, RunUserParam.class);
            }

            if (target instanceof Sample) {
                ParamGroupUpdater.updateParamGroupSubclasses((Sample) target, SampleCVParam.class, SampleUserParam.class);
            }

            if (target instanceof Scan) {
                ParamGroupUpdater.updateParamGroupSubclasses((Scan) target, ScanCVParam.class, ScanUserParam.class);
            }

            if (target instanceof ScanList) {
                ParamGroupUpdater.updateParamGroupSubclasses((ScanList) target, ScanListCVParam.class, ScanListUserParam.class);
            }

            if (target instanceof ScanSettings) {
                ParamGroupUpdater.updateParamGroupSubclasses((ScanSettings) target, ScanSettingsCVParam.class, ScanSettingsUserParam.class);
            }

            if (target instanceof SourceFile) {
                ParamGroupUpdater.updateParamGroupSubclasses((SourceFile) target, SourceFileCVParam.class, SourceFileUserParam.class);
            }

            if (target instanceof Spectrum) {
                ParamGroupUpdater.updateParamGroupSubclasses((Spectrum) target, SpectrumCVParam.class, SpectrumUserParam.class);
            }

            if (target instanceof Software){
                ParamGroupUpdater.updateParamGroupSubclasses((Software) target, SoftwareCVParam.class, SoftwareUserParam.class);
            }

            ///// ///// ///// ///// ///// ///// ///// ///// ///// /////
            // Update all classes with ParamGroup members

            if (target instanceof FileDescription) {
                FileDescription tmp = (FileDescription) target;
                // update fileContent (ParamGroup)
                ParamGroupUpdater.updateParamGroupSubclasses(tmp.getFileContent(), FileDescriptionCVParam.class, FileDescriptionUserParam.class);
                // update contact (ParamGroup list)
                if (tmp.getContact() != null && !tmp.getContact().isEmpty()) {
                    List<ParamGroup> tmpContact = new ArrayList<ParamGroup>();
                    for (ParamGroup aContact : tmp.getContact()) {
                        ParamGroupUpdater.updateParamGroupSubclasses(aContact, ContactCVParam.class, ContactUserParam.class);
                        tmpContact.add(aContact);
                    }
                    tmp.getContact().clear();
                    tmp.getContact().addAll(tmpContact);
                }
            }

            if (target instanceof Precursor) {
                Precursor tmp = (Precursor) target;
                //update activation (ParamGroup)
                ParamGroupUpdater.updateParamGroupSubclasses(tmp.getActivation(), ActivationCVParam.class, ActivationUserParam.class);
                //update isolationWindow (ParamGroup)
                ParamGroupUpdater.updateParamGroupSubclasses(tmp.getIsolationWindow(), IsolationWindowCVParam.class, IsolationWindowUserParam.class);
            }

            if (target instanceof Product) {
                Product tmp = (Product) target;
                //update isolationWindow (ParamGroup)
                ParamGroupUpdater.updateParamGroupSubclasses(tmp.getIsolationWindow(), IsolationWindowCVParam.class, IsolationWindowUserParam.class);
            }

            if (target instanceof SelectedIonList) {
                SelectedIonList tmp = (SelectedIonList) target;
                if (tmp.getSelectedIon() != null && !tmp.getSelectedIon().isEmpty()) {
                    List<ParamGroup> tmpList = new ArrayList<ParamGroup>();
                    for (ParamGroup pg : tmp.getSelectedIon()) {
                        ParamGroupUpdater.updateParamGroupSubclasses(pg, SelectedIonCVParam.class, SelectedIonUserParam.class);
                        tmpList.add(pg);
                    }
                    tmp.getSelectedIon().clear();
                    tmp.getSelectedIon().addAll(tmpList);
                }
            }

            if (target instanceof TargetList) {
                TargetList tmp = (TargetList) target;
                if (tmp.getTarget() != null && !tmp.getTarget().isEmpty()) {
                    List<ParamGroup> tmpList = new ArrayList<ParamGroup>();
                    for (ParamGroup pg : tmp.getTarget()) {
                        ParamGroupUpdater.updateParamGroupSubclasses(pg, TargetCVParam.class, TargetUserParam.class);
                        tmpList.add(pg);
                    }
                    tmp.getTarget().clear();
                    tmp.getTarget().addAll(tmpList);
                }
            }

            if (target instanceof ScanWindowList) {
                ScanWindowList tmp = (ScanWindowList) target;
                if (tmp.getScanWindow() != null && !tmp.getScanWindow().isEmpty()) {
                    List<ParamGroup> tmpList = new ArrayList<ParamGroup>();
                    for (ParamGroup pg : tmp.getScanWindow()) {
                        ParamGroupUpdater.updateParamGroupSubclasses(pg, ScanWindowCVParam.class, ScanWindowUserParam.class);
                        tmpList.add(pg);
                    }
                    tmp.getScanWindow().clear();
                    tmp.getScanWindow().addAll(tmpList);
                }
            }

        } catch (InstantiationException e) {
            throw new RuntimeException(this.getClass().getName() + ".afterUnmarshall: " + e.getMessage());
        } catch (IllegalAccessException e) {
            throw new RuntimeException(this.getClass().getName() + ".afterUnmarshall: " + e.getMessage());
        }


    }

    private void referenceResolving(Object target, Object parent, MzMLElement ele) {
        if (ele.isAutoRefResolving()) {
            Class cls = ele.getRefResolverClass();
            if (cls == null) {
                throw new IllegalStateException("Can not auto-resolve references if no reference resolver was defined for class: " + ele.getClazz());
            }
            try {
                Constructor con = cls.getDeclaredConstructor(MzMLIndexer.class, MzMLObjectCache.class);
                AbstractReferenceResolver resolver = (AbstractReferenceResolver) con.newInstance(index, cache);
                resolver.afterUnmarshal(target, parent);
            } catch (Exception e) {
                log.error("Error trying to instantiate reference resolver: " + cls.getName(), e);
                throw new IllegalStateException("Could not instantiate reference resolver: " + cls.getName());
            }
        }
    }
}
