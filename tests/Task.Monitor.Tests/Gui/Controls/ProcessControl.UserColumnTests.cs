using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.System.Services.Process;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ProcessControlUserColumnTests
{
    private const ulong TotalPhysicalMemory = 8UL * 1024 * 1024 * 1024;

    private static readonly Color Current = Color.FromArgb(1, 1, 1);
    private static readonly Color Other   = Color.FromArgb(2, 2, 2);
    private static readonly Color Root    = Color.FromArgb(3, 3, 3);
    private static readonly Color System  = Color.FromArgb(4, 4, 4);

    [Fact]
    public void A_Root_Account_Takes_The_Root_Colour_Even_For_The_Current_User() =>
        Assert.Equal(Root, UserColour(ProcessAccount.Root, Environment.UserName));

    [Fact]
    public void A_System_Account_Takes_The_System_Colour() =>
        Assert.Equal(System, UserColour(ProcessAccount.System, "LOCAL SERVICE"));

    [Fact]
    public void A_User_Account_Takes_The_Current_Or_Other_User_Colour()
    {
        Assert.Equal(Current, UserColour(ProcessAccount.User, Environment.UserName));
        Assert.Equal(Other,   UserColour(ProcessAccount.User, "someone-else"));
    }

    // A distinct colour for each kind of account, so a wrong choice fails.
    private static AppConfig CreateAppConfig()
    {
        AppConfig appConfig = new RunContextHelper().GetRunContext().AppConfig;

        appConfig.Theme.ColumnUserCurrentNonRoot = Current;
        appConfig.Theme.ColumnUserOtherNonRoot   = Other;
        appConfig.Theme.ColumnUserRoot           = Root;
        appConfig.Theme.ColumnUserSystem         = System;

        return appConfig;
    }

    private static Color UserColour(ProcessAccount account, string userName)
    {
        ProcessEntry entry = new() { Pid = 1, FileDescription = "test.exe", CmdLine = "test", Account = account, UserName = userName };

        ProcessControl.ProcessListViewItem item = new(entry, TotalPhysicalMemory, CreateAppConfig());

        return item.SubItems[(int)ProcessControl.Columns.User].ForegroundColor;
    }
}
