using System.Buffers.Binary;

namespace OFS.Sdk;

internal sealed record DecodedWaveAudio(
    ModWaveEncoding Encoding,
    int Channels,
    int Frequency,
    int BitsPerSample,
    int SampleFrames,
    float[] Samples)
{
    internal double DurationSeconds => (double)SampleFrames / Frequency;
}

internal static class WaveAudioDecoder
{
    private const ushort Pcm = 0x0001;
    private const ushort IeeeFloat = 0x0003;
    private const ushort Extensible = 0xFFFE;

    internal static DecodedWaveAudio Decode(ReadOnlySpan<byte> source)
    {
        if (source.Length < 12 || !source[..4].SequenceEqual("RIFF"u8) ||
            !source.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Audio source must be a RIFF/WAVE file.");

        var riffBytes = checked((long)ReadUInt32(source, 4) + 8L);
        if (riffBytes is < 12 or > int.MaxValue || riffBytes > source.Length)
            throw new InvalidDataException("WAV RIFF size exceeds the available source bytes.");
        source = source[..(int)riffBytes];

        ReadOnlySpan<byte> format = default;
        ReadOnlySpan<byte> data = default;
        var cursor = 12;
        while (cursor <= source.Length - 8)
        {
            var size = ReadUInt32(source, cursor + 4);
            var payload = cursor + 8;
            var end = checked((long)payload + size);
            if (end > source.Length)
                throw new InvalidDataException("WAV chunk exceeds the available source bytes.");
            var chunk = source.Slice(payload, checked((int)size));
            if (source.Slice(cursor, 4).SequenceEqual("fmt "u8) && format.IsEmpty)
                format = chunk;
            else if (source.Slice(cursor, 4).SequenceEqual("data"u8) && data.IsEmpty)
                data = chunk;
            cursor = checked((int)(end + (size & 1u)));
        }

        if (format.IsEmpty || format.Length < 16)
            throw new InvalidDataException("WAV has no valid fmt chunk.");
        if (data.IsEmpty)
            throw new InvalidDataException("WAV has no non-empty data chunk.");

        var formatTag = ReadUInt16(format, 0);
        var channels = ReadUInt16(format, 2);
        var frequency = ReadInt32(format, 4);
        var byteRate = ReadInt32(format, 8);
        var blockAlign = ReadUInt16(format, 12);
        var bits = ReadUInt16(format, 14);
        if (formatTag == Extensible)
        {
            if (format.Length < 40 || ReadUInt16(format, 16) < 22)
                throw new InvalidDataException("WAVE_FORMAT_EXTENSIBLE fmt chunk is incomplete.");
            formatTag = ReadUInt16(format, 24);
            if (!HasStandardWaveSubformatTail(format.Slice(26, 14)))
                throw new InvalidDataException("WAV extensible subformat GUID is unsupported.");
        }

        var encoding = formatTag switch
        {
            Pcm => ModWaveEncoding.PcmInteger,
            IeeeFloat => ModWaveEncoding.IeeeFloat,
            _ => throw new NotSupportedException(
                $"WAV format 0x{formatTag:X4} is unsupported; use PCM integer or IEEE float."),
        };
        if (channels is 0 or > ModAudioLimits.MaximumChannels)
            throw new InvalidDataException(
                $"WAV channel count {channels} is outside 1..{ModAudioLimits.MaximumChannels}.");
        if (frequency is < 8_000 or > ModAudioLimits.MaximumFrequency)
            throw new InvalidDataException(
                $"WAV frequency {frequency} is outside 8000..{ModAudioLimits.MaximumFrequency} Hz.");
        if ((encoding == ModWaveEncoding.PcmInteger && bits is not (8 or 16 or 24 or 32)) ||
            (encoding == ModWaveEncoding.IeeeFloat && bits != 32))
            throw new NotSupportedException(
                $"WAV {encoding} with {bits} bits per sample is unsupported.");

        var bytesPerSample = bits / 8;
        var expectedBlockAlign = checked(channels * bytesPerSample);
        if (blockAlign != expectedBlockAlign || byteRate != frequency * blockAlign)
            throw new InvalidDataException("WAV block alignment or byte rate is inconsistent.");
        if (data.Length % blockAlign != 0)
            throw new InvalidDataException("WAV data is not aligned to complete sample frames.");

        var frames = data.Length / blockAlign;
        var sampleValues = checked(frames * channels);
        if (sampleValues <= 0 || sampleValues > ModAudioLimits.MaximumSampleValues)
            throw new InvalidDataException(
                $"WAV expands to {sampleValues} samples; limit is " +
                $"{ModAudioLimits.MaximumSampleValues}.");
        var duration = (double)frames / frequency;
        if (duration > ModAudioLimits.MaximumDurationSeconds)
            throw new InvalidDataException(
                $"WAV duration {duration:F3}s exceeds {ModAudioLimits.MaximumDurationSeconds:F0}s.");

        var samples = new float[sampleValues];
        for (var index = 0; index < sampleValues; ++index)
        {
            var offset = index * bytesPerSample;
            samples[index] = encoding == ModWaveEncoding.IeeeFloat
                ? DecodeFloat(data, offset)
                : DecodePcm(data, offset, bits);
        }
        return new DecodedWaveAudio(
            encoding, channels, frequency, bits, frames, samples);
    }

    private static float DecodePcm(ReadOnlySpan<byte> data, int offset, int bits) => bits switch
    {
        8 => (data[offset] - 128) / 128f,
        16 => BinaryPrimitives.ReadInt16LittleEndian(data[offset..]) / 32768f,
        24 => DecodeInt24(data, offset) / 8_388_608f,
        32 => BinaryPrimitives.ReadInt32LittleEndian(data[offset..]) / 2_147_483_648f,
        _ => throw new InvalidOperationException("Unsupported PCM width reached the decoder."),
    };

    private static int DecodeInt24(ReadOnlySpan<byte> data, int offset)
    {
        var value = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        return (value & 0x0080_0000) != 0 ? value | unchecked((int)0xFF00_0000) : value;
    }

    private static float DecodeFloat(ReadOnlySpan<byte> data, int offset)
    {
        var bits = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
        var value = BitConverter.Int32BitsToSingle(bits);
        if (!float.IsFinite(value))
            throw new InvalidDataException("WAV contains a non-finite IEEE float sample.");
        return Math.Clamp(value, -1f, 1f);
    }

    private static bool HasStandardWaveSubformatTail(ReadOnlySpan<byte> tail) =>
        tail.SequenceEqual(
        new byte[]
        {
            0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80,
            0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71,
        });

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(source[offset..]);

    private static uint ReadUInt32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);

    private static int ReadInt32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(source[offset..]);
}
