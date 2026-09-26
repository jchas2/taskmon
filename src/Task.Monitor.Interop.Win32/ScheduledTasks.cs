using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.Interop.Win32;

// tasksched.dll is bound through its raw vtable rather than through [ComImport] interfaces.
// taskmon publishes with PublishAot=true, and Native AOT has no built-in COM marshalling.
// We must call the vtable directly.
public static unsafe class ScheduledTasks
{
    private static readonly Guid CLSID_TaskScheduler = new("0F87369F-A4E5-4CFC-BD3E-73E6154572DD");
    private static readonly Guid IID_ITaskService    = new("2FABA4C7-4DA9-4013-9697-20CC3FD40F85");

    private const uint CLSCTX_INPROC_SERVER = 1;

    private const int TASK_ENUM_EXCLUDE_HIDDEN = 0;

    private const int VariantSize = 24;

    // ITaskService vtable slots.
    private const int SlotServiceGetFolder = 7;
    private const int SlotServiceConnect = 10;

    // ITaskFolder vtable slots.
    private const int SlotFolderPath = 8;
    private const int SlotFolderGetFolders = 10;
    private const int SlotFolderGetTasks = 14;

    // ITaskFolderCollection and IRegisteredTaskCollection share the same vtable slots.
    private const int SlotCollectionCount = 7;
    private const int SlotCollectionItem = 8;

    // IRegisteredTask vtable slots.
    private const int SlotTaskName = 7;
    private const int SlotTaskEnabled = 10;
    private const int SlotTaskXml = 20;

    public readonly record struct TaskRecord(
        string FolderPath, 
        string Name, 
        bool Enabled, 
        string Xml);

    public static IReadOnlyList<TaskRecord> EnumerateTasks()
    {
        List<TaskRecord> records = new();
        nint taskService = nint.Zero;
        nint rootFolder = nint.Zero;

        int hResult = Ole32.ComInitializeEx(Ole32.COINIT_MULTITHREADED);

        if (Ole32.CoCreateInstance(
            in CLSID_TaskScheduler, 
            nint.Zero, 
            CLSCTX_INPROC_SERVER,
            in IID_ITaskService, 
            out taskService) < 0 || taskService == nint.Zero) {

            goto Finished;
        }

        if (!Connect(taskService)) {
            goto Finished;
        }

        rootFolder = GetFolder(taskService, "\\");

        if (rootFolder == nint.Zero) {
            goto Finished;
        }

        WalkFolder(rootFolder, records);

        Finished:
        InteropHelper.MarshalRelease(ref rootFolder);
        InteropHelper.MarshalRelease(ref taskService);
        Ole32.ComUninitialize(hResult);
        return records;
    }

    private static bool Connect(nint taskService)
    {
        // Connect to the local machine as the current user.
        byte* server   = stackalloc byte[VariantSize]; 
        byte* user     = stackalloc byte[VariantSize]; 
        byte* domain   = stackalloc byte[VariantSize]; 
        byte* password = stackalloc byte[VariantSize]; 

        new Span<byte>(server,   VariantSize).Clear();
        new Span<byte>(user,     VariantSize).Clear();
        new Span<byte>(domain,   VariantSize).Clear();
        new Span<byte>(password, VariantSize).Clear();
        
        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint, nint, nint, int>)
            Ole32.Vtbl(taskService)[SlotServiceConnect])(
                taskService, 
                (nint)server, 
                (nint)user, 
                (nint)domain, 
                (nint)password);

        return hr >= 0;
    }

    private static nint GetFolder(nint taskService, string path)
    {
        nint pathBstr = Ole32.SysAllocString(path);
        nint folder;

        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)
            Ole32.Vtbl(taskService)[SlotServiceGetFolder])(taskService, pathBstr, &folder);

        nint result = hr >= 0 ? folder : nint.Zero;
        Ole32.SysFreeString(pathBstr);
        return result;
    }

    private static void WalkFolder(nint folder, List<TaskRecord> records)
    {
        string folderPath = GetBSTRProperty(folder, SlotFolderPath);
        nint taskCollection;

        int getTasksHr = ((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)
            Ole32.Vtbl(folder)[SlotFolderGetTasks])(folder, TASK_ENUM_EXCLUDE_HIDDEN, &taskCollection);

        if (getTasksHr >= 0 && taskCollection != nint.Zero) {
            ReadTasks(taskCollection, folderPath, records);
            InteropHelper.MarshalRelease(ref taskCollection);
        }

        nint folderCollection;

        int getFoldersHr = ((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)
            Ole32.Vtbl(folder)[SlotFolderGetFolders])(folder, TASK_ENUM_EXCLUDE_HIDDEN, &folderCollection);

        if (getFoldersHr >= 0 && folderCollection != nint.Zero) {
            int count = GetCollectionCount(folderCollection);

            for (int i = 1; i <= count; i++) {
                nint subFolder = GetCollectionItem(folderCollection, i);

                if (subFolder == nint.Zero) {
                    continue;
                }

                WalkFolder(subFolder, records);
                InteropHelper.MarshalRelease(ref subFolder);
            }
            
            InteropHelper.MarshalRelease(ref folderCollection);
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

            string name = GetBSTRProperty(task, SlotTaskName);
            bool enabled = GetVariantBoolProperty(task, SlotTaskEnabled);
            string xml = GetBSTRProperty(task, SlotTaskXml);

            records.Add(new TaskRecord(
                folderPath, 
                name, 
                enabled, 
                xml));
            
            InteropHelper.MarshalRelease(ref task);
        }
    }

    private static int GetCollectionCount(nint collection)
    {
        int count;

        ((delegate* unmanaged[Stdcall]<nint, int*, int>)
            Ole32.Vtbl(collection)[SlotCollectionCount])(collection, &count);

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
            Ole32.Vtbl(collection)[SlotCollectionItem])(collection, (nint)indexVariant, &item);

        return hr >= 0 ? item : nint.Zero;
    }

    private static string GetBSTRProperty(nint obj, int slot)
    {
        nint bstr;
        int hr = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)Ole32.Vtbl(obj)[slot])(obj, &bstr);

        if (hr < 0 || bstr == nint.Zero) {
            return string.Empty;
        }

        string value = InteropHelper.TryMarshalPtrToStringBSTR(bstr); 
        Marshal.FreeBSTR(bstr);
        return value;
    }

    private static bool GetVariantBoolProperty(nint obj, int slot)
    {
        short value;
        ((delegate* unmanaged[Stdcall]<nint, short*, int>)Ole32.Vtbl(obj)[slot])(obj, &value);
        return value != 0;
    }
}
