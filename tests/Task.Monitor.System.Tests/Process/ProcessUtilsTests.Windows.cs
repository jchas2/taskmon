namespace Task.Monitor.System.Tests.Process;

public partial class ProcessUtilsTests
{
#if __WIN32__
    private void CreateScriptFile(string fileName)
    {
        string script = @"
:loop
timeout /t 1 > null
goto loop
";
        File.WriteAllText(fileName, script);
    }
#endif
}
