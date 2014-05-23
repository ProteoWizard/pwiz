package uk.ac.ebi.jmzml.xml.util;

import it.unimi.dsi.fastutil.chars.CharOpenHashSet;

/**
 * Created with IntelliJ IDEA.
 * User: rcote
 * Date: 04/04/13
 * Time: 14:39
 * To change this template use File | Settings | File Templates.
 */
public class EscapingXMLUtilities {

    public static final char substitute = '\uFFFD';
    private static final CharOpenHashSet illegalChars;

    private EscapingXMLUtilities() {
    }

    static {
        /**
         // excluded control characters
         \u0000 Null character
         \u0001 Start of header
         \u0002 Start of text
         \u0003 End of text
         \u0004 End of transmission
         \u0005 Enquiry
         \u0006 Positive acknowledge
         \u0007 Alert (bell)
         \u0008 Backspace
         \u000B Vertical tab
         \u000C Form feed
         \u000E Shift out
         \u000F Shift in
         \u0010 Data link escape
         \u0011 Device control 1 (XON)
         \u0012 Device control 2 (tape on)
         \u0013 Device control 3 (XOFF)
         \u0014 Device control 4 (tape off)
         \u0015 Negative acknowledgement
         \u0016 Synchronous idle
         \u0017 End of transmission block
         \u0018 Cancel
         \u0019 End of medium
         \u001A Substitute
         \u001B Escape
         \u001C File separator (Form separator)
         \u001D Group separator
         \u001E Record separator
         \u001F Unit separator

         // not excluded control characters
         \u0009 Horizontal tab
         \u000A Line feed
         \u000D Carriage return

         //valid, but discouraged
         \u0080 	<control>
         \u0081 	<control>
         \u0082 	BREAK PERMITTED HERE
         \u0083 	NO BREAK HERE
         \u0084 	<control>
         \u0085 	NEXT LINE (NEL)
         \u0086 	START OF SELECTED AREA
         \u0087 	END OF SELECTED AREA
         \u0088 	CHARACTER TABULATION SET
         \u0089 	CHARACTER TABULATION WITH JUSTIFICATION
         \u008A 	LINE TABULATION SET
         \u008B 	PARTIAL LINE FORWARD
         \u008C 	PARTIAL LINE BACKWARD
         \u008D 	REVERSE LINE FEED
         \u008E 	SINGLE SHIFT TWO
         \u008F 	SINGLE SHIFT THREE
         \u0090 	DEVICE CONTROL STRING
         \u0091 	PRIVATE USE ONE
         \u0092 	PRIVATE USE TWO
         \u0093 	SET TRANSMIT STATE
         \u0094 	CANCEL CHARACTER
         \u0095 	MESSAGE WAITING
         \u0096 	START OF GUARDED AREA
         \u0097 	END OF GUARDED AREA
         \u0098 	START OF STRING
         \u0099 	<control>
         \u009A 	SINGLE CHARACTER INTRODUCER
         \u009B 	CONTROL SEQUENCE INTRODUCER
         \u009C 	STRING TERMINATOR
         \u009D 	OPERATING SYSTEM COMMAND
         \u009E 	PRIVACY MESSAGE
         \u009F 	APPLICATION PROGRAM COMMAND

         */

        final String escapeString = "\u0000\u0001\u0002\u0003\u0004\u0005" +
                "\u0006\u0007\u0008\u009D\u000B\u000C\u000E\u000F\u0010\u0011\u0012" +
                "\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C" +
                "\u001D\u001E\u001F\uFFFE\uFFFF" +
                "\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B" +
                "\u008C\u008D\u008E\u008F\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097"  +
                "\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F";

        illegalChars = new CharOpenHashSet();
        for (int i = 0; i < escapeString.length(); i++) {
            illegalChars.add(escapeString.charAt(i));
        }
    }

    private static boolean isIllegal(char c) {
        return illegalChars.contains(c);
    }

    /**
     * Substitutes all illegal characters in the given string by the value of
     * {@link EscapingXMLUtilities#substitute}. If no illegal characters
     * were found, no copy is made and the given string is returned.
     *
     * @param string
     * @return
     */
    public static String escapeCharacters(String string) {

        char[] copy = null;
        boolean copied = false;
        for (int i = 0; i < string.length(); i++) {
            if (isIllegal(string.charAt(i))) {
                if (!copied) {
                    copy = string.toCharArray();
                    copied = true;
                }
                copy[i] = substitute;
            }
        }
        return copied ? new String(copy) : string;
    }

}
