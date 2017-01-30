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

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collection;
import java.util.Collections;
import java.util.HashSet;
import java.util.Iterator;
import java.util.List;

public class TransitionKeys implements Iterable<String>
{
    public static final TransitionKeys EMPTY = new TransitionKeys(new String[0]);
    public static final TransitionKeys of(Collection<String> keys) {
        HashSet<String> hashSet = new HashSet<>(keys);
        int size = hashSet.size();
        String[] array = hashSet.toArray(new String[size]);
        Arrays.sort(array);
        return new TransitionKeys(array);
    }

    private TransitionKeys(String[] keys) {
        _keys = keys;
    }
    private final String[] _keys;

    public int indexOf(String key) {
        int index = Arrays.binarySearch(_keys, key);
        if (index < 0) {
            return -1;
        }
        return index;
    }

    public boolean contains(String key) {
        return indexOf(key) >= 0;
    }

    public boolean containsAll(Iterable<String> keys) {
        for (String key : keys) {
            if (!contains(key)) {
                return false;
            }
        }
        return true;
    }

    public String get(int index) {
        return _keys[index];
    }

    public int size() {
        return _keys.length;
    }

    public boolean isEmpty() {
        return size() == 0;
    }

    public List<String> asList() {
        return Collections.unmodifiableList(Arrays.asList(_keys));
    }

    @Override
    public Iterator<String> iterator() {
        return Arrays.asList(_keys).iterator();
    }

    public TransitionKeys union(TransitionKeys transitionKeys) {
        if (isEmpty()) {
            return transitionKeys;
        }
        if (transitionKeys.isEmpty()) {
            return this;
        }
        ArrayList<String> list = new ArrayList<>(size() + transitionKeys.size());
        list.addAll(asList());
        list.addAll(transitionKeys.asList());
        return of(list);
    }

    public TransitionKeys intersect(TransitionKeys transitionKeys) {
        HashSet<String> set = new HashSet<>(asList());
        set.retainAll(transitionKeys.asList());
        return of(set);
    }

    @Override
    public boolean equals(Object o) {
        if (this == o) {
            return true;
        }
        if (o == null || getClass() != o.getClass()) {
            return false;
        }
        TransitionKeys that = (TransitionKeys) o;
        return Arrays.deepEquals(_keys, that._keys);
    }

    @Override
    public int hashCode() {
        return Arrays.deepHashCode(_keys);
    }
}
