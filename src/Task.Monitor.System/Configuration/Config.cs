using System.Text;
using Task.Monitor.Internal.Abstractions;

namespace Task.Monitor.System.Configuration;

public class Config
{
    private IList<ConfigSection> configSections;

    public Config() =>
        configSections = new List<ConfigSection>();

    public Config AddConfigSection(ConfigSection section)
    {
        if (ContainsSection(section.Name)) {
            throw new InvalidOperationException($"A section with name {section.Name} already exists.");
        }
        
        ConfigSections.Add(section);
        return this;
    }
    
    public IList<ConfigSection> ConfigSections
    {
        get => configSections;
        private set => configSections = value;
    }

    public bool ContainsSection(string name) =>
        configSections.Any(s => s.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

    public static Config FromFile(IFileSystem fileSys, string path)
    {
        ConfigParser parser = new(fileSys, path);
        return ParseConfig(parser);
    }

    public static Config? FromString(string str)
    {
        ConfigParser parser = new(str);
        return ParseConfig(parser);
    }
    
    public ConfigSection GetConfigSection(string name)
    {
        if (!ContainsSection(name)) {
            throw new InvalidOperationException($"Section name {name} not found.");
        }

        return configSections.Single(s => s.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
    }
    
    private static Config ParseConfig(ConfigParser parser)
    {
        Config config = new();
        parser.Parse(); 
        config.ConfigSections = parser.Sections;
        return config;
    }

    public static void ToFile(IFileSystem fileSys, string path, Config config)
    {
        StringBuilder buffer = new(1024 * 16);
        
        foreach (ConfigSection configSection in config.ConfigSections) {
            buffer.AppendLine(configSection.ToString());
        }        

        fileSys.WriteAllText(path, buffer.ToString());
    }
}
