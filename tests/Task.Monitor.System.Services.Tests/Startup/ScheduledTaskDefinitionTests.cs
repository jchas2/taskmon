using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class ScheduledTaskDefinitionTests
{
    private const string Header = """
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
        """;

    [Fact]
    public void Parses_A_Logon_Triggered_Exec_Task()
    {
        string xml = $"""
            {Header}
              <RegistrationInfo>
                <Author>Microsoft Corporation</Author>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <UserId>DOMAIN\jchas</UserId>
                </LogonTrigger>
              </Triggers>
              <Actions Context="Author">
                <Exec>
                  <Command>C:\Program Files\App\app.exe</Command>
                  <Arguments>/start</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;

        ScheduledTaskDefinition? definition = ScheduledTaskDefinition.Parse(xml);

        Assert.NotNull(definition);
        Assert.True(definition.HasLogonTrigger);
        Assert.False(definition.HasBootTrigger);
        Assert.Equal("Microsoft Corporation", definition.Author);
        Assert.Equal(@"C:\Program Files\App\app.exe", definition.ExecutablePath);
        Assert.Equal("/start", definition.Arguments);
        Assert.Equal(@"DOMAIN\jchas", definition.LogonTriggerUserId);
    }

    [Fact]
    public void Parses_A_Boot_Triggered_Task()
    {
        string xml = $"""
            {Header}
              <Triggers>
                <BootTrigger />
              </Triggers>
              <Actions>
                <Exec>
                  <Command>C:\Windows\System32\svc.exe</Command>
                </Exec>
              </Actions>
            </Task>
            """;

        ScheduledTaskDefinition? definition = ScheduledTaskDefinition.Parse(xml);

        Assert.NotNull(definition);
        Assert.True(definition.HasBootTrigger);
        Assert.False(definition.HasLogonTrigger);
        Assert.Null(definition.Arguments);
    }

    [Fact]
    public void Treats_An_Empty_LogonTrigger_UserId_As_Any_User()
    {
        string xml = $"""
            {Header}
              <Triggers>
                <LogonTrigger>
                  <UserId></UserId>
                </LogonTrigger>
              </Triggers>
              <Actions>
                <Exec>
                  <Command>app.exe</Command>
                </Exec>
              </Actions>
            </Task>
            """;

        ScheduledTaskDefinition? definition = ScheduledTaskDefinition.Parse(xml);

        Assert.NotNull(definition);
        Assert.True(definition.HasLogonTrigger);
        Assert.Null(definition.LogonTriggerUserId);
    }

    [Fact]
    public void Time_Triggered_Task_Has_Neither_Logon_Nor_Boot_Trigger()
    {
        string xml = $"""
            {Header}
              <Triggers>
                <TimeTrigger>
                  <StartBoundary>2026-01-01T09:00:00</StartBoundary>
                </TimeTrigger>
              </Triggers>
              <Actions>
                <Exec>
                  <Command>app.exe</Command>
                </Exec>
              </Actions>
            </Task>
            """;

        ScheduledTaskDefinition? definition = ScheduledTaskDefinition.Parse(xml);

        Assert.NotNull(definition);
        Assert.False(definition.HasLogonTrigger);
        Assert.False(definition.HasBootTrigger);
    }

    [Fact]
    public void A_ComHandler_Only_Task_Has_No_Executable_Path()
    {
        string xml = $"""
            {Header}
              <Triggers>
                <LogonTrigger />
              </Triggers>
              <Actions>
                <ComHandler />
              </Actions>
            </Task>
            """;

        ScheduledTaskDefinition? definition = ScheduledTaskDefinition.Parse(xml);

        Assert.NotNull(definition);
        Assert.True(definition.HasLogonTrigger);
        Assert.Null(definition.ExecutablePath);
        Assert.Null(definition.Arguments);
    }

    [Fact]
    public void Missing_Author_Is_Null()
    {
        string xml = $"""
            {Header}
              <Triggers>
                <LogonTrigger />
              </Triggers>
              <Actions>
                <Exec>
                  <Command>app.exe</Command>
                </Exec>
              </Actions>
            </Task>
            """;

        ScheduledTaskDefinition? definition = ScheduledTaskDefinition.Parse(xml);

        Assert.NotNull(definition);
        Assert.Null(definition.Author);
    }

    [Fact]
    public void Malformed_Xml_Returns_Null()
    {
        Assert.Null(ScheduledTaskDefinition.Parse("<Task><Unclosed></Task>"));
    }

    [Fact]
    public void Empty_Xml_Returns_Null()
    {
        Assert.Null(ScheduledTaskDefinition.Parse(""));
    }
}
