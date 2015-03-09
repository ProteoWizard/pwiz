package edu.uw.gs.skyline.cloudapi.chromatogramgenerator;

import java.io.IOException;
import java.io.OutputStream;

/**
 * Created by nicksh on 3/5/14.
 */
class StreamWrapper extends OutputStream {
    private final OutputStream outputStream;
    private long position;
    private static final ByteConverter byteConverter = new ByteConverter();
    public StreamWrapper(OutputStream outputStream) {
        this.outputStream = outputStream;
    }

    @Override
    public void write(int b) throws IOException {
        outputStream.write(b);
        incrementPosition(1);
    }

    @Override
    public void write(byte[] b) throws IOException {
        outputStream.write(b);
        incrementPosition(b.length);
    }

    @Override
    public void write(byte[] b, int off, int len) throws IOException {
        outputStream.write(b, off, len);
        incrementPosition(len);
    }

    public long getPosition() {
        return position;
    }

    public void writeByte(byte b) throws IOException {
        write((int) b);
    }

    public void writeInt(int value) throws IOException {
        write(byteConverter.toByteArray(new int []{value}));
    }
    public void writeShort(short value) throws IOException {
        write(byteConverter.toByteArray(new short[] {value}));
    }
    public void writeLong(long value) throws IOException {
        write(byteConverter.toByteArray(new long[]{value}));
    }
    public void writeDouble(double value) throws IOException {
        write(byteConverter.toByteArray(new double[]{value}));
    }
    public void writeFloat(float value) throws IOException {
        write(byteConverter.toByteArray(new float[]{value}));
    }
    private void incrementPosition(int amount) {
        position += amount;
    }
}
