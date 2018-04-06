package edu.washington.gs.skyline.model;

import org.apache.commons.lang3.StringUtils;
import java.io.UnsupportedEncodingException;
import java.net.URLDecoder;
import java.net.URLEncoder;
import java.util.AbstractMap;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;
import java.util.stream.Stream;
import java.util.stream.StreamSupport;

/**
 * Created by nicksh on 9/6/2016.
 */
public class NameValueCollection {
    public static final NameValueCollection EMPTY
            = new NameValueCollection(Collections.<String, String>emptyMap().entrySet());

    private final Iterable<Map.Entry<String, String>> entries;
    public NameValueCollection(Iterable<Map.Entry<String, String>> entries) {
        this.entries = entries;
    }

    public Iterable<Map.Entry<String, String>> getEntries() {
        return entries;
    }

    private Stream<Map.Entry<String, String>> entriesStream() {
        return StreamSupport.stream(entries.spliterator(), false);
    }

    public String getFirstValue(String key) {
        Optional<Map.Entry<String, String>> entry = entriesStream()
                .filter(e->key.equals(e.getKey()))
                .findFirst();
        return entry.isPresent() ? entry.get().getValue() : null;
    }

    public List<String> getValues(String key) {
        return entriesStream()
                .filter(entry->key.equals(entry.getKey()))
                .map(Map.Entry::getValue)
                .collect(Collectors.toList());
    }

    public String toString() {
        String separator = "";
        StringBuilder stringBuilder = new StringBuilder();
        for (Map.Entry<String, String> entry : getEntries()) {
            stringBuilder.append(separator);
            separator = "&";
            stringBuilder.append(encode(entry.getKey()));
            if (null != entry.getValue()) {
                stringBuilder.append("=");
                stringBuilder.append(encode(entry.getValue()));
            }
        }
        return stringBuilder.toString();
    }

    public static NameValueCollection parseQueryString(String s) {
        List<Map.Entry<String, String>> entries = Arrays.stream(StringUtils.split(s, '&'))
                .map(NameValueCollection::parseEntry)
                .collect(Collectors.toList());
        return new NameValueCollection(entries);
    }

    private static Map.Entry<String, String> parseEntry(String nameValue) {
        int ichEquals = nameValue.indexOf("=");
        if (ichEquals < 0) {
            return new AbstractMap.SimpleImmutableEntry<>(decode(nameValue), null);
        }
        return new AbstractMap.SimpleImmutableEntry<>(decode(nameValue.substring(0, ichEquals)),
                decode(nameValue.substring(ichEquals + 1)));
    }

    public static String encode(String s) {
        try {
            return URLEncoder.encode(s, "UTF-8");
        } catch (UnsupportedEncodingException uee) {
            throw new RuntimeException(uee);
        }
    }

    public static String decode(String s) {
        try {
            return URLDecoder.decode(s, "UTF-8");
        } catch (UnsupportedEncodingException uee) {
            throw new RuntimeException(uee);
        }
    }
}
