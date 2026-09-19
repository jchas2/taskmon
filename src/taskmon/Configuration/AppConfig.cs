using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;

namespace Task.Monitor.Configuration;

public sealed class AppConfig
{
    private readonly IFileSystem fileSystem;
    private Config iniConfig;
    private Theme defaultTheme = new();
    private Layout defaultLayout = new();
    private SummaryLayout2? defaultSummaryLayout2;
    private readonly List<Theme> allThemes = new();
    private readonly List<Layout> allLayouts = new();
    private readonly List<SummaryLayout2> allSummaryLayouts2 = new();
    
#if __WIN32__
    private bool useIrixMode = false;
#elif __APPLE__
    private bool useIrixMode = true;
#endif

    private ConfigSection? filterSection;
    private ConfigSection? sortSection;
    private ConfigSection? statsSection;
    private ConfigSection? uxSection;
    private const string ConfigFile = $"{Constants.AppName}.ini";
    
    public AppConfig(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
        iniConfig = new();
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

                if (fileSystem.DirectoryExists(userPath) || fileSystem.TryCreateDirectory(userPath)) {
                    PathPermissions.EnsureUserOwnership(userPath);
                    return userPath;
                }

                string tempPath = Path.GetTempPath();
                
                if (fileSystem.DirectoryExists(tempPath) || fileSystem.TryCreateDirectory(tempPath)) {
                    PathPermissions.EnsureUserOwnership(tempPath);
                    return tempPath;
                }
                
                return null;
            }
            catch {
                return null;
            }
        }
    }

    public string? DefaultLogPath
    {
        get {
            try {
                string logPath = string.Empty;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library",
                        "Logs", Constants.AppName);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        Constants.AppName, "Logs");
                }

                if (fileSystem.DirectoryExists(logPath) || fileSystem.TryCreateDirectory(logPath)) {
                    PathPermissions.EnsureUserOwnership(logPath);
                    return logPath;
                }
                
                string tempPath = Path.GetTempPath();
                
                if (fileSystem.DirectoryExists(tempPath) || fileSystem.TryCreateDirectory(tempPath)) {
                    PathPermissions.EnsureUserOwnership(tempPath);
                    return tempPath;
                }
                
                return null;
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
    
    // Null until either a tree layout has been saved this run (SaveSummaryLayout2) or one was
    // found on disk/in the default-summary-layout2 preference at load time - SummaryControl2
    // falls back to SummaryLayoutTree.CreateExample() when this is null.
    public SummaryLayout2? DefaultSummaryLayout2
    {
        get => defaultSummaryLayout2;
        set {
            if (value != null && !allSummaryLayouts2.Contains(value)) {
                throw new InvalidOperationException();
            }

            defaultSummaryLayout2 = value;
            uxSection?.Add(Constants.Keys.DefaultSummaryLayout2, value?.Name ?? string.Empty);
        }
    }

    public Theme Theme
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
        get => statsSection?.GetInt(Constants.Keys.Delay, WorkerService.DefaultDelayInMilliseconds) ??
               WorkerService.DefaultDelayInMilliseconds;
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

    public bool ShowYAxisScale
    {
        get => uxSection?.GetBool(Constants.Keys.ShowYAxisScale, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowYAxisScale, value.ToString());
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

    public bool ShowSmallMetreGrid
    {
        get => uxSection?.GetBool(Constants.Keys.ShowSmallMetreGrid, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowSmallMetreGrid, value.ToString());
    }

    public bool ShowLargeMetreGrid
    {
        get => uxSection?.GetBool(Constants.Keys.ShowLargeMetreGrid, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowLargeMetreGrid, value.ToString());
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
            if (fileSystem.TryCreateDirectory(layoutPath)) {
                PathPermissions.EnsureUserOwnership(layoutPath);
            }
            else {
                validLayoutPath = false;
            }
        }

        if (validLayoutPath) {
            string[] layoutFiles = fileSystem.GetFiles(layoutPath);

            foreach (string layoutFile in layoutFiles) {
                string layoutText = fileSystem.ReadAllText(layoutFile);
                Trace.WriteLine($"Parsing {layoutFile}");

                if (!TryParseIniSection(layoutText, out ConfigSection? section)) {
                    Trace.WriteLine($"Failed to parse: \n{layoutText}\n");
                    continue;
                }

                AddParsedLayout(section!, layoutPath, layoutText, deploy: false);
            }
        }

        Assembly asm = Assembly.GetExecutingAssembly();

        foreach (string name in asm.GetManifestResourceNames()) {
            if (!name.EndsWith(Constants.LayoutExtension)) {
                continue;
            }

            using StreamReader reader = new(asm.GetManifestResourceStream(name)!);
            string layoutText = reader.ReadToEnd();

            if (!TryParseIniSection(layoutText, out ConfigSection? section)) {
                Debug.Fail($"Failed to parse manifest asset {name}");
                continue;
            }

            AddParsedLayout(section!, layoutPath, layoutText, deploy: true);
        }

        uxSection = iniConfig.GetConfigSection(Constants.Sections.UX);

        if (allLayouts.Any(t => t.Name.Equals(uxSection.GetString(Constants.Keys.DefaultLayout), StringComparison.CurrentCultureIgnoreCase))) {
            DefaultLayout = allLayouts
                .Where(t => t.Name == uxSection.GetString(Constants.Keys.DefaultLayout))
                .First();
        }
        else {
            // No grid layout by the configured name - the normal case now that every shipped
            // layout is a SummaryLayout2 tree (only a hand-kept grid file on disk would match),
            // so this is not an assert. The unnamed built-in Layout (2 x 4 charts, all eight) keeps
            // the legacy fixed-grid SummaryControl working if MainScreen2 is switched back to it.
            if (!allLayouts.Contains(defaultLayout)) {
                allLayouts.Add(defaultLayout);
            }

            DefaultLayout = defaultLayout;
        }

        string preferredTree = uxSection.GetString(Constants.Keys.DefaultSummaryLayout2);

        SummaryLayout2? configured = allSummaryLayouts2.FirstOrDefault(
            t => t.Name.Equals(preferredTree, StringComparison.CurrentCultureIgnoreCase));

        if (configured != null) {
            DefaultSummaryLayout2 = configured;
        }
        else if (allSummaryLayouts2.Count > 0 && defaultSummaryLayout2 == null) {
            // No saved preference (or it named a layout that no longer exists): the shipped
            // "All Charts" - the same default the legacy grid used - rather than whichever tree
            // happened to be found first, which with user-saved layouts on disk could be anything.
            DefaultSummaryLayout2 = allSummaryLayouts2.FirstOrDefault(
                    t => t.Name.Equals(Constants.Sections.LayoutAllCharts, StringComparison.CurrentCultureIgnoreCase))
                ?? allSummaryLayouts2[0];
        }
    }

    // A layout file (on disk or embedded) is either a fixed-grid Layout or a SummaryLayout2 tree
    // - never both - decided purely by the layout-type key (SummaryLayout2.IsTreeLayout). deploy
    // is only true for the embedded-manifest pass: a name already present (found on disk, or an
    // earlier manifest entry) is left alone rather than overwritten with the shipped copy.
    private void AddParsedLayout(ConfigSection section, string layoutPath, string sourceText, bool deploy)
    {
        if (new SummaryLayout2(section).IsTreeLayout) {
            if (allSummaryLayouts2.Any(t => t.Name.Equals(section.Name))) {
                return;
            }

            allSummaryLayouts2.Add(new SummaryLayout2(section));
        }
        else {
            Layout layout = new(section);

            if (allLayouts.Any(t => t.Name.Equals(layout.Name))) {
                return;
            }

            allLayouts.Add(layout);
        }

        if (deploy) {
            string layoutFilePath = Path.Combine(layoutPath, $"{section.Name}{Constants.LayoutExtension}");
            fileSystem.WriteAllText(layoutFilePath, sourceText);
            PathPermissions.EnsureUserOwnership(layoutFilePath);
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
            .AddIfMissing(Constants.Keys.Delay, WorkerService.DefaultDelayInMilliseconds.ToString())
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
            .AddIfMissing(Constants.Keys.ShowSmallMetreGrid, false.ToString())
            .AddIfMissing(Constants.Keys.ShowLargeMetreGrid, true.ToString())
            .AddIfMissing(Constants.Keys.ShowYAxisScale, true.ToString())
            .AddIfMissing(Constants.Keys.UseLargeCharts, false.ToString())
            .AddIfMissing(Constants.Keys.UseIrixCpuReporting, useIrixMode.ToString());

        if (!iniConfig.ContainsSection(uxSection.Name)) {
            iniConfig.AddConfigSection(uxSection);
        }
    }

    private void LoadThemes()
    {
        string themePath = !string.IsNullOrEmpty(DefaultConfigPath)
            ? Path.Combine(DefaultConfigPath, Constants.ThemeDirectory)
            : string.Empty;

        bool validThemePath = !string.IsNullOrEmpty(themePath);
        
        if (validThemePath && !fileSystem.DirectoryExists(themePath)) {
            if (fileSystem.TryCreateDirectory(themePath)) {
                PathPermissions.EnsureUserOwnership(themePath);
            }
            else {
                validThemePath = false;
            }            
        }

        Assembly asm = Assembly.GetExecutingAssembly();
        
        // Load default/shipped themes.
        foreach (string name in asm.GetManifestResourceNames()) {
            if (!name.EndsWith(Constants.ThemeExtension)) {
                continue;
            }

            using StreamReader reader = new(asm.GetManifestResourceStream(name)!);
            string themeText = reader.ReadToEnd();
            Trace.WriteLine($"Parsing asset {name}");

            if (!TryParseIni(themeText, out Theme? theme)) {
                Debug.Fail($"Failed to parse manifest asset {name}");
                continue;
            }
            
            theme!.Normalize();
            
            if (!allThemes.Any(t => t.Name.Equals(theme.Name))) {
                allThemes.Add(theme);
            }

            if (validThemePath) {
                string themeFilePath = Path.Combine(themePath, $"{theme!.Name}{Constants.ThemeExtension}");

                if (!fileSystem.FileExists(themeFilePath) || fileSystem.GetFileLength(themeFilePath) != themeText.Length) {
                    Trace.WriteLine($"Creating/Overwriting {themeFilePath}");
                    fileSystem.WriteAllText(themeFilePath, themeText);
                    PathPermissions.EnsureUserOwnership(themeFilePath);
                }
            }
        }

        if (validThemePath) {
            // Load custom theme files from disk into allThemes array.
            string[] themeFiles = fileSystem.GetFiles(themePath);
            
            foreach (string themeFile in themeFiles) {
                string themeName = Path.GetFileNameWithoutExtension(themeFile);

                if (allThemes.Any(t => t.Name.Equals(themeName))) {
                    continue;
                }
                
                string themeText = fileSystem.ReadAllText(themeFile);
                Trace.WriteLine($"Parsing file {themeFile}");

                if (!TryParseIni(themeText, out Theme? theme)) {
                    Trace.WriteLine($"Failed to parse: \n{themeText}\n");
                    continue;
                }

                theme!.Normalize();
                allThemes.Add(theme!);
            }
        }

        uxSection = iniConfig.GetConfigSection(Constants.Sections.UX);

        if (allThemes.Any(t => t.Name.Equals(uxSection.GetString(Constants.Keys.DefaultTheme), StringComparison.CurrentCultureIgnoreCase))) {
            Theme = allThemes
                .Where(t => t.Name == uxSection.GetString(Constants.Keys.DefaultTheme))
                .First();
        }
        else {
            // Handle the case where the config file has been edited with a default-theme name that has not been loaded.
            Debug.Assert(allThemes.Contains(defaultTheme));
            
            if (!allThemes.Contains(defaultTheme)) {
                allThemes.Add(defaultTheme);
            }

            Theme = defaultTheme;
        }
    }

    public List<Layout> Layouts => allLayouts;

    public List<SummaryLayout2> SummaryLayouts2 => allSummaryLayouts2;

    public List<Theme> Themes => allThemes;

    public override string ToString()
    {
        StringBuilder buffer = new(1024 * iniConfig.ConfigSections.Count);
        
        foreach (ConfigSection section in iniConfig.ConfigSections) {
            buffer.AppendLine(section.ToString());
        }

        return buffer.ToString();
    } 

    public bool TryLoad(Config config)
    {
        try {
            iniConfig = config;
            LoadSections();
            LoadThemes();
            LoadLayouts();
            return true;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
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
            ExceptionHelper.LogException(ex, $"Error loading config: ${ex.Message}.");
        }
        catch (Exception ex) when (ex is ConfigParseException) {
            ExceptionHelper.LogException(ex, $"Error parsing config: {ex.Message}.");
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
            ExceptionHelper.LogException(ex, $"Error parsing Ini: {ex.Message}");
            return false;
        }
    }

    // Layout files need their raw ConfigSection before deciding whether they're a grid Layout or
    // a SummaryLayout2 tree (see AddParsedLayout) - unlike TryParseIni<T>, which commits to a
    // concrete type up front.
    private bool TryParseIniSection(string text, out ConfigSection? section)
    {
        section = null;

        try {
            ConfigParser parser = new(text);
            parser.Parse();
            section = parser.Sections[0];
            return true;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error parsing Ini: {ex.Message}");
            return false;
        }
    }

    // There is no equivalent save path for the fixed-grid Layout format - those are only ever
    // deployed once from embedded resources (see LoadLayouts) and never written back. Tree
    // layouts need one because Milestone 4/5's designer is how they're created in the first
    // place, with nothing to embed ahead of time.
    public bool SaveSummaryLayout2(SummaryLayout2 layout)
    {
        try {
            string layoutPath = !string.IsNullOrEmpty(DefaultConfigPath)
                ? Path.Combine(DefaultConfigPath, Constants.LayoutDirectory)
                : string.Empty;

            if (string.IsNullOrEmpty(layoutPath)) {
                return false;
            }

            if (!fileSystem.DirectoryExists(layoutPath) && !fileSystem.TryCreateDirectory(layoutPath)) {
                return false;
            }

            string layoutFilePath = Path.Combine(layoutPath, $"{layout.Name}{Constants.LayoutExtension}");
            fileSystem.WriteAllText(layoutFilePath, layout.ToString());
            PathPermissions.EnsureUserOwnership(layoutFilePath);

            SummaryLayout2? existing = allSummaryLayouts2.FirstOrDefault(
                t => t.Name.Equals(layout.Name, StringComparison.CurrentCultureIgnoreCase));

            if (existing != null) {
                allSummaryLayouts2.Remove(existing);
            }

            allSummaryLayouts2.Add(layout);

            // Re-saving the layout currently in use replaces its entry above - repoint the default
            // at the new instance, or it would keep serving the pre-edit copy (SummaryControl2
            // reloads when the default instance changes, so this is also what shows the edits).
            if (existing != null && existing == defaultSummaryLayout2) {
                defaultSummaryLayout2 = layout;
            }

            return true;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error saving summary layout: {ex.Message}");
            return false;
        }
    }

    public bool TrySave(string path)
    {
        try {
            Config.ToFile(fileSystem, path, iniConfig);
            PathPermissions.EnsureUserOwnership(path);
            return true;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error saving config: {ex.Message} to path {path}");
            return false;
        }
    }
}
