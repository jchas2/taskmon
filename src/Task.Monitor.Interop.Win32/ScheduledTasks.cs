namespace Task.Monitor.Interop.Win32;

// tasksched.dll is bound through its raw vtable rather than through [ComImport] interfaces.
// taskmon publishes with PublishAot=true, and Native AOT has no built-in COM marshalling.
// We must call the vtable directly.
public static unsafe class ScheduledTasks
{
    public static readonly Guid CLSID_TaskScheduler = new("0F87369F-A4E5-4CFC-BD3E-73E6154572DD");
    public static readonly Guid IID_ITaskService    = new("2FABA4C7-4DA9-4013-9697-20CC3FD40F85");

    public const int TASK_ENUM_EXCLUDE_HIDDEN = 0;

    public const int E_FAIL = unchecked((int)0x80004005);

    public const short VARIANT_FALSE = 0;
    public const short VARIANT_TRUE  = -1;

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

    public static int IRegisteredTask_get_Enabled(nint task, short* enabled) =>
        ((delegate* unmanaged[Stdcall]<nint, short*, int>)Ole32.Vtbl(task)[SlotTaskEnabled])(task, enabled);

    public static int IRegisteredTask_get_Name(nint task, nint* name) =>
        ((delegate* unmanaged[Stdcall]<nint, nint*, int>)Ole32.Vtbl(task)[SlotTaskName])(task, name);

    public static int IRegisteredTask_get_Xml(nint task, nint* xml) =>
        ((delegate* unmanaged[Stdcall]<nint, nint*, int>)Ole32.Vtbl(task)[SlotTaskXml])(task, xml);

    public static int IRegisteredTask_put_Enabled(nint task, short enabled) =>
        ((delegate* unmanaged[Stdcall]<nint, short, int>)Ole32.Vtbl(task)[SlotTaskPutEnabled])(task, enabled);

    public static int ITaskFolder_GetFolders(nint folder, int flags, nint* folders) =>
        ((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Ole32.Vtbl(folder)[SlotFolderGetFolders])(folder, flags, folders);

    public static int ITaskFolder_GetTask(nint folder, nint path, nint* task) =>
        ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Ole32.Vtbl(folder)[SlotFolderGetTask])(folder, path, task);

    public static int ITaskFolder_GetTasks(nint folder, int flags, nint* tasks) =>
        ((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Ole32.Vtbl(folder)[SlotFolderGetTasks])(folder, flags, tasks);

    public static int ITaskFolder_get_Path(nint folder, nint* path) =>
        ((delegate* unmanaged[Stdcall]<nint, nint*, int>)Ole32.Vtbl(folder)[SlotFolderPath])(folder, path);

    public static int ITaskService_Connect(nint service, nint serverName, nint user, nint domain, nint password) =>
        ((delegate* unmanaged[Stdcall]<nint, nint, nint, nint, nint, int>)
            Ole32.Vtbl(service)[SlotServiceConnect])(service, serverName, user, domain, password);

    public static int ITaskService_GetFolder(nint service, nint path, nint* folder) =>
        ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Ole32.Vtbl(service)[SlotServiceGetFolder])(service, path, folder);

    public static int TaskCollection_get_Count(nint collection, int* count) =>
        ((delegate* unmanaged[Stdcall]<nint, int*, int>)Ole32.Vtbl(collection)[SlotCollectionCount])(collection, count);

    public static int TaskCollection_get_Item(nint collection, nint index, nint* item) =>
        ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Ole32.Vtbl(collection)[SlotCollectionItem])(collection, index, item);
}
