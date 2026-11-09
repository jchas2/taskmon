using System.Diagnostics;
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

    private const int E_FAIL = unchecked((int)0x80004005);

    private const short VARIANT_FALSE = 0;
    private const short VARIANT_TRUE  = -1;

    private const int VariantSize = 24;

    // ITaskService vtable slots.
    private const int SlotServiceGetFolder = 7;
    private const int SlotServiceConnect = 10;

    // ITaskFolder vtable slots.
    private const int SlotFolderPath = 8;
    private const int SlotFolderGetFolders = 10;
    private const int SlotFolderGetTask = 13;
    private const int SlotFolderGetTasks = 14;

    // ITaskFolderCollection and IRegisteredTaskCollection share the same vtable slots.
    private const int SlotCollectionCount = 7;
    private const int SlotCollectionItem = 8;

    // IRegisteredTask vtable slots.
    private const int SlotTaskName = 7;
    private const int SlotTaskEnabled = 10;
    private const int SlotTaskPutEnabled = 11;
    private const int SlotTaskXml = 20;

    public readonly record struct TaskRecord(
        string FolderPath, 
        string Name, 
        bool Enabled, 
        string Xml);

    private static int Connect(nint taskService)
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

        return hr;
    }

    private static int ConnectService(out nint taskService)
    {
        int hr = Ole32.CoCreateInstance(
            in CLSID_TaskScheduler,
            nint.Zero,
            CLSCTX_INPROC_SERVER,
            in IID_ITaskService,
            out taskService);

        if (hr < 0 || taskService == nint.Zero) {
            taskService = nint.Zero;
            return hr < 0 ? hr : E_FAIL;
        }

        return Connect(taskService);
    }

    public static IReadOnlyList<TaskRecord> EnumerateTasks()
    {
        List<TaskRecord> records = new();

        using ComInitScope com = ComInitScope.Initialise(Ole32.COINIT_MULTITHREADED);
        int connectHr = ConnectService(out nint taskServiceRef);
        using ComScope taskService = new(taskServiceRef);

        if (connectHr < 0) {
            return records;
        }

        int folderHr = GetFolder(taskService, "\\", out nint rootFolderRef);

        using ComScope rootFolder = new(rootFolderRef);

        if (folderHr < 0) {
            return records;
        }

        WalkFolder(rootFolder, records);
        return records;
    }

    private static string GetBSTRProperty(nint obj, int slot)
    {
        nint value = nint.Zero;
        int  hr    = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)Ole32.Vtbl(obj)[slot])(obj, &value);

        if (hr < 0 || value == nint.Zero) {
            return string.Empty;
        }

        using BstrScope bstr = new(value);

        return InteropHelper.TryMarshalPtrToStringBSTR(bstr);
    }

    private static int GetByPath(nint obj, int slot, string path, out nint result)
    {
        using BstrScope pathBstr = BstrScope.Allocate(path);

        nint found;

        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)
            Ole32.Vtbl(obj)[slot])(obj, pathBstr, &found);

        result = hr >= 0 ? found : nint.Zero;
        return hr;
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

    private static int GetFolder(nint taskService, string path, out nint folder) =>
        GetByPath(taskService, SlotServiceGetFolder, path, out folder);

    private static int GetTask(nint folder, string name, out nint task) =>
        GetByPath(folder, SlotFolderGetTask, name, out task);

    private static bool GetVariantBoolProperty(nint obj, int slot)
    {
        short value;
        ((delegate* unmanaged[Stdcall]<nint, short*, int>)Ole32.Vtbl(obj)[slot])(obj, &value);
        return value != 0;
    }

    private static void ReadTasks(nint taskCollection, string folderPath, List<TaskRecord> records)
    {
        int count = GetCollectionCount(taskCollection);

        for (int i = 1; i <= count; i++) {
            using ComScope task = new(GetCollectionItem(taskCollection, i));

            if (task.IsNull) {
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
        }
    }

    public static int SetEnabled(string folderPath, string name, bool enabled)
    {
        using ComInitScope com = ComInitScope.Initialise(Ole32.COINIT_MULTITHREADED);
        int hr = ConnectService(out nint taskServiceRef);
        using ComScope taskService = new(taskServiceRef);

        if (hr < 0) {
            return hr;
        }

        hr = GetFolder(taskService, folderPath, out nint folderRef);

        using ComScope folder = new(folderRef);

        if (hr < 0) {
            return hr;
        }

        hr = GetTask(folder, name, out nint taskRef);

        using ComScope task = new(taskRef);

        if (hr < 0) {
            return hr;
        }

        return ((delegate* unmanaged[Stdcall]<nint, short, int>)
            Ole32.Vtbl(task)[SlotTaskPutEnabled])(task, enabled ? VARIANT_TRUE : VARIANT_FALSE);
    }

    private static void WalkFolder(nint folder, List<TaskRecord> records)
    {
        string folderPath        = GetBSTRProperty(folder, SlotFolderPath);
        nint   taskCollectionRef = nint.Zero;

        int getTasksHr = ((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)
            Ole32.Vtbl(folder)[SlotFolderGetTasks])(folder, TASK_ENUM_EXCLUDE_HIDDEN, &taskCollectionRef);

        using (ComScope taskCollection = new(taskCollectionRef)) {
            if (getTasksHr >= 0 && !taskCollection.IsNull) {
                ReadTasks(taskCollection, folderPath, records);
            }
        }

        nint folderCollectionRef = nint.Zero;

        int getFoldersHr = ((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)
            Ole32.Vtbl(folder)[SlotFolderGetFolders])(folder, TASK_ENUM_EXCLUDE_HIDDEN, &folderCollectionRef);

        using ComScope folderCollection = new(folderCollectionRef);

        if (getFoldersHr < 0 || folderCollection.IsNull) {
            return;
        }

        int count = GetCollectionCount(folderCollection);

        for (int i = 1; i <= count; i++) {
            using ComScope subFolder = new(GetCollectionItem(folderCollection, i));

            if (subFolder.IsNull) {
                continue;
            }

            WalkFolder(subFolder, records);
        }
    }
}
