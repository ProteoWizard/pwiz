#include <iostream>
#include <zstd.h>
#include <array>
#include <vector>
#include <unordered_map>
#include <unordered_set>
#include <cstdint>
#include <algorithm>
#include "pwiz/utility/misc/span.hpp"
#include "pwiz/utility/misc/endian.hpp"

namespace mzd
{
    using byte_t = std::uint8_t;
    using buffer_t = std::vector<byte_t>;
    using buffer_span_t = tcb::span<const byte_t>;

    /// @brief Implementation of endianness
    namespace binary
    {
        const bool is_big_endian() {
            // The <bit> header isn't available on the minimum version of C++, so falling back on the method used in MSNumpress.cpp
            // the
            // #ifdef PWIZ_BIG_ENDIAN
            // return false;
            // #else
            // return true;
            // #endif
            const int ONE = 1;
            return *((char*)&(ONE)) == 1;
        }

        template <typename Z>
        union _byte_view_impl
        {
            Z value;
            std::array<uint8_t, sizeof(Z)> view;
        };

        template <typename Z>
        struct byte_view
        {
            _byte_view_impl<Z> inner;

            byte_view() {}

            byte_view(const Z value)
            {
                std::memcpy((void*)(&this->inner.value), (void*)(&value), sizeof(Z));
                // this->inner.value = value;
            }

            Z value()
            {
                return this->inner.value;
            }

            std::array<uint8_t, sizeof(Z)> &buffer()
            {
                return this->inner.view;
            }

            byte_view(std::array<uint8_t, sizeof(Z)> view)
            {
                this->inner.view = view;
            }

            static byte_view as_little_endian(Z value)
            {
                auto view = byte_view(value);
                if (is_big_endian())
                {
                    view.byteswap();
                }
                return view;
            }

            static byte_view as_little_endian(std::array<uint8_t, sizeof(Z)> value)
            {
                auto view = byte_view(value);
                if (is_big_endian())
                {
                    view.byteswap();
                }
                return view;
            }

            auto begin()
            {
                return this->inner.view.begin();
            }

            auto end()
            {
                return this->inner.view.end();
            }

            int byteswap()
            {
                std::reverse(this->begin(), this->end());
                return 0;
            }


        };

        template <typename Z>
        ostream &operator<<(ostream &os, byte_view<Z>& view) {
            os << "[ ";
            for_each(view.begin(), view.end(), [&os](byte_t &c)
                        { os << (int)c << ' '; });
            os << "]";
            return os;
        }
    }

    /// @brief Implementation details of byte-shuffling and delta codec
    namespace inner
    {

        using mzd::binary::byte_view;
        using mzd::binary::is_big_endian;

        template <typename T>
        void delta_encode(std::vector<T> &data)
        {
            size_t n = data.size();
            if (n < 2)
            {
                return;
            }
            T prev = data[0];
            T offset = data[0];

            for (size_t i = 1; i < n; i++)
            {
                T tmp = data[i];
                data[i] += offset - prev;
                prev = tmp;
            }
        }

        template <typename T>
        void delta_decode(std::vector<T> &data)
        {
            size_t n = data.size();
            if (n < 2)
            {
                return;
            }

            T offset = data[0];
            T prev = data[1];
            for (size_t i = 2; i < n; i++)
            {
                data[i] += prev - offset;
                prev = data[i];
            }
        }

        /// @brief Shuffle the bytes of `data` into `buffer`. Also enforces little-endian ordering
        /// @tparam T
        /// @param data The data to transpose
        /// @param buffer Where to transpose the data into
        template <typename T>
        void transpose(const std::vector<const T> &data, buffer_t &buffer)
        {
            const tcb::span<const T> view = tcb::span<const T>(data.data(), data.size());
            return transpose<T>(view, buffer);
        }

        /// @brief Shuffle the bytes of `data` into `buffer`. Also enforces little-endian ordering
        /// @tparam T
        /// @param data The data to transpose
        /// @param buffer Where to transpose the data into
        template <typename T>
        void transpose(const tcb::span<const T> &data, buffer_t &buffer)
        {
            auto nData = data.size();
            auto nBytes = nData * sizeof(T);

            buffer.clear();
            buffer.reserve(nBytes);
            for (size_t i = 0; i < sizeof(T); i++)
            {
                for (size_t j = 0; j < data.size(); j++)
                {
                    auto value = data[j];
                    byte_view<T> view = byte_view(value);
                    auto buf = view.buffer();
                    if (is_big_endian())
                    {
                        buffer.push_back(buf[(sizeof(T) - 1) - i]);
                    }
                    else
                    {
                        buffer.push_back(buf[i]);
                    }
                }
            }
            return;
        }

        /// @brief Reverses the byte shuffling done by `tranpose` to read values from `buffer` back out into `data`
        /// @tparam T
        /// @param buffer The transposesd data
        /// @param data Where to store the un-transposed data
        template <typename T>
        void reverse_transpose(const buffer_span_t &buffer, std::vector<T> &data)
        {
            auto nBytes = buffer.size();
            auto nData = nBytes / sizeof(T);
            data.resize(nData);
            for (size_t i = 0; i < buffer.size(); i++)
            {
                auto datum = &data[i % nData];
                auto byteView = reinterpret_cast<uint8_t *>(datum);
                if (is_big_endian())
                {
                    byteView[(sizeof(T) - 1) - (i / nData)] = buffer[i];
                }
                else
                {
                    byteView[i / nData] = buffer[i];
                }
            }
            return;
        }

    }

    /// @brief Implementation of the dictionary codec
    namespace dict
    {

        using mzd::binary::byte_view;
        using mzd::binary::is_big_endian;
        using mzd::inner::reverse_transpose;
        using mzd::inner::transpose;

        template <typename T, typename I, typename K>
        int encode_dictionary_indices(const tcb::span<const T> &data, std::vector<I> sorted_values, buffer_t &transposeBuffer, buffer_t &outBuffer)
        {
            std::unordered_map<I, size_t> value_to_indices;
            value_to_indices.reserve(sorted_values.size());

            for (size_t i = 0; i < sorted_values.size(); i++)
            {
                value_to_indices[sorted_values[i]] = i;

            }

            uint64_t n_values = sorted_values.size();

            auto offset_to_data = (sizeof(I) * n_values) + (sizeof(uint64_t) * 2);
            auto view = byte_view<uint64_t>::as_little_endian(offset_to_data);
            outBuffer.insert(outBuffer.end(), view.begin(), view.end());

            view = byte_view<uint64_t>::as_little_endian(n_values);
            outBuffer.insert(outBuffer.end(), view.begin(), view.end());

            transpose<I>(sorted_values, transposeBuffer);
            outBuffer.insert(outBuffer.end(), transposeBuffer.begin(), transposeBuffer.end());

            std::vector<K> index_buffer;
            index_buffer.reserve(data.size());

            for (auto val : data)
            {
                I bytes_of = *reinterpret_cast<I *>(&val);
                size_t idx = value_to_indices[bytes_of];
                K k_idx = *reinterpret_cast<K *>(&idx);
                index_buffer.push_back(k_idx);
            }

            transpose<K>(index_buffer, transposeBuffer);
            outBuffer.insert(outBuffer.end(), transposeBuffer.begin(), transposeBuffer.end());
            return outBuffer.size();
        }

        template <typename T, typename I>
        int encode_values(const tcb::span< const T> &data, buffer_t &transposeBuffer, buffer_t &outBuffer)
        {
            std::unordered_set<I> value_codes;
            for (T val : data)
            {
                I bytes_of = *reinterpret_cast<I *>(&val);
                value_codes.insert(bytes_of);
            }

            std::vector<I> value_codes_sorted = std::vector(value_codes.cbegin(), value_codes.cend());
            std::sort(value_codes_sorted.begin(), value_codes_sorted.end());

            auto n_values = value_codes_sorted.size();

            if (n_values <= pow(2, 8))
            {
                return encode_dictionary_indices<T, I, uint8_t>(data, value_codes_sorted, transposeBuffer, outBuffer);
            }
            else if (n_values <= pow(2, 16))
            {
                return encode_dictionary_indices<T, I, uint16_t>(data, value_codes_sorted, transposeBuffer, outBuffer);
            }
            else if (n_values <= pow(2, 32))
            {
                return encode_dictionary_indices<T, I, uint32_t>(data, value_codes_sorted, transposeBuffer, outBuffer);
            }
            else if (n_values <= pow(2, 64))
            {
                return encode_dictionary_indices<T, I, uint64_t>(data, value_codes_sorted, transposeBuffer, outBuffer);
            }
            else
            {
                throw runtime_error("Cannot encode a dictionary with more than 2 ** 64 distinct values");
            }
            return 0;
        }

        template <typename T>
        int dictionary_encode(const tcb::span<const T> &data, buffer_t &transposeBuffer, buffer_t &outBuffer)
        {
            if (sizeof(T) <= 1)
            {
                return encode_values<T, uint8_t>(data, transposeBuffer, outBuffer);
            }
            else if (sizeof(T) <= 2)
            {
                return encode_values<T, uint16_t>(data, transposeBuffer, outBuffer);
            }
            else if (sizeof(T) <= 4)
            {
                return encode_values<T, uint32_t>(data, transposeBuffer, outBuffer);
            }
            else if (sizeof(T) <= 8)
            {
                return encode_values<T, uint64_t>(data, transposeBuffer, outBuffer);
            }
            else
            {
                throw runtime_error("Cannot encode a dictionary with more values longer than 8 bytes");
            }
            return 0;
        }

        template <typename T, typename I>
        int decode_values(const buffer_t &data, size_t offset, size_t n_values, std::vector<T> &values)
        {
            auto start = data.begin() + 16;
            auto end = data.begin() + offset;
            byte_view<I> block;

            // buffer_span_t slice(start, end);
            buffer_span_t slice(data.data() + 16, offset - 16);
            std::vector<I> blocks;
            reverse_transpose(slice, blocks);

            auto i = 0;
            for (auto chunk : blocks)
            {
                T val = *reinterpret_cast<T *>(&chunk);
                values.push_back(val);
                i += 1;
            }
            return i;
        }

        template <typename T, typename K>
        int decode_indices(const buffer_t &data, size_t offset, std::vector<T> &values_lookup, std::vector<T> &values)
        {
            auto start = data.begin() + offset;
            auto end = data.end();
            byte_view<K> block;

            // buffer_span_t slice(start, end);
            buffer_span_t slice(data.data() + offset, data.size() - offset);
            std::vector<K> blocks;
            reverse_transpose<K>(slice, blocks);

            for (auto idx : blocks)
            {
                T val = values_lookup[idx];
                values.push_back(val);
            }
            return 0;
        }

        /// @brief Decode a dictionary-compressed byte buffer
        /// @tparam T The type being decoded
        /// @param data The dictionary-encoded data buffer
        /// @param outBuffer The buffer to decode elements into
        /// @return 0 if successful, otherwise an error
        template <typename T>
        int dictionary_decode(const buffer_t &data, std::vector<T> &outBuffer)
        {
            if (data.size() < 16)
            {
                if (data.empty())
                {
                    outBuffer.clear();
                    return 0;
                }
                throw runtime_error("Buffer less than 16 bytes long, invalid dictionary buffer");
            }

            byte_view<uint64_t> view;
            std::memcpy((void*)&view, data.data(), 8);
            if (is_big_endian())
            {
                view.byteswap();
            }
            auto offset = view.value();

            std::memcpy((void *)&view, data.data() + 8, 8);
            if (is_big_endian()) {
                view.byteswap();
            }
            auto n_values = view.value();


            if (data.size() < offset)
            {
                throw runtime_error("Buffer less than value offsets, invalid dictionary buffer");
            }

            if (n_values == 0)
            {
                outBuffer.clear();
                return 0;
            }

            auto value_size = (offset - 16) / n_values;

            std::vector<T> value_lookup;
            if (value_size == 1)
            {
                decode_values<T, uint8_t>(data, offset, n_values, value_lookup);
                if (n_values < pow(2, 8))
                {
                    decode_indices<T, uint8_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 16))
                {
                    decode_indices<T, uint16_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 32))
                {
                    decode_indices<T, uint32_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 64))
                {
                    decode_indices<T, uint64_t>(data, offset, value_lookup, outBuffer);
                }
                else
                {
                    throw runtime_error("Too many value indices!");
                }
            }
            else if (value_size == 2)
            {
                decode_values<T, uint16_t>(data, offset, n_values, value_lookup);
                if (n_values < pow(2, 8))
                {
                    decode_indices<T, uint8_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 16))
                {
                    decode_indices<T, uint16_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 32))
                {
                    decode_indices<T, uint32_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 64))
                {
                    decode_indices<T, uint64_t>(data, offset, value_lookup, outBuffer);
                }
                else
                {
                    throw runtime_error("Too many value indices!");
                }
            }
            else if (value_size == 4)
            {
                decode_values<T, uint32_t>(data, offset, n_values, value_lookup);
                if (n_values < pow(2, 8))
                {
                    decode_indices<T, uint8_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 16))
                {
                    decode_indices<T, uint16_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 32))
                {
                    decode_indices<T, uint32_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 64))
                {
                    decode_indices<T, uint64_t>(data, offset, value_lookup, outBuffer);
                }
                else
                {
                    throw runtime_error("Too many value indices!");
                }
            }
            else if (value_size == 8)
            {
                decode_values<T, uint64_t>(data, offset, n_values, value_lookup);
                if (n_values < pow(2, 8))
                {
                    decode_indices<T, uint8_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 16))
                {
                    decode_indices<T, uint16_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 32))
                {
                    decode_indices<T, uint32_t>(data, offset, value_lookup, outBuffer);
                }
                else if (n_values < pow(2, 64))
                {
                    decode_indices<T, uint64_t>(data, offset, value_lookup, outBuffer);
                }
                else
                {
                    throw runtime_error("Too many value indices!");
                }
            }
            else
            {
                throw runtime_error("Value size too large, value cannot be longer than 8 bytes");
            }

            return 0;
        }
    }

    /// @brief Compress an array of numerical data using byte shuffling and ZSTD compression. Data will be stored in little endian byte order.
    /// @tparam T The data type of the array to compress
    /// @param data The data array to compress
    /// @param transposeBuffer An intermediate byte buffer to shuffle bytes into
    /// @param outBuffer A byte buffer to write ZSTD-compressed bytes to
    /// @param level The ZSTD compression level
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t byteshuffle_compress_buffer(const std::vector<T> &data,
                                       buffer_t &transposeBuffer,
                                       buffer_t &outBuffer,
                                       int level = ZSTD_defaultCLevel())
    {
        const tcb::span<const T> view(data.data(), data.size());
        return byteshuffle_compress_buffer(view, transposeBuffer, outBuffer, level);
    }

    /// @brief Compress an array of numerical data using byte shuffling and ZSTD compression. Data will be stored in little endian byte order.
    /// @tparam T The data type of the array to compress
    /// @param data The data array to compress
    /// @param transposeBuffer An intermediate byte buffer to shuffle bytes into
    /// @param outBuffer A byte buffer to write ZSTD-compressed bytes to
    /// @param level The ZSTD compression level
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t byteshuffle_compress_buffer(const tcb::span<const T> &data,
                                       buffer_t &transposeBuffer,
                                       buffer_t &outBuffer,
                                       int level = ZSTD_defaultCLevel())
    {
        transposeBuffer.clear();
        inner::transpose<T>(data, transposeBuffer);
        auto outputBound = ZSTD_compressBound(transposeBuffer.size());
        if (ZSTD_isError(outputBound))
        {
            // ZSTD_ErrorCode errCode = ZSTD_getErrorCode(outputBound);
            // std::cout << "Zstd error: " << errCode << " " << string(ZSTD_getErrorName(errCode)) << " " << string(ZSTD_getErrorString(errCode)) << std::endl;
            return outputBound;
        }
        outBuffer.resize(outputBound);
        auto used = ZSTD_compress(
            (void *)outBuffer.data(),
            outputBound,
            (void *)transposeBuffer.data(),
            transposeBuffer.size(),
            level);
        if (ZSTD_isError(used))
        {
            ZSTD_ErrorCode errCode = ZSTD_getErrorCode(used);
            std::cout << "Zstd error: " << errCode << " " << string(ZSTD_getErrorName(errCode)) << " " << string(ZSTD_getErrorString(errCode)) << std::endl;
            return used;
        }
        outBuffer.resize(used);
        return 0;
    }

    /// @brief Compress an array of numerical data using byte shuffling and ZSTD compression
    /// @tparam T The data type of the array to compress
    /// @param data The data array to compress
    /// @param outBuffer A byte buffer to write ZSTD-compressed bytes to
    /// @param level The ZSTD compression level
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t byteshuffle_compress_buffer(const std::vector<T> &data,
                                       buffer_t &outBuffer,
                                       int level = ZSTD_defaultCLevel())
    {
        buffer_t transposeBuffer;
        return byteshuffle_compress_buffer(data, transposeBuffer, outBuffer, level);
    }

    /// @brief Decompress an array of numerical data using byte shuffling and ZSTD compression
    /// @tparam T The data type of the array to compress
    /// @param buffer A byte buffer to containing ZSTD-compressed bytes
    /// @param transposeBuffer An intermediate byte buffer to shuffle bytes into
    /// @param dataBuffer The data array to decompress into
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t byteshuffle_decompress_buffer(const buffer_span_t &buffer,
                                         buffer_t &transposeBuffer,
                                         std::vector<T> &dataBuffer)
    {
        if (buffer.empty())
        {
            dataBuffer.clear();
            return 0;
        }
        transposeBuffer.clear();
        auto outputBound = ZSTD_getFrameContentSize(buffer.data(), buffer.size());
        if (ZSTD_isError(outputBound))
        {
            return outputBound;
        }
        transposeBuffer.resize(outputBound);
        auto used = ZSTD_decompress(
            (void *)transposeBuffer.data(),
            outputBound,
            (void *)buffer.data(),
            buffer.size());
        if (ZSTD_isError(used))
        {
            return used;
        }
        transposeBuffer.resize(used);
        inner::reverse_transpose(transposeBuffer, dataBuffer);
        return 0;
    }

    /// @brief Compress an array of numerical data using delta encoding, byte shuffling and ZSTD compression
    /// @tparam T The data type of the array to compress
    /// @param data The data array to compress. *This array will be mutated with delta encoding*
    /// @param transposeBuffer An intermediate byte buffer to shuffle bytes into
    /// @param outBuffer A byte buffer to write ZSTD-compressed bytes to
    /// @param level The ZSTD compression level
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t delta_compress_buffer(
        std::vector<T> &data,
        buffer_t &transposeBuffer,
        buffer_t &outBuffer,
        int level = ZSTD_defaultCLevel())
    {
        inner::delta_encode(data);
        return byteshuffle_compress_buffer(data, transposeBuffer, outBuffer);
    }

    /// @brief Decompress an array of numerical data using delta encoding, byte shuffling and ZSTD compression
    /// @tparam T The data type of the array to compress
    /// @param buffer A byte buffer to containing ZSTD-compressed bytes
    /// @param transposeBuffer An intermediate byte buffer to shuffle bytes into
    /// @param dataBuffer The data array to decompress into
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t delta_decompress_buffer(
        const buffer_span_t &buffer,
        buffer_t &transposeBuffer,
        std::vector<T> &dataBuffer)
    {
        auto z = byteshuffle_decompress_buffer(buffer, transposeBuffer, dataBuffer);
        inner::delta_decode(dataBuffer);
        return z;
    }

    /// @brief Compress an array of numerical data using dictionary encoding and ZSTD compression. Data will be stored in little-endian byte order
    /// @tparam T The data type of the array to compress
    /// @param data The data array to compress
    /// @param dictBuffer An intermediate byte buffer to hold the dictionary encoded bytes in
    /// @param transposeBuffer An intermediate byte buffer to hold the intermediate shuffled bytes in
    /// @param outBuffer A byte buffer to write ZSTD-compressed bytes to
    /// @param level The ZSTD compression level
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t dict_compress_buffer(
        const std::vector<T> &data,
        buffer_t &dictBuffer,
        buffer_t &transposeBuffer,
        buffer_t &outBuffer,
        int level = ZSTD_defaultCLevel())
    {
        tcb::span<const T> view(data.data(), data.size());
        return dict_compress_buffer(view, dictBuffer, transposeBuffer, outBuffer);
    }

    /// @brief Compress an array of numerical data using dictionary encoding and ZSTD compression. Data will be stored in little-endian byte order
    /// @tparam T The data type of the array to compress
    /// @param data The data array to compress
    /// @param dictBuffer An intermediate byte buffer to hold the dictionary encoded bytes in
    /// @param transposeBuffer An intermediate byte buffer to hold the intermediate shuffled bytes in
    /// @param outBuffer A byte buffer to write ZSTD-compressed bytes to
    /// @param level The ZSTD compression level
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t dict_compress_buffer(
        const tcb::span<const T> &data,
        buffer_t &dictBuffer,
        buffer_t &transposeBuffer,
        buffer_t &outBuffer,
        int level = ZSTD_defaultCLevel())
    {
        dictBuffer.clear();
        dict::dictionary_encode<T>(data, transposeBuffer, dictBuffer);
        dictBuffer.shrink_to_fit();

        auto outputBound = ZSTD_compressBound(dictBuffer.size());
        if (ZSTD_isError(outputBound)) {
            return outputBound;
        }
        outBuffer.resize(outputBound);
        auto used = ZSTD_compress(
            (void *)outBuffer.data(),
            outputBound,
            (void *)dictBuffer.data(),
            dictBuffer.size(),
            level);
        if (ZSTD_isError(used))
        {
            return used;
        }
        outBuffer.resize(used);
        return 0;
    }

    template <typename T>
    size_t dict_compress_buffer(
            const std::vector<T> &data,
            buffer_t &dictBuffer,
            buffer_t &outBuffer,
            int level = ZSTD_defaultCLevel())
    {
        buffer_t transposeBuffer;
        return dict_compress_buffer(data, dictBuffer, transposeBuffer, outBuffer, level);
    }

    /// @brief Decompress an array of numerical data using dictionary encoding and ZSTD compression
    /// @tparam T The data type of the array to compress
    /// @param buffer A byte buffer containing ZSTD-compressed bytes
    /// @param dictBuffer An intermediate byte buffer to hold the dictionary encoded bytes
    /// @param dataBuffer The data array to decompress into
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t dict_decompress_buffer(
        const buffer_span_t &buffer,
        buffer_t &dictBuffer,
        std::vector<T> &dataBuffer)
    {
        if (buffer.empty())
        {
            dataBuffer.clear();
            return 0;
        }
        dictBuffer.clear();
        auto outputBound = ZSTD_getFrameContentSize(buffer.data(), buffer.size());
        if (ZSTD_isError(outputBound))
        {
            // ZSTD_ErrorCode errCode = ZSTD_getErrorCode(outputBound);
            // std::cout << "Zstd error: " << errCode << " " << string(ZSTD_getErrorName(errCode)) << " " << string(ZSTD_getErrorString(errCode)) << std::endl;
            return outputBound;
        }
        dictBuffer.resize(outputBound);
        auto used = ZSTD_decompress(
            (void *)dictBuffer.data(),
            outputBound,
            (void *)buffer.data(),
            buffer.size());
        if (ZSTD_isError(used))
        {
            // ZSTD_ErrorCode errCode = ZSTD_getErrorCode(used);
            // std::cout << "Zstd error: " << errCode << " " << string(ZSTD_getErrorName(errCode)) << " " << string(ZSTD_getErrorString(errCode)) << std::endl;
            return used;
        }
        dataBuffer.clear();
        return dict::dictionary_decode(
            dictBuffer,
            dataBuffer);
    }

    /// @brief Compress an array of numerical data using ZSTD compression. Data will be stored in little-endian byte order
    /// @tparam T The data type of the array to compress
    /// @param data The data array to compress
    /// @param outBuffer The byte buffer to compress into
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t compress_buffer(const tcb::span<const T> &data,
                           buffer_t &outBuffer,
                           int level = ZSTD_defaultCLevel()) {
        auto outputBound = ZSTD_compressBound(data.size() * sizeof(T));
        if (ZSTD_isError(outputBound))
        {
            // ZSTD_ErrorCode errCode = ZSTD_getErrorCode(outputBound);
            // std::cout << "Zstd error: " << errCode << " " << string(ZSTD_getErrorName(errCode)) << " " << string(ZSTD_getErrorString(errCode)) << std::endl;
            return outputBound;
        }
        outBuffer.resize(outputBound);
        if (binary::is_big_endian() && sizeof(T) > 1)
        {
            buffer_t revEndian;
            revEndian.reserve(data.size() * sizeof(T));
            for (size_t i = 0; i < data.size(); i++)
            {
                const T val = data[i];
                binary::byte_view<T> view = binary::byte_view<T>::as_little_endian(val);
                std::copy(view.begin(), view.end(), std::back_inserter(revEndian));
            }
            auto used = ZSTD_compress(
                (void *)outBuffer.data(),
                outputBound,
                (void *)revEndian.data(),
                revEndian.size(),
                level);
            if (ZSTD_isError(used))
            {
                // ZSTD_ErrorCode errCode = ZSTD_getErrorCode(used);
                // std::cout << "Zstd error: " << errCode << " " << string(ZSTD_getErrorName(errCode)) << " " << string(ZSTD_getErrorString(errCode)) << std::endl;
                return used;
            }
            outBuffer.resize(used);
        }
        else
        {
            auto used = ZSTD_compress(
                (void *)outBuffer.data(),
                outputBound,
                (void *)data.data(),
                data.size() * sizeof(T),
                level);
            if (ZSTD_isError(used))
            {
                ZSTD_ErrorCode errCode = ZSTD_getErrorCode(used);
                // std::cout << "Zstd error: " << errCode << " " << string(ZSTD_getErrorName(errCode)) << " " << string(ZSTD_getErrorString(errCode)) << std::endl;
                return used;
            }
            outBuffer.resize(used);
        }
        return 0;
    }

    /// @brief Decompress Zstd-compressed data back into it's native format.
    /// @tparam T The data type of the array to decompress to
    /// @param buffer A byte buffer containing containing little endian ZSTD-compressed bytes
    /// @param dataBuffer The data array to decompress into. Data will be in native byte ordering
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t decompress_buffer(const buffer_span_t &buffer, std::vector<T> &dataBuffer)
    {
        if (buffer.empty())
        {
            dataBuffer.clear();
            return 0;
        }
        auto outputBound = ZSTD_getFrameContentSize(buffer.data(), buffer.size());
        if (ZSTD_isError(outputBound))
        {
            // ZSTD_ErrorCode errCode = ZSTD_getErrorCode(outputBound);
            // std::cout << "Zstd error: " << errCode << " " << string(ZSTD_getErrorName(errCode)) << " " << string(ZSTD_getErrorString(errCode)) << std::endl;
            return outputBound;
        }
        dataBuffer.resize(outputBound / sizeof(T));
        auto used = ZSTD_decompress(
            (void *)dataBuffer.data(),
            outputBound,
            (void *)buffer.data(),
            buffer.size());
        if (ZSTD_isError(used))
        {
            // ZSTD_ErrorCode errCode = ZSTD_getErrorCode(used);
            // std::cout << "Zstd error: " << errCode << " " << string(ZSTD_getErrorName(errCode)) << " " << string(ZSTD_getErrorString(errCode)) << std::endl;
            return used;
        }
        dataBuffer.resize(used / sizeof(T));
        if (binary::is_big_endian() && sizeof(T) > 1)
        {
            for (size_t i = 0; i < dataBuffer.size(); i++)
            {
                T val = dataBuffer[i];
                binary::byte_view<T> view(val);
                view.byteswap();
                dataBuffer[i] = view.value();
            }
        }
        return 0;
    }

    /// @brief Compress an array of numerical data using ZSTD compression. Data will be stored in little-endian byte order.
    /// @tparam T The data type of the array to compress
    /// @param data The data array to compress
    /// @param outBuffer The byte buffer to compress into
    /// @return 0 if successful, some other value corresponding to a ZSTD error code otherwise
    template <typename T>
    size_t compress_buffer(const std::vector<const T> &data,
                           buffer_t &outBuffer,
                           int level = ZSTD_defaultCLevel())
    {
        const tcb::span<const T> view(data.data(), data.size());
        return compress_buffer(view, outBuffer, level);
    }

}