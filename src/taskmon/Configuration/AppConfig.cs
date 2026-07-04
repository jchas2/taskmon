using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.Process;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls.Chart;

namespace Task.Monitor.Configuration;

public sealed class AppConfig
{
    private readonly IFileSystem fileSystem;
    private Config iniConfig;
    private Theme defaultTheme = new();
    private Layout defaultLayout = new();
    private readonly List<Theme> allThemes = new();
    private readonly List<Layout> allLayouts = new();
    
#if __WIN32__
    private bool useIrixMode = false;
#elif __APPLE__
    private bool useIrixMode = true;
#endif

    private ConfigSection? filterSection;
    private ConfigSection? iterationSection;
    private ConfigSection? sortSection;
    private ConfigSection? statsSection;
    private ConfigSection? uxSection;
    
    private const string ConfigFile = $"{Constants.AppName}.ini";
    
    public AppConfig(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
        this.iniConfig = new();
        LoadSections();
        LoadThemes();
        LoadLayouts();
    }

    public AppConfig(IFileSystem fileSystem, Config iniConfig)
    {
        this.fileSystem = fileSystem;
        this.iniConfig = iniConfig;
        LoadSections();
        LoadThemes();
        LoadLayouts();
    }
    
    public bool ConfirmTaskDelete
    {
        get => uxSection?.GetBool(Constants.Keys.ConfirmTaskDelete, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ConfirmTaskDelete, value.ToString());
    }

    public string? DefaultConfigFilePath
    {
        get {
            string? configPath = DefaultConfigPath;

            return !string.IsNullOrEmpty(configPath) 
                ? Path.Combine(configPath, ConfigFile) 
                : null;
        }
    }

    public string? DefaultConfigPath
    {
        get {
            try {
                string userPath = string.Empty;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config",
                        Constants.AppName);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                        Constants.AppName);
                }

                if (fileSystem.DirectoryExists(userPath)) {
                    return userPath;
                }

                if (fileSystem.TryCreateDirectory(userPath)) {
                    return userPath;
                }

                string appPath = AppContext.BaseDirectory;
                return fileSystem.DirectoryExists(appPath) ? appPath : null;
            }
            catch {
                return null;
            }
        }
    }

    public Layout DefaultLayout
    {
        get => defaultLayout;
        set {
            if (!allLayouts.Contains(value)) {
                throw new InvalidOperationException();
            }

            defaultLayout = value;

            if (iniConfig.ConfigSections.Any(cs => cs.Name.Equals(value.Name, StringComparison.CurrentCultureIgnoreCase))) {
                uxSection?.Add(Constants.Keys.DefaultLayout, value.Name);
            }
        }
    }
    
    public Theme DefaultTheme
    {
        get => defaultTheme;
        set {
            if (!allThemes.Contains(value)) {
                throw new InvalidOperationException();
            }

            defaultTheme = value;
            
            ConsolePalette.PreferIndexedColours = 
                TerminalCapabilities.ResolvePreferIndexed(defaultTheme.ColourMode, Environment.GetEnvironmentVariable);
            
            uxSection?.Add(Constants.Keys.DefaultTheme, defaultTheme.Name);
        }
    }
    
    public int DelayInMilliseconds
    {
        get => statsSection?.GetInt(Constants.Keys.Delay, Processor.DefaultDelayInMilliseconds) ??
               Processor.DefaultDelayInMilliseconds;
        set => statsSection?.Add(Constants.Keys.Delay, value.ToString());
    }

    public int FilterPid
    {
        get => filterSection?.GetInt(Constants.Keys.Pid, -1) ?? -1;
        set => filterSection?.Add(Constants.Keys.Pid, value.ToString());
    }

    public string FilterUserName
    {
        get => filterSection?.GetString(Constants.Keys.UserName, string.Empty) ?? string.Empty;
        set => filterSection?.Add(Constants.Keys.UserName, value);
    }

    public string FilterProcess
    {
        get => filterSection?.GetString(Constants.Keys.Process, string.Empty) ?? string.Empty;
        set => filterSection?.Add(Constants.Keys.Process, value);
    }
    
    public bool HighlightDaemons
    {
        get => uxSection?.GetBool(Constants.Keys.HighlightDaemons, true) ?? true;
        set => uxSection?.Add(Constants.Keys.HighlightDaemons, value.ToString());
    }
    
    public bool HighlightStatisticsColumnUpdate
    {
        get => uxSection?.GetBool(Constants.Keys.HighlightStatsColUpdate, true) ?? true;
        set => uxSection?.Add(Constants.Keys.HighlightStatsColUpdate, value.ToString());
    }

    public MetreControlStyle MetreStyle
    {
        get => uxSection?.GetEnum(Constants.Keys.MetreStyle, MetreControlStyle.Dots) ?? MetreControlStyle.Dots;
        set => uxSection?.Add(Constants.Keys.MetreStyle, value.ToString());
    }
    
    public bool MultiSelectProcesses
    {
        get => uxSection?.GetBool(Constants.Keys.MultiSelectProcesses, false) ?? false;
        set => uxSection?.Add(Constants.Keys.MultiSelectProcesses, value.ToString());
    }

    public int NumberOfProcesses
    {
        get => statsSection?.GetInt(Constants.Keys.NProcs, -1) ?? -1;
        set => statsSection?.Add(Constants.Keys.NProcs, value.ToString());
    }

    public const Statistics DefaultVisibleColumns =
        Statistics.Process | Statistics.Pid | Statistics.User | Statistics.Pri |
        Statistics.Cpu | Statistics.Thrd | Statistics.Gpu | Statistics.Mem |
        Statistics.Path | Statistics.Disk;

    public Statistics VisibleColumns
    {
        get => statsSection?.GetEnum(Constants.Keys.Cols, DefaultVisibleColumns) ?? DefaultVisibleColumns;
        set => statsSection?.Add(Constants.Keys.Cols, value.ToString());
    }

    public Statistics SortColumn
    {
        get => sortSection?.GetEnum(Constants.Keys.Col, Statistics.Cpu) ?? Statistics.Cpu;
        set => sortSection?.Add(Constants.Keys.Col, value.ToString());
    }

    public bool SortAscending
    {
        get => sortSection?.GetBool(Constants.Keys.Asc, false) ?? false;
        set => sortSection?.Add(Constants.Keys.Asc, value.ToString());
    }

    public int IterationLimit
    {
        get => iterationSection?.GetInt(Constants.Keys.Limit, 0) ?? 0;
        set => iterationSection?.Add(Constants.Keys.Limit, value.ToString());
    }
    
    public bool ShowMetreCpuNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreCpuNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreCpuNumerically, value.ToString());
    }

    public bool ShowMetreDiskNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreDiskNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreDiskNumerically, value.ToString());
    }

    public bool ShowMetreGpuNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreGpuNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreGpuNumerically, value.ToString());
    }

    public bool ShowMetreGpuMemNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreGpuMemNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreGpuMemNumerically, value.ToString());
    }

    public bool ShowMetreMemoryNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreMemNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreMemNumerically, value.ToString());
    }
    
    public bool ShowMetreNetworkNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreNetworkNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreNetworkNumerically, value.ToString());
    }

    public bool ShowMetreSwapNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreSwapNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreSwapNumerically, value.ToString());
    }

    public bool UseLargeCharts
    {
        get => uxSection?.GetBool(Constants.Keys.UseLargeCharts, false) ?? false;
        set => uxSection?.Add(Constants.Keys.UseLargeCharts, value.ToString());
    }

    public bool UseIrixReporting
    {
        get => uxSection?.GetBool(Constants.Keys.UseIrixCpuReporting, useIrixMode) ?? useIrixMode;
        set => uxSection?.Add(Constants.Keys.UseIrixCpuReporting, value.ToString());
    }

    private void LoadLayouts()
    {
        bool validLayoutPath = true;
        
        string layoutPath = !string.IsNullOrEmpty(DefaultConfigPath)
            ? Path.Combine(DefaultConfigPath, Constants.LayoutDirectory)
            : string.Empty;

        validLayoutPath = !string.IsNullOrEmpty(layoutPath);
        
        if (validLayoutPath && !fileSystem.DirectoryExists(layoutPath)) {
            if (!fileSystem.TryCreateDirectory(layoutPath)) {
                validLayoutPath = false;
            }
        }

        if (validLayoutPath) {
            string[] layoutFiles = fileSystem.GetFiles(layoutPath);
            
            foreach (string layoutFile in layoutFiles) {
                string layoutText = fileSystem.ReadAllText(layoutFile);
                
                if (!TryParseIni(layoutText, out Layout? layout)) {
                    continue;
                }
                
                if (!allLayouts.Any(t => t.Name.Equals(layout!.Name))) {
                    allLayouts.Add(layout!);
                }
            }
        }
        
        Assembly asm = Assembly.GetExecutingAssembly();
        
        foreach (string name in asm.GetManifestResourceNames()) {
            if (!name.EndsWith(Constants.LayoutExtension)) {
                continue;
            }

            using StreamReader reader = new(asm.GetManifestResourceStream(name)!);
            string layoutText = reader.ReadToEnd();

            if (!TryParseIni(layoutText, out Layout? layout)) {
                Debug.Fail($"Failed to parse manifest asset {name}");
                continue;
            }
            
            if (!allLayouts.Any(t => t.Name.Equals(layout!.Name))) {
                allLayouts.Add(layout!);
                string layoutFilePath = Path.Combine(layoutPath, $"{layout!.Name}{Constants.LayoutExtension}");
                fileSystem.WriteAllText(layoutFilePath, layoutText);
            }
        }

        uxSection = iniConfig.GetConfigSection(Constants.Sections.UX);

        if (allLayouts.Any(t => t.Name.Equals(uxSection.GetString(Constants.Keys.DefaultLayout), StringComparison.CurrentCultureIgnoreCase))) {
            DefaultLayout = allLayouts
                .Where(t => t.Name == uxSection.GetString(Constants.Keys.DefaultLayout))
                .First();
        }
        else {
            // Handle the case where the config file has been edited with a default-layout name that has not been loaded.
            Debug.Assert(allLayouts.Contains(defaultLayout));
            
            if (!allLayouts.Contains(defaultLayout)) {
                allLayouts.Add(defaultLayout);
            }

            DefaultLayout = defaultLayout;
        }
    }
    
    private void LoadSections()
    {
        filterSection = iniConfig.ContainsSection(Constants.Sections.Filter)
            ? iniConfig.GetConfigSection(Constants.Sections.Filter)
            : new ConfigSection(Constants.Sections.Filter);

        filterSection
            .AddIfMissing(Constants.Keys.Pid, "-1")
            .AddIfMissing(Constants.Keys.UserName, string.Empty)
            .AddIfMissing(Constants.Keys.Process, string.Empty);

        if (!iniConfig.ContainsSection(filterSection.Name)) {
            iniConfig.AddConfigSection(filterSection);
        }

        iterationSection = iniConfig.ContainsSection(Constants.Sections.Iterations)
            ? iniConfig.GetConfigSection(Constants.Sections.Iterations)
            : new ConfigSection(Constants.Sections.Iterations);

        iterationSection.AddIfMissing(Constants.Keys.Limit, "0");

        if (!iniConfig.ContainsSection(iterationSection.Name)) {
            iniConfig.AddConfigSection(iterationSection);
        }

        sortSection = iniConfig.ContainsSection(Constants.Sections.Sort)
            ? iniConfig.GetConfigSection(Constants.Sections.Sort)
            : new ConfigSection(Constants.Sections.Sort);

        sortSection
            .AddIfMissing(Constants.Keys.Col, Statistics.Cpu.ToString())
            .AddIfMissing(Constants.Keys.Asc, false.ToString());

        if (!iniConfig.ContainsSection(sortSection.Name)) {
            iniConfig.AddConfigSection(sortSection);
        }
        
        statsSection = iniConfig.ContainsSection(Constants.Sections.Stats)
            ? iniConfig.GetConfigSection(Constants.Sections.Stats)
            : new ConfigSection(Constants.Sections.Stats);

        statsSection
            .AddIfMissing(Constants.Keys.Cols, DefaultVisibleColumns.ToString())
            .AddIfMissing(Constants.Keys.Delay, Processor.DefaultDelayInMilliseconds.ToString())
            .AddIfMissing(Constants.Keys.NProcs, "-1");

        if (!iniConfig.ContainsSection(statsSection.Name)) {
            iniConfig.AddConfigSection(statsSection);
        }
        
        uxSection = iniConfig.ContainsSection(Constants.Sections.UX)
            ? iniConfig.GetConfigSection(Constants.Sections.UX)
            : new ConfigSection(Constants.Sections.UX);

        uxSection
            .AddIfMissing(Constants.Keys.ConfirmTaskDelete, true.ToString())
            .AddIfMissing(Constants.Keys.DefaultLayout, Constants.Sections.LayoutAllCharts)
            .AddIfMissing(Constants.Keys.DefaultTheme, Constants.Sections.ThemeTaskmonDefault)
            .AddIfMissing(Constants.Keys.HighlightDaemons, true.ToString())
            .AddIfMissing(Constants.Keys.HighlightStatsColUpdate, true.ToString())
            .AddIfMissing(Constants.Keys.MetreStyle, MetreControlStyle.Dots.ToString())
            .AddIfMissing(Constants.Keys.MultiSelectProcesses, false.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreCpuNumerically, true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreDiskNumerically, true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreGpuNumerically, true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreMemNumerically, true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreGpuMemNumerically, true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreNetworkNumerically, true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreSwapNumerically, true.ToString())
            .AddIfMissing(Constants.Keys.UseLargeCharts, false.ToString())
            .AddIfMissing(Constants.Keys.UseIrixCpuReporting, useIrixMode.ToString());

        if (!iniConfig.ContainsSection(uxSection.Name)) {
            iniConfig.AddConfigSection(uxSection);
        }
    }

    private void LoadThemes()
    {
        bool validThemePath = true;
        
        string themePath = !string.IsNullOrEmpty(DefaultConfigPath)
            ? Path.Combine(DefaultConfigPath, Constants.ThemeDirectory)
            : string.Empty;

        validThemePath = !string.IsNullOrEmpty(themePath);
        
        if (validThemePath && !fileSystem.DirectoryExists(themePath)) {
            if (!fileSystem.TryCreateDirectory(themePath)) {
                validThemePath = false;
            }
        }

        if (validThemePath) {
            string[] themeFiles = fileSystem.GetFiles(themePath);
            
            foreach (string themeFile in themeFiles) {
                string themeText = fileSystem.ReadAllText(themeFile);
                
                if (!TryParseIni(themeText, out Theme? theme)) {
                    continue;
                }

                theme!.Normalize();
                
                if (!allThemes.Any(t => t.Name.Equals(theme!.Name))) {
                    allThemes.Add(theme!);
                }
            }
        }
        
        Assembly asm = Assembly.GetExecutingAssembly();
        
        foreach (string name in asm.GetManifestResourceNames()) {
            if (!name.EndsWith(Constants.ThemeExtension)) {
                continue;
            }

            using StreamReader reader = new(asm.GetManifestResourceStream(name)!);
            string themeText = reader.ReadToEnd();

            if (!TryParseIni(themeText, out Theme? theme)) {
                Debug.Fail($"Failed to parse manifest asset {name}");
                continue;
            }
            
            theme!.Normalize();
            
            if (!allThemes.Any(t => t.Name.Equals(theme!.Name))) {
                allThemes.Add(theme!);
                string themeFilePath = Path.Combine(themePath, $"{theme!.Name}{Constants.ThemeExtension}");
                fileSystem.WriteAllText(themeFilePath, themeText);
            }
        }

        uxSection = iniConfig.GetConfigSection(Constants.Sections.UX);

        if (allThemes.Any(t => t.Name.Equals(uxSection.GetString(Constants.Keys.DefaultTheme), StringComparison.CurrentCultureIgnoreCase))) {
            DefaultTheme = allThemes
                .Where(t => t.Name == uxSection.GetString(Constants.Keys.DefaultTheme))
                .First();
        }
        else {
            // Handle the case where the config file has been edited with a default-theme name that has not been loaded.
            Debug.Assert(allThemes.Contains(defaultTheme));
            
            if (!allThemes.Contains(defaultTheme)) {
                allThemes.Add(defaultTheme);
            }

            DefaultTheme = defaultTheme;
        }
    }

    public List<Layout> Layouts => allLayouts;

    public List<Theme> Themes => allThemes;

    public bool TryLoad(Config config)
    {
        try {
            iniConfig = config;
            LoadSections();
            LoadThemes();
            return true;
        }
        catch (Exception ex) {
            ExceptionHelper.HandleException(ex);
            return false;
        }
    }
    
    public bool TryLoad(string path)
    {
        try {
            Config config = Config.FromFile(fileSystem, path);
            return TryLoad(config);
        }
        catch (Exception ex) when (ex is FileNotFoundException || ex is IOException) {
            ExceptionHelper.HandleException(ex, $"Error loading config: ${ex.Message}.");
        }
        catch (Exception ex) when (ex is ConfigParseException) {
            ExceptionHelper.HandleException(ex, $"Error parsing config: {ex.Message}.");
        }

        return false;
    }

    private bool TryParseIni<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        string text, out T? instance) where T : class
    {
        instance = null;

        try {
            ConfigParser parser = new(text);
            parser.Parse();
            instance = (T?)Activator.CreateInstance(typeof(T), parser.Sections[0]);
            return true;
        }
        catch (Exception ex) {
            ExceptionHelper.HandleException(ex);
            return false;
        }
    }

    public bool TrySave(string path)
    {
        try {
            Config.ToFile(fileSystem, path, iniConfig);
            return true;
        }
        catch (Exception ex) {
            ExceptionHelper.HandleException(ex, $"Error saving config: {ex.Message} to path {path}");
            return false;
        }
    }
}
