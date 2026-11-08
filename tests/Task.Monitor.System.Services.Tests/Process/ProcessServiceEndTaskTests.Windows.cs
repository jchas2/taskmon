namespace Task.Monitor.System.Services.Tests.Process;

public partial class ProcessServiceEndTaskTests
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
