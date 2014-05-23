package edu.uw.gs.skyline.cloudapi.chromatogramgenerator;

/**
 * Created by nicksh on 3/7/14.
 */
class ByteConverter {
    public byte[] toByteArray(int[] ints) {
        byte[] result = new byte[ints.length * 4];
        for (int i = 0; i < ints.length; i++) {
            int value = ints[i];
            result[i * 4] = (byte) (value & 0xff);
            result[i * 4 + 1] = (byte) ((value & 0xff00) >> 8);
            result[i * 4 + 2] = (byte) ((value & 0xff0000) >> 16);
            result[i * 4 + 3] = (byte) ((value & 0xff000000) >> 24);
        }
        return result;
    }
    public byte[] toByteArray(short[] shorts) {
        byte[] result = new byte[shorts.length * 2];
        for (int i = 0; i < shorts.length; i++) {
            short value = shorts[i];
            result[i * 2] = (byte) (value & 0xff);
            result[i * 2 + 1] = (byte)((value & 0xff00) >> 8);
        }
        return result;
    }
    public byte[] toByteArray(long[] longs) {
        byte[] result = new byte[longs.length * 8];
        for (int i = 0; i < longs.length; i++) {
            long value = longs[i];
            result[i * 8] = (byte) (value & 0xffL);
            result[i * 8 + 1] = (byte) ((value & 0xff00L) >> 8);
            result[i * 8 + 2] = (byte) ((value & 0xff0000L) >> 16);
            result[i * 8 + 3] = (byte) ((value & 0xff000000L) >> 24);
            result[i * 8 + 4] = (byte) ((value & 0xff00000000L) >> 32);
            result[i * 8 + 5] = (byte) ((value & 0xff0000000000L) >> 40);
            result[i * 8 + 6] = (byte) ((value & 0xff000000000000L) >> 48);
            result[i * 8 + 7] = (byte) ((value & 0xff00000000000000L) >> 56);
        }
        return result;
    }
    public byte[] toByteArray(float[] floats) {
        int[] ints = new int[floats.length];
        for (int i = 0; i < ints.length; i++) {
            ints[i] = Float.floatToIntBits(floats[i]);
        }
        return toByteArray(ints);
    }
    public byte[] toByteArray(double[] doubles) {
        long[] longs = new long[doubles.length];
        for (int i = 0; i < longs.length; i ++) {
            longs[i] = Double.doubleToLongBits(doubles[i]);
        }
        return toByteArray(longs);
    }
}
