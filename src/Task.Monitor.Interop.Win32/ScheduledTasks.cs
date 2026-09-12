using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

/// <summary>
/// Enumerates registered Windows Task Scheduler tasks (name, folder, enabled state, and full XML
/// definition) via the Task Scheduler 2.0 COM API (taskschd.dll).
///
/// Bound through the raw COM vtable rather than [ComImport] interfaces, for the same reason as
/// <see cref="ShellLink"/> and <see cref="Wbem"/>: taskmon publishes with PublishAot=true and
/// Native AOT has no built-in COM marshalling, so an RCW based binding compiles but fails at
/// runtime in the published build.
///
/// ITaskService / ITaskFolder / ITaskFolderCollection / IRegisteredTaskCollection / IRegisteredTask
/// are all dual (IDispatch-derived) interfaces, so their own members start at vtable slot 7 (past
/// IUnknown's 3 and IDispatch's 4). The slot numbers below were cross-checked against the
/// authoritative taskschd.idl (Wine's mirror of the Microsoft SDK definition) and verified live
/// against a real task store before being hardcoded here - a hand-recalled slot number that is
/// off by one silently calls the wrong method instead of failing loudly.
/// </summary>
public static unsafe class ScheduledTasks
{
    private static readonly Guid CLSID_TaskScheduler = new("0F87369F-A4E5-4CFC-BD3E-73E6154572DD");
    private static readonly Guid IID_ITaskService = new("2FABA4C7-4DA9-4013-9697-20CC3FD40F85");

    private const uint CLSCTX_INPROC_SERVER = 1;

    // Passed to GetFolders / GetTasks. TASK_ENUM_HIDDEN (0x1) would also return hidden tasks - most
    // of the OS's own maintenance tasks under \Microsoft\Windows\... are hidden, so excluding them
    // matches the Task Scheduler UI's default view and keeps a "why does this run at startup" list
    // free of that noise.
    private const int TASK_ENUM_EXCLUDE_HIDDEN = 0;

    // VARIANT is 24 bytes on x64: vt at +0, the value union at +8 (same layout as Wbem.VariantSize).
    private const int VariantSize = 24;

    // ITaskService (IDispatch + 9): GetFolder=7, GetRunningTasks=8, NewTask=9, Connect=10,
    // get_Connected=11, get_TargetServer=12, get_ConnectedUser=13, get_ConnectedDomain=14,
    // get_HighestVersion=15.
    private const int SlotServiceGetFolder = 7;
    private const int SlotServiceConnect = 10;

    // ITaskFolder (IDispatch + 13): get_Name=7, get_Path=8, GetFolder=9, GetFolders=10,
    // CreateFolder=11, DeleteFolder=12, GetTask=13, GetTasks=14, DeleteTask=15, ...
    private const int SlotFolderPath = 8;
    private const int SlotFolderGetFolders = 10;
    private const int SlotFolderGetTasks = 14;

    // ITaskFolderCollection and IRegisteredTaskCollection share the same shape
    // (IDispatch + 3): get_Count=7, get_Item=8, get__NewEnum=9.
    private const int SlotCollectionCount = 7;
    private const int SlotCollectionItem = 8;

    // IRegisteredTask (IDispatch + 18): get_Name=7, get_Path=8, get_State=9, get_Enabled=10,
    // put_Enabled=11, Run=12, RunEx=13, GetInstances=14, get_LastRunTime=15, get_LastTaskResult=16,
    // get_NumberOfMissedRuns=17, get_NextRunTime=18, get_Definition=19, get_Xml=20, ...
    private const int SlotTaskName = 7;
    private const int SlotTaskEnabled = 10;
    private const int SlotTaskXml = 20;

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    private static extern int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [DllImport(Libraries.Ole32)]
    private static extern void CoUninitialize();

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    private static extern int CoCreateInstance(
        in Guid rclsid, nint pUnkOuter, uint dwClsContext, in Guid riid, out nint ppv);

    [DllImport(Libraries.OleAut32)]
    private static extern nint SysAllocString([MarshalAs(UnmanagedType.LPWStr)] string value);

    [DllImport(Libraries.OleAut32)]
    private static extern void SysFreeString(nint bstr);

    public readonly record struct TaskRecord(string FolderPath, string Name, bool Enabled, string Xml);

    /// <summary>
    /// Enumerates every non-hidden registered task under the root folder and its subfolders.
    /// Returns an empty list on any failure connecting to the Task Scheduler service.
    /// </summary>
    public static IReadOnlyList<TaskRecord> EnumerateTasks()
    {
        List<TaskRecord> records = new();

        // COINIT_MULTITHREADED (0) - this runs on a background scan thread with no message pump,
        // same choice as Wbem.
        int initHr = CoInitializeEx(nint.Zero, 0);
        bool shouldUninitialise = initHr >= 0;

        nint taskService = nint.Zero;
        nint rootFolder = nint.Zero;

        try {
            if (CoCreateInstance(
                    in CLSID_TaskScheduler, nint.Zero, CLSCTX_INPROC_SERVER,
                    in IID_ITaskService, out taskService) < 0 || taskService == nint.Zero) {

                return records;
            }

            if (!Connect(taskService)) {
                return records;
            }

            rootFolder = GetFolder(taskService, "\\");

            if (rootFolder == nint.Zero) {
                return records;
            }

            WalkFolder(rootFolder, records);
            return records;
        }
        catch (Exception ex) {
            Trace.WriteLine($"{nameof(ScheduledTasks)}.{nameof(EnumerateTasks)} failed: {ex.Message}");
            return records;
        }
        finally {
            if (rootFolder != nint.Zero) {
                Marshal.Release(rootFolder);
            }

            if (taskService != nint.Zero) {
                Marshal.Release(taskService);
            }

            if (shouldUninitialise) {
                CoUninitialize();
            }
        }
    }

    private static bool Connect(nint taskService)
    {
        // Four empty (VT_EMPTY) VARIANTs: connect to the local machine as the current user.
        byte* server = stackalloc byte[VariantSize]; new Span<byte>(server, VariantSize).Clear();
        byte* user = stackalloc byte[VariantSize]; new Span<byte>(user, VariantSize).Clear();
        byte* domain = stackalloc byte[VariantSize]; new Span<byte>(domain, VariantSize).Clear();
        byte* password = stackalloc byte[VariantSize]; new Span<byte>(password, VariantSize).Clear();

        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint, nint, nint, int>)
            Vtbl(taskService)[SlotServiceConnect])(
                taskService, (nint)server, (nint)user, (nint)domain, (nint)password);

        return hr >= 0;
    }

    private static nint GetFolder(nint taskService, string path)
    {
        nint pathBstr = SysAllocString(path);

        try {
            nint folder;

            int hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)
                Vtbl(taskService)[SlotServiceGetFolder])(taskService, pathBstr, &folder);

            return hr >= 0 ? folder : nint.Zero;
        }
        finally {
            SysFreeString(pathBstr);
        }
    }

    private static void WalkFolder(nint folder, List<TaskRecord> records)
    {
        try {
            string folderPath = GetBstrProperty(folder, SlotFolderPath);

            nint taskCollection;

            int getTasksHr = ((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)
                Vtbl(folder)[SlotFolderGetTasks])(folder, TASK_ENUM_EXCLUDE_HIDDEN, &taskCollection);

            if (getTasksHr >= 0 && taskCollection != nint.Zero) {
                try {
                    ReadTasks(taskCollection, folderPath, records);
                }
                finally {
                    Marshal.Release(taskCollection);
                }
            }

            nint folderCollection;

            int getFoldersHr = ((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)
                Vtbl(folder)[SlotFolderGetFolders])(folder, TASK_ENUM_EXCLUDE_HIDDEN, &folderCollection);

            if (getFoldersHr >= 0 && folderCollection != nint.Zero) {
                try {
                    int count = GetCollectionCount(folderCollection);

                    for (int i = 1; i <= count; i++) {
                        nint subFolder = GetCollectionItem(folderCollection, i);

                        if (subFolder == nint.Zero) {
                            continue;
                        }

                        try {
                            WalkFolder(subFolder, records);
                        }
                        finally {
                            Marshal.Release(subFolder);
                        }
                    }
                }
                finally {
                    Marshal.Release(folderCollection);
                }
            }
        }
        catch (Exception ex) {
            Trace.WriteLine($"{nameof(ScheduledTasks)}.{nameof(WalkFolder)} failed: {ex.Message}");
        }
    }

    private static void ReadTasks(nint taskCollection, string folderPath, List<TaskRecord> records)
    {
        int count = GetCollectionCount(taskCollection);

        for (int i = 1; i <= count; i++) {
            nint task = GetCollectionItem(taskCollection, i);

            if (task == nint.Zero) {
                continue;
            }

            try {
                string name = GetBstrProperty(task, SlotTaskName);
                bool enabled = GetVariantBoolProperty(task, SlotTaskEnabled);
                string xml = GetBstrProperty(task, SlotTaskXml);

                records.Add(new TaskRecord(folderPath, name, enabled, xml));
            }
            finally {
                Marshal.Release(task);
            }
        }
    }

    private static int GetCollectionCount(nint collection)
    {
        int count;

        ((delegate* unmanaged[Stdcall]<nint, int*, int>)
            Vtbl(collection)[SlotCollectionCount])(collection, &count);

        return count;
    }

    private static nint GetCollectionItem(nint collection, int index)
    {
        byte* indexVariant = stackalloc byte[VariantSize];
        new Span<byte>(indexVariant, VariantSize).Clear();
        *(ushort*)indexVariant = 3; // VT_I4
        *(int*)(indexVariant + 8) = index;

        nint item;

        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)
            Vtbl(collection)[SlotCollectionItem])(collection, (nint)indexVariant, &item);

        return hr >= 0 ? item : nint.Zero;
    }

    private static string GetBstrProperty(nint obj, int slot)
    {
        nint bstr;

        int hr = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)Vtbl(obj)[slot])(obj, &bstr);

        if (hr < 0 || bstr == nint.Zero) {
            return string.Empty;
        }

        string value = Marshal.PtrToStringBSTR(bstr);
        Marshal.FreeBSTR(bstr);

        return value;
    }

    private static bool GetVariantBoolProperty(nint obj, int slot)
    {
        short value;

        ((delegate* unmanaged[Stdcall]<nint, short*, int>)Vtbl(obj)[slot])(obj, &value);

        return value != 0;
    }

    private static void** Vtbl(nint pUnknown) => *(void***)pUnknown;
}
