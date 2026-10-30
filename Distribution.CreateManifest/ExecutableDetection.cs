using System.Buffers.Binary;

namespace Distribution.CreateManifest;

/// <summary>
///     Decides whether a distribution file is a program, which the launchers give the execute permission on Linux.
///     A file is a program when its name ends in <c>.exe</c>, when it starts with a shebang, or when it is a 64-bit little-endian ELF executable, or a position-independent one that names an interpreter.
/// </summary>
internal static class ExecutableDetection
{
    private const int ELFHeaderLength = 64;
    private const ushort ExecutableType = 2;
    private const ushort SharedObjectType = 3;
    private const uint InterpreterSegmentType = 3;

    private static ReadOnlySpan<byte> ELFMagic => [0x7F, (byte) 'E', (byte) 'L', (byte) 'F'];

    internal static bool IsExecutable(string path)
    {
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return true;

        using FileStream stream = File.OpenRead(path);

        Span<byte> buffer = stackalloc byte[ELFHeaderLength];

        Span<byte> header = buffer[..stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false)];

        if (header.Length >= 2 && header[0] == (byte) '#' && header[1] == (byte) '!')
            return true;

        return IsELFProgram(stream, header);
    }

    private static bool IsELFProgram(FileStream stream, ReadOnlySpan<byte> header)
    {
        // The Linux Distributions Ship Only 64-Bit Little-Endian Binaries
        if (header.Length < ELFHeaderLength || header[..4].SequenceEqual(ELFMagic) is false || header[4] is not 2 || header[5] is not 1)
            return false;

        ushort type = BinaryPrimitives.ReadUInt16LittleEndian(header[16..]);

        if (type is ExecutableType)
            return true;

        if (type is not SharedObjectType)
            return false;

        // A Position-Independent Program And A Shared Library Share A Type; Only The Program Names An Interpreter Among Its Program Headers
        ulong programHeaderOffset = BinaryPrimitives.ReadUInt64LittleEndian(header[32..]);
        ushort programHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(header[54..]);
        ushort programHeaderCount = BinaryPrimitives.ReadUInt16LittleEndian(header[56..]);

        if (programHeaderSize < sizeof(uint) || programHeaderOffset > (ulong) stream.Length)
            return false;

        Span<byte> segmentType = stackalloc byte[sizeof(uint)];

        for (ulong index = 0; index < programHeaderCount; index++)
        {
            ulong offset = programHeaderOffset + index * programHeaderSize;

            if (offset > (ulong) stream.Length - sizeof(uint))
                return false;

            stream.Position = (long) offset;
            stream.ReadExactly(segmentType);

            if (BinaryPrimitives.ReadUInt32LittleEndian(segmentType) is InterpreterSegmentType)
                return true;
        }

        return false;
    }
}
