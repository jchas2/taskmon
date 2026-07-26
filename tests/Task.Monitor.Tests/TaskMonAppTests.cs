using System.Reflection;
using Task.Monitor.Actions;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;

namespace Task.Monitor.Tests;

public class TaskMonAppTests
{
    private readonly StringWriter stringOutputWriter = new();    // stdout
    private readonly StringWriter stringErrorWriter = new();     // stderr

    [Fact]
    public void TaskMon_Actions_Canary_Test()
    {
        AssemblyName assemblyName = Assembly
            .GetExecutingAssembly()
            .GetReferencedAssemblies()
            .First(asm => asm.Name == "taskmon");

         Assembly taskmonLib = Assembly.Load(assemblyName);

         int count = taskmonLib.GetExportedTypes()
             .Where(t => t.IsClass && t.Assembly == taskmonLib)
             .Count(t => typeof(IAction).IsAssignableFrom(t));
         
         Assert.Equal(4, count);
    }
    
    [Fact]
    public void Should_Set_Pid_Filter()
    {
        RunContext runContext = new RunContextHelper().GetRunContext();
        TaskMonApp app = new(runContext);
        bool result = app.ProcessArgs(new[] { "--pid", "1234" }, out List<IAction> actions);
        
        Assert.True(result);
        Assert.Equal(1234, runContext.AppConfig.FilterPid);
        Assert.Equal(typeof(RunAppAction), actions[0].GetType());
    }

    public static TheoryData<string, string, string> ValidStringFilterData()
        => new() {
            { "-u", "root", "FilterUserName" },
            { "--username", "root", "FilterUserName" },
            { "-u", "any_random_string_29380643206cslkjhcskdhc*&^&*%&%", "FilterUserName" },
            { "--username", "any_random_string_29380643206cslkjhcskdhc*&^&*%&%", "FilterUserName" },
            { "-p", "kernel_task", "FilterProcess" },
            { "--process", "kernel_task", "FilterProcess" },
            { "-p", "any_random_string_GJgj758578mmm*Y&%g", "FilterProcess" },
            { "--process", "any_random_string_GJgj758578mmm*Y&%g", "FilterProcess" },
        };
    [Theory]
    [MemberData(nameof(ValidStringFilterData))]
    public void Should_Set_String_Filters(string arg, string argValue, string appConfigPropertyName)
    {
        RunContext runContext = new RunContextHelper().GetRunContext();
        TaskMonApp app = new(runContext);
        bool result = app.ProcessArgs(new[] { arg, argValue }, out List<IAction> actions);

        string actualValue = runContext.AppConfig
            .GetType()
            .GetProperty(appConfigPropertyName)!
            .GetValue(runContext.AppConfig)!
            .ToString()!;
        
        Assert.True(result);
        Assert.Equal(argValue, actualValue);
        Assert.Equal(typeof(RunAppAction), actions[0].GetType());
    }
    
    public static TheoryData<string, string, string> InvalidArgData()
        => new() {
            { "--pid", "abc!@#", $"{Constants.AppName}: bad pid arg: abc!@#" },
            { "--sort", "$bogus$column$", $"{Constants.AppName}: bad sort arg: $bogus$column$" },
            { "--delay", "!#@$%^#$@", $"{Constants.AppName}: bad delay arg: !#@$%^#$@" },
            { "--delay", "200", $"{Constants.AppName}: bad delay arg: 200" },
            { "--delay", "-1500", $"{Constants.AppName}: bad delay arg: -1500" },
            { "--limit", "1oo", $"{Constants.AppName}: bad limit arg: 1oo" },
            { "--limit", "-10", $"{Constants.AppName}: bad limit arg: -10" },
            { "--nprocs", "4g77h", $"{Constants.AppName}: bad nprocs arg: 4g77h" },
            { "--nprocs", "0", $"{Constants.AppName}: bad nprocs arg: 0" },
            { "--nprocs", "-12", $"{Constants.AppName}: bad nprocs arg: -12" },
            { "--theme", "bad_theme_976GJG", $"{Constants.AppName}: bad theme arg: bad_theme_976GJG" }
        };

    [Theory]
    [MemberData(nameof(InvalidArgData))]
    public void Should_Fail_Invalid_Arg_Values(string arg, string argValue, string expectedError)
    {
        OutputWriter.SetOutputWriter(new OutputWriter(stringOutputWriter));
        OutputWriter.SetErrorWriter(new OutputWriter(stringErrorWriter));
        
        RunContext runContext = new RunContextHelper().GetRunContext();
        TaskMonApp app = new(runContext);

        bool result = app.ProcessArgs(new[] { arg, argValue }, out List<IAction> _);
    
        Assert.False(result);
        Assert.Equal(expectedError, stringErrorWriter.ToString().Trim());        
    }

    [Fact]
    public void Should_Set_Valid_Theme()
    {
        RunContext runContext = new RunContextHelper().GetRunContext();
        TaskMonApp app = new(runContext);
        bool result = app.ProcessArgs(new[] { "--theme", Constants.Sections.ThemeTaskmonDefault }, out List<IAction> actions);
        
        Assert.True(result);
        Assert.Equal(Constants.Sections.ThemeTaskmonDefault, runContext.AppConfig.DefaultTheme.Name);
        Assert.Equal(typeof(RunAppAction), actions[0].GetType());
    }

    public static TheoryData<string, Type> ActionArgData()
        => new() {
            { "-h", typeof(ShowUsageAction) },
            { "--help", typeof(ShowUsageAction) },
            { "--sort-help", typeof(SortHelpAction) },
            { "--theme-help", typeof(ThemeHelpAction) }
        };
    [Theory]
    [MemberData(nameof(ActionArgData))]
    public void Should_Load_Actions_For_Args(string arg, Type actionType)
    {
        RunContext runContext = new RunContextHelper().GetRunContext();
        TaskMonApp app = new(runContext);
        bool result = app.ProcessArgs(new[] { arg }, out List<IAction> actions);

        Assert.True(result);
        Assert.Equal(actionType, actions[0].GetType());
    }

    [Fact]
    public void Should_Load_RunApp_Action_For_No_Args()
    {
        RunContext runContext = new RunContextHelper().GetRunContext();
        TaskMonApp app = new(runContext);
        bool result = app.ProcessArgs(new string[] { }, out List<IAction> actions);

        Assert.True(result);
        Assert.Equal(typeof(RunAppAction), actions[0].GetType());
    }
}
