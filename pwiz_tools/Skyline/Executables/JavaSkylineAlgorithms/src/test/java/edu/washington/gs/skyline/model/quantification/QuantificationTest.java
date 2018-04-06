/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
package edu.washington.gs.skyline.model.quantification;

import junit.framework.TestCase;
import org.apache.commons.csv.CSVFormat;
import org.apache.commons.csv.CSVParser;
import org.apache.commons.csv.CSVRecord;
import org.apache.commons.lang3.tuple.ImmutablePair;
import org.apache.commons.lang3.tuple.Pair;
import sun.reflect.generics.reflectiveObjects.NotImplementedException;

import java.io.InputStreamReader;
import java.io.Reader;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

public class QuantificationTest extends TestCase {
    public void testNoNormalization() throws Exception {
        List<InputRecord> allInputRecords = readInputRecords("NoNormalizationInput.csv");
        allInputRecords = filterRecords(NormalizationMethod.NONE, allInputRecords);
        Map<RecordKey, Double> expected = readExpectedRows("NoNormalizationExpected.csv");
        for (Map.Entry<RecordKey, Double> entry : expected.entrySet()) {
            List<InputRecord> peptideRecords = allInputRecords.stream()
                    .filter(record->record.getRecordKey().getPeptideKey().equals(entry.getKey().getPeptideKey()))
                    .collect(Collectors.toList());
            TransitionKeys allTransitionKeys = TransitionKeys.of(peptideRecords.stream().map(InputRecord::getTransitionKey)
                    .collect(Collectors.toSet()));
            List<InputRecord> replicateRecords = allInputRecords.stream()
                    .filter(record->record.getRecordKey().equals(entry.getKey()))
                    .collect(Collectors.toList());
            Map<String, Double> areaMap = replicateRecords.stream()
                    .collect(Collectors.toMap(InputRecord::getTransitionKey, InputRecord::getArea));
            TransitionAreas transitionAreas = TransitionAreas.fromMap(areaMap);
            assertCloseEnough(entry.getValue(), transitionAreas.totalArea(allTransitionKeys));
        }
    }

    public void testRatioToHeavy() throws Exception {
        List<InputRecord> allInputRecords = readInputRecords("NoNormalizationInput.csv");
        allInputRecords = filterRecords(new NormalizationMethod.RatioToLabel("heavy"), allInputRecords);
        Map<RecordKey, Double> expected = readExpectedRows("RatioToHeavy.csv");
        for (Map.Entry<RecordKey, Double> entry : expected.entrySet()) {
            List<InputRecord> records =
                    allInputRecords.stream().filter(record->record.getRecordKey().equals(entry.getKey())).collect(Collectors.toList());
            TransitionAreas numerator = TransitionAreas.fromMap(records.stream().filter(record->"light".equals(record.getIsotopeLabelType()))
                    .collect(Collectors.toMap(InputRecord::getTransitionKey, InputRecord::getArea)));
            TransitionAreas denominator = TransitionAreas.fromMap(records.stream().filter(record->"heavy".equals(record.getIsotopeLabelType()))
                    .collect(Collectors.toMap(InputRecord::getTransitionKey, InputRecord::getArea)));

            Double actualArea = numerator.ratioTo(denominator);
            assertCloseEnough(entry.getValue(), actualArea);
        }
    }

    private void assertCloseEnough(Double expected, Double actual) {
        if (expected == null) {
            assertNull(actual);
        } else {
            assertNotNull(actual);
            assertEquals(expected, actual, Math.abs(expected/100));
        }
    }

    List<InputRecord> filterRecords(NormalizationMethod normalizationMethod, List<InputRecord> list) {
        return list.stream().filter(record->acceptRecord(normalizationMethod, record)).collect(Collectors.toList());
    }

    private boolean acceptRecord(NormalizationMethod normalizationMethod, InputRecord record) {
        if (record.getArea() == null) {
            return false;
        }
        if (!(normalizationMethod instanceof NormalizationMethod.RatioToLabel)
                && !"light".equals(record.getIsotopeLabelType())) {
            return false;
        }
        if (!normalizationMethod.isAllowTruncatedTransitions() && record.isTruncated()) {
            return false;
        }
        return true;
    }

    private List<InputRecord> readInputRecords(String filename) throws Exception {
        List<InputRecord> list = new ArrayList<>();
        Reader reader = new InputStreamReader(QuantificationTest.class.getResourceAsStream(filename));
        try {
            CSVParser parser = new CSVParser(reader, CSVFormat.EXCEL.withHeader());
            for (CSVRecord record : parser.getRecords()) {
                list.add(new InputRecord(record));
            }

        } finally {
            reader.close();
        }
        return list;
    }

    private Map<RecordKey, Double> readExpectedRows(String filename) throws Exception {
        Map<RecordKey, Double> map = new HashMap<>();
        Reader reader = new InputStreamReader(QuantificationTest.class.getResourceAsStream(filename));
        try {
            CSVParser parser = new CSVParser(reader, CSVFormat.EXCEL.withHeader());
            for (CSVRecord record : parser.getRecords()) {
                map.put(new RecordKey(record), parseNullableDouble(record.get("NormalizedArea")));
            }
        } finally {
            reader.close();
        }
        return map;
    }


    private List<ReplicateData> readReplicates(String filename) throws Exception {
        Map<String, ReplicateData> replicates = new LinkedHashMap<>();
        Reader reader = new InputStreamReader(QuantificationTest.class.getResourceAsStream(filename));
        try {
            CSVParser parser = new CSVParser(reader, CSVFormat.EXCEL.withHeader());
            for (CSVRecord record : parser.getRecords()) {
                String fileName = record.get("FileName");
                ReplicateData replicate = replicates.get(fileName);
                if (replicate == null) {
                    replicate = new ReplicateData();
                    replicates.put(fileName, replicate);
                }
            }

        } finally {
            reader.close();
        }
        throw new NotImplementedException();
    }

    public class InputRecord {
        final RecordKey key;
        final int precursorCharge;
        final String fragmentIon;
        final int productCharge;
        final String isotopeLabelType;
        final String condition;
        final String bioReplicate;
        final Double area;
        final String standardType;
        final boolean truncated;

        public InputRecord(CSVRecord record) {
            this.key = new RecordKey(record);
            precursorCharge = Integer.parseInt(record.get("PrecursorCharge"));
            fragmentIon = record.get("FragmentIon");
            productCharge = Integer.parseInt(record.get("ProductCharge"));
            isotopeLabelType = record.get("IsotopeLabelType");
            condition = record.get("Condition");
            bioReplicate = record.get("BioReplicate");
            area = parseNullableDouble(record.get("Area"));
            standardType = record.get("StandardType");
            truncated = Boolean.parseBoolean(record.get("Truncated"));
        }


        public RecordKey getRecordKey() {
            return key;
        }

        public String getTransitionKey() {
            return getPrecursorCharge() + "-" + getFragmentIon() + "-" + getProductCharge();
        }

        public int getPrecursorCharge() {
            return precursorCharge;
        }

        public String getFragmentIon() {
            return fragmentIon;
        }

        public int getProductCharge() {
            return productCharge;
        }

        public String getIsotopeLabelType() {
            return isotopeLabelType;
        }

        public String getCondition() {
            return condition;
        }

        public String getBioReplicate() {
            return bioReplicate;
        }

        public Double getArea() {
            return area;
        }

        public String getStandardType() {
            return standardType;
        }

        public boolean isTruncated() {
            return truncated;
        }
    }

    public class RecordKey {
        final String proteinName;
        final String peptideModifiedSequence;
        final String fileName;

        public RecordKey(CSVRecord record) {
            this(record.get("ProteinName"), record.get("PeptideModifiedSequence"), record.get("FileName"));
        }
        public RecordKey(String proteinName, String peptideModifiedSequence, String fileName) {
            this.proteinName = proteinName;
            this.peptideModifiedSequence = peptideModifiedSequence;
            this.fileName = fileName;
        }

        public Pair<String, String> getPeptideKey() {
            return new ImmutablePair<>(getProteinName(), getPeptideModifiedSequence());
        }

        public String getProteinName() {
            return proteinName;
        }

        public String getPeptideModifiedSequence() {
            return peptideModifiedSequence;
        }

        public String getFileName() {
            return fileName;
        }

        @Override
        public boolean equals(Object o) {
            if (this == o) return true;
            if (o == null || getClass() != o.getClass()) return false;

            RecordKey recordKey = (RecordKey) o;

            if (!proteinName.equals(recordKey.proteinName)) return false;
            if (!peptideModifiedSequence.equals(recordKey.peptideModifiedSequence)) return false;
            return fileName.equals(recordKey.fileName);

        }

        @Override
        public int hashCode() {
            int result = proteinName.hashCode();
            result = 31 * result + peptideModifiedSequence.hashCode();
            result = 31 * result + fileName.hashCode();
            return result;
        }
    }
    private static Double parseNullableDouble(String value) {
        if ("#N/A".equals(value)) {
            return null;
        }
        return Double.parseDouble(value);
    }
}
