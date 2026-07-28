using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MyCodex.Cdp;

public sealed record PipeLaunchedProcess(
    Process Process,
    Stream BrowserOutput,
    Stream BrowserInput) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await BrowserOutput.DisposeAsync().ConfigureAwait(false);
        await BrowserInput.DisposeAsync().ConfigureAwait(false);
        Process.Dispose();
    }
}

// Launches Chromium with exactly two inherited DevTools pipe handles.
public static class WindowsPipeProcessLauncher
{
    private const uint HandleFlagInherit = 0x00000001;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateSuspended = 0x00000004;
    private const uint DuplicateSameAccess = 0x00000002;
    private const uint ResumeThreadFailed = 0xFFFFFFFF;
    private static readonly nuint ProcThreadAttributeHandleList = 0x00020002;

    public static PipeLaunchedProcess Launch(
        string executablePath,
        string workingDirectory,
        IReadOnlyList<string> arguments)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("CDP pipe launch requires Windows.");
        }
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "Desktop executable was not found.",
                executablePath);
        }

        CreateInheritablePipe(
            out var browserRead,
            out var hostWrite,
            hostEndIsRead: false);
        CreateInheritablePipe(
            out var browserWrite,
            out var hostRead,
            hostEndIsRead: true);

        var attributeList = IntPtr.Zero;
        var handleArray = IntPtr.Zero;
        Process? launchedProcess = null;
        var processInfo = default(ProcessInformation);
        try
        {
            nuint attributeSize = 0;
            InitializeProcThreadAttributeList(
                IntPtr.Zero,
                1,
                0,
                ref attributeSize);
            attributeList = Marshal.AllocHGlobal(checked((int)attributeSize));
            if (!InitializeProcThreadAttributeList(
                    attributeList,
                    1,
                    0,
                    ref attributeSize))
            {
                ThrowLastError("InitializeProcThreadAttributeList");
            }

            handleArray = Marshal.AllocHGlobal(IntPtr.Size * 2);
            Marshal.WriteIntPtr(handleArray, 0, browserRead.DangerousGetHandle());
            Marshal.WriteIntPtr(handleArray, IntPtr.Size, browserWrite.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ProcThreadAttributeHandleList,
                    handleArray,
                    (nuint)(IntPtr.Size * 2),
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                ThrowLastError("UpdateProcThreadAttribute");
            }

            var allArguments = arguments.Concat(
                new[]
                {
                    $"--remote-debugging-io-pipes=" +
                    $"{SerializeHandle(browserRead)}," +
                    $"{SerializeHandle(browserWrite)}"
                });
            var commandLine = new StringBuilder(
                string.Join(
                    " ",
                    new[] { Quote(executablePath) }.Concat(allArguments.Select(Quote))));
            var startup = new StartupInfoEx
            {
                StartupInfo = new StartupInfo
                {
                    Cb = Marshal.SizeOf<StartupInfoEx>()
                },
                AttributeList = attributeList
            };
            if (!CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,
                    ExtendedStartupInfoPresent | CreateSuspended,
                    IntPtr.Zero,
                    workingDirectory,
                    ref startup,
                    out processInfo))
            {
                ThrowLastError("CreateProcessW");
            }
            launchedProcess = Process.GetProcessById(checked((int)processInfo.ProcessId));

            // Chromium intentionally closes the browser when its DevTools pipe
            // observes EOF. Non-inheritable duplicates owned by the exact root
            // keep both peer endpoints alive after MyCodex disconnects, while
            // still closing naturally when that Codex root exits.
            DuplicateIntoProcess(hostRead, processInfo.Process);
            DuplicateIntoProcess(hostWrite, processInfo.Process);
            if (ResumeThread(processInfo.Thread) == ResumeThreadFailed)
            {
                ThrowLastError("ResumeThread");
            }

            CloseHandle(processInfo.Thread);
            processInfo.Thread = IntPtr.Zero;
            CloseHandle(processInfo.Process);
            processInfo.Process = IntPtr.Zero;
            browserRead.Dispose();
            browserWrite.Dispose();

            return new PipeLaunchedProcess(
                launchedProcess,
                new FileStream(hostRead, FileAccess.Read, 64 * 1024, isAsync: false),
                new FileStream(hostWrite, FileAccess.Write, 64 * 1024, isAsync: false));
        }
        catch
        {
            if (launchedProcess is { HasExited: false })
            {
                launchedProcess.Kill(entireProcessTree: true);
                launchedProcess.WaitForExit(10_000);
            }
            launchedProcess?.Dispose();
            browserRead.Dispose();
            browserWrite.Dispose();
            hostRead.Dispose();
            hostWrite.Dispose();
            throw;
        }
        finally
        {
            if (processInfo.Thread != IntPtr.Zero)
            {
                CloseHandle(processInfo.Thread);
            }
            if (processInfo.Process != IntPtr.Zero)
            {
                CloseHandle(processInfo.Process);
            }
            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
            if (handleArray != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(handleArray);
            }
        }
    }

    private static void DuplicateIntoProcess(
        SafeFileHandle sourceHandle,
        IntPtr targetProcess)
    {
        if (!DuplicateHandle(
                GetCurrentProcess(),
                sourceHandle.DangerousGetHandle(),
                targetProcess,
                out _,
                0,
                false,
                DuplicateSameAccess))
        {
            ThrowLastError("DuplicateHandle");
        }
    }

    private static void CreateInheritablePipe(
        out SafeFileHandle childEnd,
        out SafeFileHandle hostEnd,
        bool hostEndIsRead)
    {
        var security = new SecurityAttributes
        {
            Length = Marshal.SizeOf<SecurityAttributes>(),
            InheritHandle = true
        };
        if (!CreatePipe(out var read, out var write, ref security, 0))
        {
            ThrowLastError("CreatePipe");
        }
        childEnd = hostEndIsRead ? write : read;
        hostEnd = hostEndIsRead ? read : write;
        if (!SetHandleInformation(
                childEnd,
                HandleFlagInherit,
                HandleFlagInherit))
        {
            childEnd.Dispose();
            hostEnd.Dispose();
            ThrowLastError("SetHandleInformation(child)");
        }
        if (!SetHandleInformation(
                hostEnd,
                HandleFlagInherit,
                0))
        {
            childEnd.Dispose();
            hostEnd.Dispose();
            ThrowLastError("SetHandleInformation");
        }
    }

    private static void ThrowLastError(string operation)
    {
        var code = Marshal.GetLastWin32Error();
        throw new Win32Exception(code, $"{operation} failed with Win32 error {code}.");
    }

    private static string Quote(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }
        if (!value.Any(character => char.IsWhiteSpace(character) || character == '"'))
        {
            return value;
        }
        var builder = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }
            if (character == '"')
            {
                builder.Append('\\', backslashes * 2 + 1).Append('"');
                backslashes = 0;
                continue;
            }
            builder.Append('\\', backslashes).Append(character);
            backslashes = 0;
        }
        builder.Append('\\', backslashes * 2).Append('"');
        return builder.ToString();
    }

    private static uint SerializeHandle(SafeFileHandle handle)
    {
        var value = handle.DangerousGetHandle().ToInt64();
        return checked((uint)value);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Cb;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
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
        public IntPtr Reserved2Pointer;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public uint ProcessId;
        public uint ThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreatePipe(
        out SafeFileHandle readPipe,
        out SafeFileHandle writePipe,
        ref SecurityAttributes pipeAttributes,
        uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(
        SafeFileHandle handle,
        uint mask,
        uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        int attributeCount,
        int flags,
        ref nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        uint flags,
        nuint attribute,
        IntPtr value,
        nuint size,
        IntPtr previousValue,
        IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(
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
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out IntPtr targetHandle,
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint options);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);
}
