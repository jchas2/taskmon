#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static unsafe class TaskSchedulerClient
{
    public readonly record struct TaskRecord(
        string FolderPath,
        string Name,
        bool   Enabled,
        string Xml);

    private static int Connect(nint taskService)
    {
        // Empty VARIANTs connect to the local machine as the current user.
        byte* server   = stackalloc byte[Ole32.VariantSize];
        byte* user     = stackalloc byte[Ole32.VariantSize];
        byte* domain   = stackalloc byte[Ole32.VariantSize];
        byte* password = stackalloc byte[Ole32.VariantSize];

        new Span<byte>(server,   Ole32.VariantSize).Clear();
        new Span<byte>(user,     Ole32.VariantSize).Clear();
        new Span<byte>(domain,   Ole32.VariantSize).Clear();
        new Span<byte>(password, Ole32.VariantSize).Clear();

        return ScheduledTasks.ITaskService_Connect(taskService, (nint)server, (nint)user, (nint)domain, (nint)password);
    }

    private static int ConnectService(out nint taskService, out string call)
    {
        call = $"{nameof(Ole32.CoCreateInstance)} TaskScheduler";

        int hr = Ole32.CoCreateInstance(
            in ScheduledTasks.CLSID_TaskScheduler,
            nint.Zero,
            Ole32.CLSCTX_INPROC_SERVER,
            in ScheduledTasks.IID_ITaskService,
            out taskService);

        if (hr < 0 || taskService == nint.Zero) {
            taskService = nint.Zero;
            return hr < 0 ? hr : ScheduledTasks.E_FAIL;
        }

        call = "ITaskService::Connect";

        return Connect(taskService);
    }

    public static IReadOnlyList<TaskRecord> EnumerateTasks(out ComFailure? failure)
    {
        List<TaskRecord> records = new();

        failure = null;

        using ComInitScope com = ComInitScope.Initialise(Ole32.COINIT_MULTITHREADED);
        int connectHr = ConnectService(out nint taskServiceRef, out string connectCall);
        using ComScope taskService = new(taskServiceRef);

        if (connectHr < 0) {
            failure = new ComFailure(connectCall, connectHr);
            return records;
        }

        int folderHr = GetByPath(taskService, &ScheduledTasks.ITaskService_GetFolder, "\\", out nint rootFolderRef);

        using ComScope rootFolder = new(rootFolderRef);

        if (folderHr < 0) {
            failure = new ComFailure(@"ITaskService::GetFolder \", folderHr);
            return records;
        }

        WalkFolder(rootFolder, records);
        return records;
    }

    private static string GetBstr(nint obj, delegate*<nint, nint*, int> get)
    {
        nint value = nint.Zero;

        if (get(obj, &value) < 0 || value == nint.Zero) {
            return string.Empty;
        }

        using BstrScope bstr = new(value);

        return InteropHelper.TryMarshalPtrToStringBSTR(bstr);
    }

    private static int GetByPath(nint obj, delegate*<nint, nint, nint*, int> get, string path, out nint result)
    {
        using BstrScope pathBstr = BstrScope.Allocate(path);

        nint found = nint.Zero;
        int  hr    = get(obj, pathBstr, &found);

        result = hr >= 0 ? found : nint.Zero;
        return hr;
    }

    private static int GetCollectionCount(nint collection)
    {
        int count = 0;

        ScheduledTasks.TaskCollection_get_Count(collection, &count);

        return count;
    }

    private static nint GetCollectionItem(nint collection, int index)
    {
        byte* indexVariant = stackalloc byte[Ole32.VariantSize];
        new Span<byte>(indexVariant, Ole32.VariantSize).Clear();
        *(ushort*)indexVariant    = 3; // VT_I4
        *(int*)(indexVariant + 8) = index;

        nint item = nint.Zero;
        int  hr   = ScheduledTasks.TaskCollection_get_Item(collection, (nint)indexVariant, &item);

        return hr >= 0 ? item : nint.Zero;
    }

    private static void ReadTasks(nint taskCollection, string folderPath, List<TaskRecord> records)
    {
        int count = GetCollectionCount(taskCollection);

        for (int i = 1; i <= count; i++) {
            using ComScope task = new(GetCollectionItem(taskCollection, i));

            if (task.IsNull) {
                continue;
            }

            short enabled = ScheduledTasks.VARIANT_FALSE;

            ScheduledTasks.IRegisteredTask_get_Enabled(task, &enabled);

            records.Add(new TaskRecord(
                folderPath,
                GetBstr(task, &ScheduledTasks.IRegisteredTask_get_Name),
                enabled != ScheduledTasks.VARIANT_FALSE,
                GetBstr(task, &ScheduledTasks.IRegisteredTask_get_Xml)));
        }
    }

    public static int SetEnabled(string folderPath, string name, bool enabled)
    {
        using ComInitScope com = ComInitScope.Initialise(Ole32.COINIT_MULTITHREADED);
        int hr = ConnectService(out nint taskServiceRef, out _);
        using ComScope taskService = new(taskServiceRef);

        if (hr < 0) {
            return hr;
        }

        hr = GetByPath(taskService, &ScheduledTasks.ITaskService_GetFolder, folderPath, out nint folderRef);

        using ComScope folder = new(folderRef);

        if (hr < 0) {
            return hr;
        }

        hr = GetByPath(folder, &ScheduledTasks.ITaskFolder_GetTask, name, out nint taskRef);

        using ComScope task = new(taskRef);

        if (hr < 0) {
            return hr;
        }

        return ScheduledTasks.IRegisteredTask_put_Enabled(task, enabled 
            ? ScheduledTasks.VARIANT_TRUE 
            : ScheduledTasks.VARIANT_FALSE);
    }

    private static void WalkFolder(nint folder, List<TaskRecord> records)
    {
        string folderPath        = GetBstr(folder, &ScheduledTasks.ITaskFolder_get_Path);
        nint   taskCollectionRef = nint.Zero;

        int getTasksHr = ScheduledTasks.ITaskFolder_GetTasks(folder, ScheduledTasks.TASK_ENUM_EXCLUDE_HIDDEN, &taskCollectionRef);

        using (ComScope taskCollection = new(taskCollectionRef)) {
            if (getTasksHr >= 0 && !taskCollection.IsNull) {
                ReadTasks(taskCollection, folderPath, records);
            }
        }

        nint folderCollectionRef = nint.Zero;

        int getFoldersHr = ScheduledTasks.ITaskFolder_GetFolders(folder, ScheduledTasks.TASK_ENUM_EXCLUDE_HIDDEN, &folderCollectionRef);

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
#endif
