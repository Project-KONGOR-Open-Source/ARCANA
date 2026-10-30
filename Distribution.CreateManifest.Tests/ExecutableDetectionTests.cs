using System.Buffers.Binary;
using System.Text;

namespace Distribution.CreateManifest.Tests;

/// <summary>
///     Verifies which files the manifest flags as executable: Windows programs by name, scripts by their shebang, and Linux programs by their ELF header.
/// </summary>
public sealed class ExecutableDetectionTests
{
    private const ushort ExecutableType = 2;
    private const ushort SharedObjectType = 3;
    private const uint LoadableSegmentType = 1;
    private const uint InterpreterSegmentType = 3;

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static string WriteFile(string directory, string name, byte[] contents)
    {
        string path = Path.Combine(directory, name);

        File.WriteAllBytes(path, contents);

        return path;
    }

    /// <summary>
    ///     Builds a 64-bit little-endian ELF header followed by one program header, as a Linux program or shared library starts.
    /// </summary>
    private static byte[] CreateELF(ushort type, uint segmentType, ulong programHeaderOffset = 64)
    {
        byte[] contents = new byte[64 + 56];

        contents[0] = 0x7F;
        contents[1] = (byte) 'E';
        contents[2] = (byte) 'L';
        contents[3] = (byte) 'F';
        contents[4] = 2;
        contents[5] = 1;
        contents[6] = 1;

        BinaryPrimitives.WriteUInt16LittleEndian(contents.AsSpan(16), type);
        BinaryPrimitives.WriteUInt64LittleEndian(contents.AsSpan(32), programHeaderOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(contents.AsSpan(54), 56);
        BinaryPrimitives.WriteUInt16LittleEndian(contents.AsSpan(56), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(contents.AsSpan(64), segmentType);

        return contents;
    }

    [Test]
    public async Task An_EXE_File_Is_Executable_Whatever_Its_Contents()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await Assert.That(ExecutableDetection.IsExecutable(WriteFile(directory, "hon.exe", Encoding.UTF8.GetBytes("text")))).IsTrue();
            await Assert.That(ExecutableDetection.IsExecutable(WriteFile(directory, "HON_CHECK.EXE", Encoding.UTF8.GetBytes("text")))).IsTrue();
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task An_ELF_Executable_Is_Executable()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await Assert.That(ExecutableDetection.IsExecutable(WriteFile(directory, "hon", CreateELF(ExecutableType, LoadableSegmentType)))).IsTrue();
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task A_Position_Independent_ELF_With_An_Interpreter_Is_Executable()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await Assert.That(ExecutableDetection.IsExecutable(WriteFile(directory, "hon_check", CreateELF(SharedObjectType, InterpreterSegmentType)))).IsTrue();
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task An_ELF_Shared_Library_Is_Not_Executable()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await Assert.That(ExecutableDetection.IsExecutable(WriteFile(directory, "libk2.so", CreateELF(SharedObjectType, LoadableSegmentType)))).IsFalse();
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task A_Script_With_A_Shebang_Is_Executable()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await Assert.That(ExecutableDetection.IsExecutable(WriteFile(directory, "Server-Console.sh", Encoding.UTF8.GetBytes("#!/usr/bin/env bash\n")))).IsTrue();
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task A_Text_File_Is_Not_Executable()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await Assert.That(ExecutableDetection.IsExecutable(WriteFile(directory, "readme.txt", Encoding.UTF8.GetBytes("Heroes Of Newerth\n")))).IsFalse();
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task A_Short_Or_Malformed_File_Is_Not_Executable()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            byte[] truncatedHeader = CreateELF(ExecutableType, LoadableSegmentType)[..20];
            byte[] programHeadersPastTheEnd = CreateELF(SharedObjectType, InterpreterSegmentType, programHeaderOffset: 4096);

            string[] paths =
            [
                WriteFile(directory, "empty", []),
                WriteFile(directory, "hash", "#"u8.ToArray()),
                WriteFile(directory, "truncated", truncatedHeader),
                WriteFile(directory, "past-the-end", programHeadersPastTheEnd)
            ];

            foreach (string path in paths)
                await Assert.That(ExecutableDetection.IsExecutable(path)).IsFalse();
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
