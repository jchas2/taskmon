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
    private SummaryControlLayout? defaultLayout;
    private readonly List<Theme> allThemes = new();
    private readonly List<SummaryControlLayout> allLayouts = new();
    
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
    
    public SummaryControlLayout? DefaultLayout
    {
        get => defaultLayout;
        set {
            if (value != null && !allLayouts.Contains(value)) {
                throw new InvalidOperationException();
            }

            defaultLayout = value;
            uxSection?.Add(Constants.Keys.DefaultSummaryLayout, value?.Name ?? string.Empty);
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

    // Built-in layouts are the ones shipped as embedded resources. They are loaded first and win
    // by name, and their copies on disk are kept in step with the running version, the same as
    // themes. Custom layouts are the ones saved from the layout designer, loaded from the same
    // folder. The folder is summary-layouts, not version 1's layouts folder, which this version
    // never reads or writes, so both versions can be installed side by side.
    private void LoadLayouts()
    {
        string? layoutPath = GetConfigSubdirectory(Constants.LayoutDirectory);
        Assembly asm = Assembly.GetExecutingAssembly();

        foreach (string name in asm.GetManifestResourceNames()) {
            if (!name.EndsWith(Constants.LayoutExtension)) {
                continue;
            }

            using StreamReader reader = new(asm.GetManifestResourceStream(name)!);
            string layoutText = reader.ReadToEnd();
            Trace.WriteLine($"Parsing asset {name}");

            if (!TryParseIniSection(layoutText, out ConfigSection? section)) {
                Debug.Fail($"Failed to parse manifest asset {name}");
                continue;
            }

            SummaryControlLayout layout = new(section!) { IsBuiltIn = true };

            if (!layout.IsTreeLayout) {
                Debug.Fail($"Manifest asset {name} is not a tree layout");
                continue;
            }

            if (!allLayouts.Any(t => t.Name.Equals(layout.Name))) {
                allLayouts.Add(layout);
            }

            if (layoutPath != null) {
                DeployIfChanged(Path.Combine(layoutPath, $"{layout.Name}{Constants.LayoutExtension}"), layoutText);
            }
        }

        if (layoutPath != null) {
            foreach (string layoutFile in fileSystem.GetFiles(layoutPath)) {
                if (!layoutFile.EndsWith(Constants.LayoutExtension, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                // A built-in layout's name: the deployed copy, already loaded from the manifest.
                if (IsLayoutNameTaken(Path.GetFileNameWithoutExtension(layoutFile))) {
                    continue;
                }

                string layoutText = fileSystem.ReadAllText(layoutFile);
                Trace.WriteLine($"Parsing file {layoutFile}");

                if (!TryParseIniSection(layoutText, out ConfigSection? section)) {
                    Trace.WriteLine($"Failed to parse: \n{layoutText}\n");
                    continue;
                }

                SummaryControlLayout layout = new(section!);

                if (!layout.IsTreeLayout) {
                    Trace.WriteLine($"Skipping {layoutFile}: not a tree layout");
                    continue;
                }

                if (!IsLayoutNameTaken(layout.Name)) {
                    allLayouts.Add(layout);
                }
            }
        }

        uxSection = iniConfig.GetConfigSection(Constants.Sections.UX);

        string preferredTree = uxSection.GetString(Constants.Keys.DefaultSummaryLayout);

        SummaryControlLayout? configured = allLayouts.FirstOrDefault(
            t => t.Name.Equals(preferredTree, StringComparison.CurrentCultureIgnoreCase));

        if (configured != null) {
            DefaultLayout = configured;
        }
        else if (allLayouts.Count > 0 && defaultLayout == null) {
            // No saved preference (or it named a layout that no longer exists): the shipped
            // "All Charts" rather than whichever tree happened to be found first, which with
            // user-saved layouts on disk could be anything.
            DefaultLayout = allLayouts.FirstOrDefault(
                    t => t.Name.Equals(Constants.Sections.LayoutAllCharts, StringComparison.CurrentCultureIgnoreCase))
                ?? allLayouts[0];
        }
    }

    private bool IsLayoutNameTaken(string name) =>
        allLayouts.Any(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

    // The named folder under the user's config folder, created if it is missing. Null when there
    // is no usable config folder, or the folder can't be created.
    private string? GetConfigSubdirectory(string directoryName)
    {
        string? configPath = DefaultConfigPath;

        if (string.IsNullOrEmpty(configPath)) {
            return null;
        }

        string path = Path.Combine(configPath, directoryName);

        if (fileSystem.DirectoryExists(path)) {
            return path;
        }

        if (!fileSystem.TryCreateDirectory(path)) {
            return null;
        }

        PathPermissions.EnsureUserOwnership(path);
        return path;
    }

    // Writes a shipped (embedded) theme or layout to disk when the copy there is missing or its
    // contents differ, so the file a user sees always matches the version they are running.
    private void DeployIfChanged(string filePath, string text)
    {
        if (fileSystem.FileExists(filePath) && fileSystem.ReadAllText(filePath) == text) {
            return;
        }

        Trace.WriteLine($"Creating/Overwriting {filePath}");
        fileSystem.WriteAllText(filePath, text);
        PathPermissions.EnsureUserOwnership(filePath);
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
            .AddIfMissing(Constants.Keys.UseIrixCpuReporting, useIrixMode.ToString());

        // Earlier version 2 builds stored the default layout under default-summary-layout2: carry
        // that choice over, then drop the old key. Version 1's default-layout key is left as it is
        // for a version 1 install to keep using - its layouts aren't loaded here.
        if (uxSection.Contains(Constants.Keys.DefaultSummaryLayout2)) {
            uxSection.AddIfMissing(
                Constants.Keys.DefaultSummaryLayout,
                uxSection.GetString(Constants.Keys.DefaultSummaryLayout2));

            uxSection.Remove(Constants.Keys.DefaultSummaryLayout2);
        }

        if (!iniConfig.ContainsSection(uxSection.Name)) {
            iniConfig.AddConfigSection(uxSection);
        }
    }

    private void LoadThemes()
    {
        string? themePath = GetConfigSubdirectory(Constants.ThemeDirectory);

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

            if (themePath != null) {
                DeployIfChanged(Path.Combine(themePath, $"{theme.Name}{Constants.ThemeExtension}"), themeText);
            }
        }

        if (themePath != null) {
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

    public List<SummaryControlLayout> Layouts => allLayouts;

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

    // Layout files need their raw ConfigSection to check they're a tree layout before loading them
    // (see AddParsedLayout) - unlike TryParseIni<T>, which commits to a concrete type up front.
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

    // Built-in layouts are read-only: they are refreshed from the embedded copy on every start, so
    // an edit saved under one of their names would be replaced on the next run.
    public bool IsBuiltInLayout(string name) =>
        allLayouts.Any(t => t.IsBuiltIn && t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

    // Writes a layout created or edited in the layout designer to the layouts folder, and makes it
    // available alongside the loaded ones. Refuses a built-in layout's name (see IsBuiltInLayout);
    // the designer checks for that first, to tell the user why.
    public bool SaveLayout(SummaryControlLayout layout)
    {
        if (IsBuiltInLayout(layout.Name)) {
            return false;
        }

        try {
            string? layoutPath = GetConfigSubdirectory(Constants.LayoutDirectory);

            if (layoutPath == null) {
                return false;
            }

            string layoutFilePath = Path.Combine(layoutPath, $"{layout.Name}{Constants.LayoutExtension}");
            fileSystem.WriteAllText(layoutFilePath, layout.ToString());
            PathPermissions.EnsureUserOwnership(layoutFilePath);

            SummaryControlLayout? existing = allLayouts.FirstOrDefault(
                t => t.Name.Equals(layout.Name, StringComparison.CurrentCultureIgnoreCase));

            if (existing != null) {
                allLayouts.Remove(existing);
            }

            allLayouts.Add(layout);

            // Re-saving the layout currently in use replaces its entry above - repoint the default
            // at the new instance, or it would keep serving the pre-edit copy (SummaryControl2
            // reloads when the default instance changes, so this is also what shows the edits).
            if (existing != null && existing == defaultLayout) {
                defaultLayout = layout;
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
