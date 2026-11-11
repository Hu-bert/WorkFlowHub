using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FlowHub.Infrastructure.Terminal;

/// <summary>
/// Proces uruchomiony w pseudokonsoli Windows (ConPTY) — program „myśli”, że działa w prawdziwym terminalu,
/// a my czytamy jego ekran jako strumień bajtów (sekwencje VT) i piszemy mu na klawiaturę.
/// Dzięki temu interaktywny Copilot CLI działa w przeglądarce (xterm.js).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PseudoConsoleProcess : IDisposable
{
    private readonly Lock _gate = new();
    private IntPtr _console;
    private IntPtr _attributeList;
    private IntPtr _process;
    private readonly int _processId;

    private PseudoConsoleProcess(IntPtr console, IntPtr attributeList, Native.ProcessInformation process, SafeFileHandle output, SafeFileHandle input)
    {
        _console = console;
        _attributeList = attributeList;
        _process = process.Process;
        _processId = process.ProcessId;
        Output = new FileStream(output, FileAccess.Read, bufferSize: 1, isAsync: false);
        Input = new FileStream(input, FileAccess.Write, bufferSize: 1, isAsync: false);
    }

    /// <summary>Ekran programu (sekwencje VT, UTF-8). Koniec strumienia = konsola zamknięta.</summary>
    public Stream Output { get; }

    /// <summary>Klawiatura programu.</summary>
    public Stream Input { get; }

    public static PseudoConsoleProcess Start(string fileName, IEnumerable<string> arguments, string workingDirectory, int columns, int rows)
    {
        Check(Native.CreatePipe(out var inputRead, out var inputWrite, IntPtr.Zero, 0));
        Check(Native.CreatePipe(out var outputRead, out var outputWrite, IntPtr.Zero, 0));

        var console = IntPtr.Zero;
        var attributeList = IntPtr.Zero;

        try
        {
            var result = Native.CreatePseudoConsole(Size(columns, rows), inputRead, outputWrite, 0, out console);
            if (result != 0)
            {
                throw new Win32Exception(result, "Nie udało się utworzyć pseudokonsoli (wymagany Windows 10 1809 lub nowszy).");
            }

            attributeList = AttributeListFor(console);

            var startup = new Native.StartupInfoEx { AttributeList = attributeList };
            startup.StartupInfo.Size = Marshal.SizeOf<Native.StartupInfoEx>();

            // Bez tego dziecko odziedziczyłoby przekierowane wyjście naszego procesu i ominęło pseudokonsolę.
            startup.StartupInfo.Flags = Native.StartfUseStdHandles;

            var commandLine = new StringBuilder(CommandLine(fileName, arguments));
            Check(Native.CreateProcessW(
                null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                Native.ExtendedStartupInfoPresent | Native.CreateUnicodeEnvironment,
                IntPtr.Zero, workingDirectory, ref startup, out var process));

            Native.CloseHandle(process.Thread);
            return new PseudoConsoleProcess(console, attributeList, process, outputRead, inputWrite);
        }
        catch
        {
            if (console != IntPtr.Zero)
            {
                Native.ClosePseudoConsole(console);
            }

            Free(attributeList);
            outputRead.Dispose();
            inputWrite.Dispose();
            throw;
        }
        finally
        {
            // Końcówki po stronie konsoli należą już do niej — nasze kopie zamykamy, żeby dostać koniec strumienia.
            inputRead.Dispose();
            outputWrite.Dispose();
        }
    }

    public void Resize(int columns, int rows)
    {
        lock (_gate)
        {
            if (_console != IntPtr.Zero)
            {
                Native.ResizePseudoConsole(_console, Size(columns, rows));
            }
        }
    }

    public async Task WaitForExitAsync(CancellationToken ct)
    {
        try
        {
            using var process = Process.GetProcessById(_processId);
            await process.WaitForExitAsync(ct);
        }
        catch (ArgumentException)
        {
            // Proces już się zakończył.
        }
    }

    /// <summary>
    /// Kończy program i zamyka konsolę; <see cref="Output"/> dostaje wtedy koniec strumienia.
    /// Ktoś musi w tym czasie nadal czytać Output — starsze Windowsy czekają z zamknięciem na opróżnienie bufora.
    /// </summary>
    public void Close()
    {
        lock (_gate)
        {
            if (_process != IntPtr.Zero)
            {
                Native.TerminateProcess(_process, 1);
                Native.CloseHandle(_process);
                _process = IntPtr.Zero;
            }

            if (_console != IntPtr.Zero)
            {
                Native.ClosePseudoConsole(_console);
                _console = IntPtr.Zero;
            }

            Free(_attributeList);
            _attributeList = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        Close();
        Input.Dispose();
        Output.Dispose();
    }

    /// <summary>Linia poleceń wg reguł Windows (cudzysłowy i ukośniki) — żadnej powłoki po drodze.</summary>
    public static string CommandLine(string fileName, IEnumerable<string> arguments) =>
        string.Join(' ', arguments.Prepend(fileName).Select(Quote));

    private static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"']) < 0)
        {
            return argument;
        }

        var quoted = new StringBuilder("\"");
        var backslashes = 0;

        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            quoted.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes).Append(character);
            backslashes = 0;
        }

        return quoted.Append('\\', backslashes * 2).Append('"').ToString();
    }

    private static IntPtr AttributeListFor(IntPtr console)
    {
        var size = IntPtr.Zero;
        Native.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);

        var list = Marshal.AllocHGlobal(size);
        try
        {
            Check(Native.InitializeProcThreadAttributeList(list, 1, 0, ref size));
            Check(Native.UpdateProcThreadAttribute(
                list, 0, Native.ProcThreadAttributePseudoConsole, console, IntPtr.Size, IntPtr.Zero, IntPtr.Zero));
            return list;
        }
        catch
        {
            Marshal.FreeHGlobal(list);
            throw;
        }
    }

    private static void Free(IntPtr attributeList)
    {
        if (attributeList != IntPtr.Zero)
        {
            Native.DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(attributeList);
        }
    }

    private static Native.Coord Size(int columns, int rows) => new()
    {
        X = (short)Math.Clamp(columns, 20, 500),
        Y = (short)Math.Clamp(rows, 5, 200),
    };

    private static void Check(bool succeeded)
    {
        if (!succeeded)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static class Native
    {
        public const uint ExtendedStartupInfoPresent = 0x00080000;
        public const uint CreateUnicodeEnvironment = 0x00000400;
        public const int StartfUseStdHandles = 0x00000100;
        public static readonly IntPtr ProcThreadAttributePseudoConsole = 0x00020016;

        [StructLayout(LayoutKind.Sequential)]
        public struct Coord
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct StartupInfo
        {
            public int Size;
            public string? Reserved;
            public string? Desktop;
            public string? Title;
            public int X;
            public int Y;
            public int XSize;
            public int YSize;
            public int XCountChars;
            public int YCountChars;
            public int FillAttribute;
            public int Flags;
            public short ShowWindow;
            public short Reserved2;
            public IntPtr Reserved3;
            public IntPtr StdInput;
            public IntPtr StdOutput;
            public IntPtr StdError;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct StartupInfoEx
        {
            public StartupInfo StartupInfo;
            public IntPtr AttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public int ProcessId;
            public int ThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreatePipe(out SafeFileHandle readPipe, out SafeFileHandle writePipe, IntPtr attributes, int size);

        [DllImport("kernel32.dll")]
        public static extern int CreatePseudoConsole(Coord size, SafeFileHandle input, SafeFileHandle output, uint flags, out IntPtr console);

        [DllImport("kernel32.dll")]
        public static extern int ResizePseudoConsole(IntPtr console, Coord size);

        [DllImport("kernel32.dll")]
        public static extern void ClosePseudoConsole(IntPtr console);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateProcThreadAttribute(
            IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previousValue, IntPtr returnSize);

        [DllImport("kernel32.dll")]
        public static extern void DeleteProcThreadAttributeList(IntPtr list);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcessW(
            string? applicationName,
            StringBuilder commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref StartupInfoEx startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TerminateProcess(IntPtr process, uint exitCode);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);
    }
}
