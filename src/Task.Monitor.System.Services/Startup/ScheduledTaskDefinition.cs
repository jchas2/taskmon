using System.Xml.Linq;

namespace Task.Monitor.System.Services.Startup;

// Parses the XML a registered task returns from IRegisteredTask::Xml.
public sealed class ScheduledTaskDefinition
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public string? Author             { get; private set; }
    public string? ExecutablePath     { get; private set; }
    public string? Arguments          { get; private set; }

    public string? LogonTriggerUserId { get; private set; }
    public bool HasBootTrigger        { get; private set; }
    public bool HasLogonTrigger       { get; private set; }

    public static ScheduledTaskDefinition? Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) {
            return null;
        }

        XElement? root;

        try {
            root = XDocument.Parse(xml).Root;
        }
        catch (Exception) {
            return null;
        }

        if (root is null) {
            return null;
        }

        XElement? triggers     = root.Element(Ns + "Triggers");
        XElement? logonTrigger = triggers?.Element(Ns + "LogonTrigger");
        bool hasBootTrigger    = triggers?.Element(Ns + "BootTrigger") is not null;

        XElement? exec    = root.Element(Ns + "Actions")?.Element(Ns + "Exec");
        string? command   = exec?.Element(Ns + "Command")?.Value?.Trim();
        string? arguments = exec?.Element(Ns + "Arguments")?.Value?.Trim();

        ScheduledTaskDefinition schedTask = new();
        // Not inline declared to assist with debugging.
        schedTask.Author             = root.Element(Ns + "RegistrationInfo")?.Element(Ns + "Author")?.Value;
        schedTask.ExecutablePath     = string.IsNullOrEmpty(command)
            ? null
            : Environment.ExpandEnvironmentVariables(command.Trim('"'));
        schedTask.Arguments          = string.IsNullOrEmpty(arguments)
            ? null
            : Environment.ExpandEnvironmentVariables(arguments);
        schedTask.HasLogonTrigger    = logonTrigger is not null;
        schedTask.HasBootTrigger     = hasBootTrigger;
        schedTask.LogonTriggerUserId = string.IsNullOrEmpty(logonTrigger?.Element(Ns + "UserId")?.Value)
            ? null
            : logonTrigger.Element(Ns + "UserId")!.Value;

        return schedTask;
    }
}
