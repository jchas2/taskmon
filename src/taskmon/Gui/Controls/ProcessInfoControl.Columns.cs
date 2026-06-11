namespace Task.Monitor.Gui.Controls;

public partial class ProcessInfoControl
{
    internal const int ColumnInfoKeyWidth = 14;
    internal const int ColumnInfoValueWidth = 60;
 
    public enum InfoColumns
    {
        Key = 0,
        Value,
        Count
    }
    
    internal const int ColumnModuleNameWidth = 32;
    internal const int ColumnFileNameWidth = 32;
    
    internal enum ModuleColumns
    {
        ModuleName = 0,
        FileName,
        Count
    }
    
    internal const int ColumnThreadIdWidth = 12;
    internal const int ColumnThreadStateWidth = 16;
    internal const int ColumnThreadReasonWidth = 24;
    internal const int ColumnThreadPriorityWidth = 4;
    internal const int ColumnThreadStartAddressWidth = 20;
    internal const int ColumnThreadCpuKernelTimeWidth = 20;
    internal const int ColumnThreadCpuUserTimeWidth = 20;
    internal const int ColumnThreadCpuTotalTimeWidth = 20;

    internal enum ThreadColumns
    {
        Id = 0,
        State,
        Reason,
        Priority,
        StartAddress,
        CpuKernelTime,
        CpuUserTime,
        CpuTotalTime,
        Count
    }
}
    
