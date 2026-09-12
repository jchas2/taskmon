using System.Xml.Linq;

namespace Task.Monitor.System.Services.Startup;

// Parses the XML a registered task returns from IRegisteredTask::Xml (the
// http://schemas.microsoft.com/windows/2004/02/mit/task schema) into just what the Startup screen
// needs: the first Exec action, whether it fires at logon or boot, and who it runs for.
public sealed class ScheduledTaskDefinition
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public string? Author { get; private init; }
    public string? ExecutablePath { get; private init; }
    public string? Arguments { get; private init; }

    // The LogonTrigger's UserId, when one is present and non-empty. Null means either no
    // LogonTrigger, or one that fires for every user (an empty UserId means "any user").
    public string? LogonTriggerUserId { get; private init; }
    public bool HasBootTrigger { get; private init; }
    public bool HasLogonTrigger { get; private init; }

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

        XElement? triggers = root.Element(Ns + "Triggers");
        XElement? logonTrigger = triggers?.Element(Ns + "LogonTrigger");
        bool hasBootTrigger = triggers?.Element(Ns + "BootTrigger") is not null;

        XElement? exec = root.Element(Ns + "Actions")?.Element(Ns + "Exec");
        string? command = exec?.Element(Ns + "Command")?.Value?.Trim();
        string? arguments = exec?.Element(Ns + "Arguments")?.Value?.Trim();

        return new ScheduledTaskDefinition {
            Author = root.Element(Ns + "RegistrationInfo")?.Element(Ns + "Author")?.Value,
            ExecutablePath = string.IsNullOrEmpty(command)
                ? null
                : Environment.ExpandEnvironmentVariables(command.Trim('"')),
            Arguments = string.IsNullOrEmpty(arguments)
                ? null
                : Environment.ExpandEnvironmentVariables(arguments),
            HasLogonTrigger = logonTrigger is not null,
            HasBootTrigger = hasBootTrigger,
            LogonTriggerUserId = string.IsNullOrEmpty(logonTrigger?.Element(Ns + "UserId")?.Value)
                ? null
                : logonTrigger.Element(Ns + "UserId")!.Value
        };
    }
}
