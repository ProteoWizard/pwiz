/*
        MSNumpress.java
        johan.teleman@immun.lth.se
 
        Copyright 2013 Johan Teleman

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 */

package uk.ac.ebi.jmzml.model.mzml.utilities;

import java.util.Arrays;

/**
 *
 * @author fgonzalez, jteleman, sperkins
 */
public class MSNumpress {

        ///PSI-MS obo accession numbers.
        public static final String ACC_NUMPRESS_LINEAR  = "MS:1002312";
        public static final String ACC_NUMPRESS_PIC = "MS:1002313";
        public static final String ACC_NUMPRESS_SLOF = "MS:1002314";
        
        public static byte[] encode(double[] data, String cvAccession) {
            if (cvAccession.equals(ACC_NUMPRESS_LINEAR)) {
                byte[] buffer = new byte[8 + (data.length * 5)];
                int encodedBytes = MSNumpress.encodeLinear(data, data.length, buffer, MSNumpress.optimalLinearFixedPoint(data, data.length));                
                return Arrays.copyOf(buffer, encodedBytes);
            } else if (cvAccession.equals(ACC_NUMPRESS_SLOF)) {
                byte[] buffer = new byte[8 + (data.length * 2)];
                int encodedBytes = MSNumpress.encodeSlof(data, data.length, buffer, MSNumpress.optimalSlofFixedPoint(data, data.length));
                return Arrays.copyOf(buffer, encodedBytes);
            } else if (cvAccession.equals(ACC_NUMPRESS_PIC)) {
                byte[] buffer = new byte[data.length * 5];
                int encodedBytes = MSNumpress.encodePic(data, data.length, buffer);                
                return Arrays.copyOf(buffer, encodedBytes);
            }
            
            throw new IllegalArgumentException("'"+cvAccession+"' is not a numpress compression term");
        }
        
        /**
         * Convenience function for decoding binary data encoded by MSNumpress. If
         * the passed cvAccession is one of
         *
         * ACC_NUMPRESS_LINEAR = "MS:1002312"
         * ACC_NUMPRESS_PIC = "MS:1002313"
         * ACC_NUMPRESS_SLOF = "MS:1002314"
         *
         * the corresponding decode function will be called.
         *
         * @cvAccession                The PSI-MS obo CV accession of the encoded data.
         * @data                        array of double to be encoded
         * @dataSize                number of doubles from data to encode
         * @return                        The decoded doubles
         */
        public static Double[] decode(String cvAccession, byte[] data) {
                
                if (cvAccession.equals(ACC_NUMPRESS_LINEAR)) {
                        Double[] buffer         = new Double[data.length * 2];
                        int nbrOfDoubles         = MSNumpress.decodeLinear(data, data.length, buffer);
                        Double[] result         = new Double[nbrOfDoubles];
                        System.arraycopy(buffer, 0, result, 0, nbrOfDoubles);
                        return result;
                        
                } else if (cvAccession.equals(ACC_NUMPRESS_SLOF)) {
                        Double[] buffer         = new Double[data.length / 2];
                        int nbrOfDoubles = MSNumpress.decodeSlof(data, data.length, buffer);
                        Double[] result = new Double[nbrOfDoubles];
                        System.arraycopy(buffer, 0, result, 0, nbrOfDoubles);
                        return result;
                        
                } else if (cvAccession.equals(ACC_NUMPRESS_PIC)) {
                        Double[] buffer         = new Double[data.length * 2];
                        int nbrOfDoubles         = MSNumpress.decodePic(data, data.length, buffer);
                        Double[] result         = new Double[nbrOfDoubles];
                        System.arraycopy(buffer, 0, result, 0, nbrOfDoubles);
                        return result;
                        
                }
                
                throw new IllegalArgumentException("'"+cvAccession+"' is not a numpress compression term");
        }       

        /**
         * This encoding works on a 4 byte integer, by truncating initial zeros or ones.
         * If the initial (most significant) half byte is 0x0 or 0xf, the number of such
         * halfbytes starting from the most significant is stored in a halfbyte. This initial
         * count is then followed by the rest of the ints halfbytes, in little-endian order.
         * A count halfbyte c of
         *
         *                 0 <= c <= 8                 is interpreted as an initial c                 0x0 halfbytes
         *                 9 <= c <= 15                is interpreted as an initial (c-8)         0xf halfbytes
         *
         * Ex:
         *         int                c                rest
         *         0         =>         0x8
         *         -1        =>        0xf                0xf
         *         23        =>        0x6         0x7        0x1
         *
         *         @x                        the int to be encoded
         *        @res                the byte array were halfbytes are stored
         *        @resOffset        position in res were halfbytes are written
         *        @return                the number of resulting halfbytes
         */
        public static int encodeInt(
                        long x,
                        byte[] res,
                        int resOffset
        ) {
                byte i, l;
                long m;
                long mask = 0xf0000000;
                long init = x & mask;

                if (init == 0) {
                        l = 8;
                        for (i=0; i<8; i++) {
                                m = mask >> (4*i);
                                if ((x & m) != 0) {
                                        l = i;
                                        break;
                                }
                        }
                        res[resOffset] = l;
                        for (i=l; i<8; i++)
                                res[resOffset+1+i-l] = (byte)(0xf & (x >> (4*(i-l))));
                        
                        return 1+8-l;

                } else if (init == mask) {
                        l = 7;
                        for (i=0; i<8; i++) {
                                m = mask >> (4*i);
                                if ((x & m) != m) {
                                        l = i;
                                        break;
                                }
                        }
                        res[resOffset] = (byte)(l | 8);
                        for (i=l; i<8; i++)
                                res[resOffset+1+i-l] = (byte)(0xf & (x >> (4*(i-l))));
                        
                        return 1+8-l;

                } else {
                        res[resOffset] = 0;
                        for (i=0; i<8; i++)
                                res[resOffset+1+i] = (byte)(0xf & (x >> (4*i)));
                        
                        return 9;

                }
        }
        
        
        
        public static void encodeFixedPoint(
                        double fixedPoint,
                        byte[] result
        ) {
                long fp = Double.doubleToLongBits(fixedPoint);
                for (int i=0; i<8; i++) {
                        result[7-i] = (byte)((fp >> (8*i)) & 0xff);
                }
        }
        
        
        
        public static double decodeFixedPoint(
                        byte[] data
        ) {
                long fp = 0;
                for (int i=0; i<8; i++) {
                        fp = fp | ((0xFFl & data[7-i]) << (8*i));
                }
                return Double.longBitsToDouble(fp);
        }
        
        
        
        
        /////////////////////////////////////////////////////////////////////////////////
        
        
        public static double optimalLinearFixedPoint(
                double[] data,
                int dataSize
        ) {
                if (dataSize == 0) return 0;
                if (dataSize == 1) return Math.floor(0xFFFFFFFFl / data[0]);
                double maxDouble = Math.max(data[0], data[1]);
                
                for (int i=2; i<dataSize; i++) {
                        double extrapol = data[i-1] + (data[i-1] - data[i-2]);
                        double diff         = data[i] - extrapol;
                        maxDouble                 = Math.max(maxDouble, Math.ceil(Math.abs(diff)+1));
                }

                return Math.floor(0x7FFFFFFFl / maxDouble);
        }


        /**
         * Encodes the doubles in data by first using a
         * - lossy conversion to a 4 byte 5 decimal fixed point repressentation
         * - storing the residuals from a linear prediction after first to values
         * - encoding by encodeInt (see above)
         *
         * The resulting binary is maximally dataSize * 5 bytes, but much less if the
         * data is reasonably smooth on the first order.
         *
         * This encoding is suitable for typical m/z or retention time binary arrays.
         * For masses above 100 m/z the encoding is accurate to at least 0.1 ppm.
         *
         * @data                array of double to be encoded
         * @dataSize        number of doubles from data to encode
         * @result                array were resulting bytes should be stored
         * @fixedPoint        the scaling factor used for getting the fixed point repr.
         *                                 This is stored in the binary and automatically extracted
         *                                 on decoding.
         * @return                the number of encoded bytes
         */
        public static int encodeLinear(
                        double[] data,
                        int dataSize,
                        byte[] result,
                        double fixedPoint
        ) {
                long[] ints = new long[3];
                int i;
                int ri = 16;
                byte halfBytes[] = new byte[10];
                int halfByteCount = 0;
                int hbi;
                long extrapol;
                long diff;
                
                encodeFixedPoint(fixedPoint, result);

                if (dataSize == 0) return 8;
                
                ints[1] = (long)(data[0] * fixedPoint + 0.5);
                for (i=0; i<4; i++) {
                        result[8+i] = (byte)((ints[1] >> (i*8)) & 0xff);
                }
                
                if (dataSize == 1) return 12;
                
                ints[2] = (long)(data[1] * fixedPoint + 0.5);
                for (i=0; i<4; i++) {
                        result[12+i] = (byte)((ints[2] >> (i*8)) & 0xff);
                }

                halfByteCount = 0;
                ri = 16;
                
                for (i=2; i<dataSize; i++) {
                        ints[0] = ints[1];
                        ints[1] = ints[2];
                        ints[2] = (long)(data[i] * fixedPoint + 0.5);
                        extrapol = ints[1] + (ints[1] - ints[0]);
                        diff = ints[2] - extrapol;
                        halfByteCount += encodeInt(diff, halfBytes, halfByteCount);
                                        
                        for (hbi=1; hbi < halfByteCount; hbi+=2)
                                result[ri++] = (byte)((halfBytes[hbi-1] << 4) | (halfBytes[hbi] & 0xf));
                        
                        if (halfByteCount % 2 != 0) {
                                halfBytes[0] = halfBytes[halfByteCount-1];
                                halfByteCount = 1;
                        } else
                                halfByteCount = 0;

                }
                if (halfByteCount == 1)
                        result[ri++] = (byte)(halfBytes[0] << 4);

                return ri;
        }
        
        
        
        /**
         * Decodes data encoded by encodeLinear. Note that the compression
         * discard any information < 1e-5, so data is only guaranteed
         * to be within +- 5e-6 of the original value.
         *
         * Further, values > ~42000 will also be truncated because of the
         * fixed point representation, so this scheme is stronly discouraged
         * if values above might be above this size.
         *
         * result vector guaranteedly shorter than twice the data length (in nbr of values)
         * returns the number of doubles read
         *
         * @data                array of bytes to be decoded
         * @dataSize        number of bytes from data to decode
         * @result                array were resulting doubles should be stored
         * @return                the number of decoded doubles, or -1 if dataSize < 4 or 4 < dataSize < 8
         */
        public static int decodeLinear(
                        byte[] data,
                        int dataSize,
                        Double[] result
        ) {
                int ri = 2;
                long[] ints = new long[3];
                long extrapol;
                long y;
                IntDecoder dec = new IntDecoder(data, 16);
                
                if (dataSize < 8) return -1;
                double fixedPoint = decodeFixedPoint(data);        
                if (dataSize < 12) return -1;
                
                ints[1] = 0;
                for (int i=0; i<4; i++) {
                        ints[1] = ints[1] | ( (0xFFl & data[8+i]) << (i*8));
                }
                result[0] = ints[1] / fixedPoint;
                
                if (dataSize == 12) return 1;
                if (dataSize < 16) return -1;
                
                ints[2] = 0;
                for (int i=0; i<4; i++) {
                        ints[2] = ints[2] | ( (0xFFl & data[12+i]) << (i*8));
                }
                result[1] = ints[2] / fixedPoint;
        
                while (dec.pos < dataSize) {
                        if (dec.pos == (dataSize - 1) && dec.half)
                                if ((data[dec.pos] & 0xf) != 0x8)
                                        break;
                        
                        ints[0] = ints[1];
                        ints[1] = ints[2];
                        ints[2] = dec.next();
                        
                        extrapol = ints[1] + (ints[1] - ints[0]);
                        y = extrapol + ints[2];
                        result[ri++] = y / fixedPoint;
                        ints[2] = y;
                }
                
                return ri;
        }
        
        
        
        /////////////////////////////////////////////////////////////////////////////////
        
        /**
         * Encodes ion counts by simply rounding to the nearest 4 byte integer,
         * and compressing each integer with encodeInt.
         *
         * The handleable range is therefore 0 -> 4294967294.
         * The resulting binary is maximally dataSize * 5 bytes, but much less if the
         * data is close to 0 on average.
         *
         * @data                array of doubles to be encoded
         * @dataSize        number of doubles from data to encode
         * @result                array were resulting bytes should be stored
         * @return                the number of encoded bytes
         */
        public static int encodePic(
                        double[] data,
                        int dataSize,
                        byte[] result
        ) {
                long count;
                int ri = 0;
                int hbi = 0;
                byte halfBytes[] = new byte[10];
                int halfByteCount = 0;

                //printf("Encoding %d doubles\n", (int)dataSize);

                for (int i=0; i<dataSize; i++) {
                        count = (long)(data[i] + 0.5);
                        halfByteCount += encodeInt(count, halfBytes, halfByteCount);
                                        
                        for (hbi=1; hbi < halfByteCount; hbi+=2)
                                result[ri++] = (byte)((halfBytes[hbi-1] << 4) | (halfBytes[hbi] & 0xf));
                        
                        if (halfByteCount % 2 != 0) {
                                halfBytes[0] = halfBytes[halfByteCount-1];
                                halfByteCount = 1;
                        } else
                                halfByteCount = 0;

                }
                if (halfByteCount == 1)
                        result[ri++] = (byte)(halfBytes[0] << 4);
                
                return ri;
        }
        
        
        /**
         * Decodes data encoded by encodePic
         *
         * result vector guaranteedly shorter than twice the data length (in nbr of values)
         *
         * @data                array of bytes to be decoded (need memorycont. repr.)
         * @dataSize        number of bytes from data to decode
         * @result                array were resulting doubles should be stored
         * @return                the number of decoded doubles
         */
        public static int decodePic(
                        byte[] data,
                        int dataSize,
                        Double[] result
        ) {
                int ri = 0;
                long count;
                IntDecoder dec = new IntDecoder(data, 0);

                while (dec.pos < dataSize) {
                        if (dec.pos == (dataSize - 1) && dec.half)
                                if ((data[dec.pos] & 0xf) != 0x8)
                                        break;
                                
                        count = dec.next();
                        result[ri++] = (double)count;
                }
                
                return ri;
        }
        
        
        
        
        /////////////////////////////////////////////////////////////////////////////////
        
        public static double optimalSlofFixedPoint(
                        double[] data,
                        int dataSize
        ) {
                if (dataSize == 0) return 0;
        
                double maxDouble = 1;
                double x;
                double fp;

                for (int i=0; i<dataSize; i++) {
                        x = Math.log(data[i]+1);
                        maxDouble = Math.max(maxDouble, x);
                }

                fp = Math.floor(0xFFFF / maxDouble);

                return fp;
        }

        /**
         * Encodes ion counts by taking the natural logarithm, and storing a
         * fixed point representation of this. This is calculated as
         *
         * unsigned short fp = log(d) * fixedPoint + 0.5
         *
         * @data                array of doubles to be encoded
         * @dataSize        number of doubles from data to encode
         * @result                array were resulting bytes should be stored
         * @fixedPoint        the scaling factor used for getting the fixed point repr.
         *                                 This is stored in the binary and automatically extracted
         *                                 on decoding.
         * @return                the number of encoded bytes
         */
        public static int encodeSlof(
                        double[] data,
                        int dataSize,
                        byte[] result,
                        double fixedPoint
        ) {
                int x;
                int ri = 8;
                
                encodeFixedPoint(fixedPoint, result);
                
                for (int i=0; i<dataSize; i++) {
                        x = (int)(Math.log(data[i]+1) * fixedPoint + 0.5);
                
                        result[ri++] = (byte)(0xff & x);
                        result[ri++] = (byte)(x >> 8);
                }
                return ri;
        }
        
        
        /**
         * Decodes data encoded by encodeSlof
         *
         * result vector length is twice the data length
         * returns the number of doubles read
         *
         * @data                array of bytes to be decoded (need memorycont. repr.)
         * @dataSize        number of bytes from data to decode
         * @result                array were resulting doubles should be stored
         * @return                the number of decoded doubles
         */
        public static int decodeSlof(
                        byte[] data,
                        int dataSize,
                        Double[] result
        ) {
                int x;
                int ri = 0;
                
                if (dataSize < 8) return -1;
                double fixedPoint = decodeFixedPoint(data);        
                
                for (int i=8; i<dataSize; i+=2) {
                        x = (0xff & data[i]) | ((0xff & data[i+1]) << 8);
                        result[ri++] = Math.exp(((double)(0xffff & x)) / fixedPoint) - 1;
                }
                return ri;
        }
}
