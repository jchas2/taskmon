using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Health;

namespace Task.Monitor.Configuration;

public sealed class AppConfig
{
    private const string ConfigFile             = $"{Constants.AppName}.ini";
    private const string ConfigTemplateResource = $"Task.Monitor.Assets.Config.{ConfigFile}";

    private readonly IFileSystem                fileSystem;
    private readonly List<Theme>                allThemes  = new();
    private readonly List<SummaryControlLayout> allLayouts = new();
    
    private Config                iniConfig;
    private Config                savedConfig;
    private SummaryControlLayout? defaultLayout;
    private Theme                 defaultTheme = new();
    
#if __WIN32__
    private bool useIrixMode = false;
#elif __APPLE__
    private bool useIrixMode = true;
#endif

    private ConfigSection? filterSection;
    private ConfigSection? healthSection;
    private ConfigSection? sortSection;
    private ConfigSection? statsSection;
    private ConfigSection? uxSection;

    public const Statistics DefaultVisibleColumns =
        Statistics.Process | 
        Statistics.Pid     | 
        Statistics.User    | 
        Statistics.Pri     |
        Statistics.Cpu     | 
        Statistics.Thrd    | 
        Statistics.Gpu     | 
        Statistics.Mem     |
        Statistics.Path    | 
        Statistics.Disk;

    public AppConfig(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
        iniConfig = new();
        LoadSections();
        LoadThemes();
        LoadLayouts();
        SelectConfiguredTheme();
        SelectConfiguredLayout();
        savedConfig = CopyConfig();
    }

    private AppConfig(AppConfig source)
    {
        fileSystem = source.fileSystem;
        iniConfig  = source.CopyConfig();
        LoadSections();

        allThemes.AddRange(source.allThemes);
        allLayouts.AddRange(source.allLayouts);

        defaultLayout = source.defaultLayout;
        defaultTheme  = source.defaultTheme;
        savedConfig   = CopyConfig();
    }

    public bool ConfirmTaskDelete
    {
        get => uxSection?.GetBool(Constants.Keys.ConfirmTaskDelete, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ConfirmTaskDelete, value.ToString());
    }

    private Config CopyConfig() => Config.FromString(ToString()) ?? new();

    public AppConfig CreatePreview() => new(this);

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
                        Constants.AppName, Constants.ConfigVersionDirectory);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        Constants.AppName, Constants.ConfigVersionDirectory);
                }

                return DefaultPath(fileSystem, userPath);
            }
            catch (Exception ex) {
                ExceptionHelper.LogException(ex);
                return null;
            }
        }
    }

    public string? DefaultEventsPath
    {
        get {
            try {
                string eventsPath = string.Empty;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    eventsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library",
                        "Logs", Constants.AppName, Constants.EventsFolder);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    eventsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        Constants.AppName, Constants.EventsFolder);
                }

                if (eventsPath.Length == 0 ||
                    !(fileSystem.DirectoryExists(eventsPath) || TryCreateOwnedDirectory(fileSystem, eventsPath))) {

                    return null;
                }

                PathPermissions.EnsureUserOwnership(eventsPath);
                return eventsPath;
            }
            catch (Exception ex) {
                ExceptionHelper.LogException(ex);
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

    private static string? DefaultPath(IFileSystem fileSystem, string desiredPath)
    {
        try {
            if (fileSystem.DirectoryExists(desiredPath)) {
                PathPermissions.EnsureUserOwnership(desiredPath);
                return desiredPath;
            }

            if (TryCreateOwnedDirectory(fileSystem, desiredPath)) {
                return desiredPath;
            }
                
            string tempPath = Path.GetTempPath();
                
            if (fileSystem.DirectoryExists(tempPath) || fileSystem.TryCreateDirectory(tempPath)) {
                PathPermissions.EnsureUserOwnership(tempPath);
                return tempPath;
            }
                
            return null;
            
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
            return null;
        }
    }

    public int DelayInMilliseconds
    {
        get => statsSection?.GetInt(Constants.Keys.Delay, WorkerService.DefaultDelayInMilliseconds) ??
               WorkerService.DefaultDelayInMilliseconds;
        set => statsSection?.Add(Constants.Keys.Delay, value.ToString());
    }

    private void DeployBuiltInCopies(string? folderPath, string extension, Dictionary<string, string> builtIns)
    {
        if (folderPath == null ||
            GetOrCreateDirectory(Path.Combine(folderPath, Constants.BuiltInDirectory)) is not { } builtInPath) {

            return;
        }

        foreach ((string fileName, string text) in builtIns) {
            DeployIfChanged(Path.Combine(builtInPath, fileName), text);
        }

        foreach (string file in fileSystem.GetFiles(builtInPath, $"*{extension}")) {
            if (builtIns.ContainsKey(Path.GetFileName(file))) {
                continue;
            }

            try {
                Trace.WriteLine($"Deleting {file}: not a built-in");
                fileSystem.DeleteFile(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                Trace.WriteLine($"Failed to delete {file}: {ex.Message}");
            }
        }
    }

    private void DeployIfChanged(string filePath, string text)
    {
        if (fileSystem.FileExists(filePath) && fileSystem.ReadAllText(filePath) == text) {
            return;
        }

        Trace.WriteLine($"Creating/Overwriting {filePath}");
        fileSystem.WriteAllText(filePath, text);
        PathPermissions.EnsureUserOwnership(filePath);
    }

    public int FilterPid
    {
        get => filterSection?.GetInt(Constants.Keys.Pid, -1) ?? -1;
        set => filterSection?.Add(Constants.Keys.Pid, value.ToString());
    }

    public string FilterProcess
    {
        get => filterSection?.GetString(Constants.Keys.Process, string.Empty) ?? string.Empty;
        set => filterSection?.Add(Constants.Keys.Process, value);
    }

    public string FilterUserName
    {
        get => filterSection?.GetString(Constants.Keys.UserName, string.Empty) ?? string.Empty;
        set => filterSection?.Add(Constants.Keys.UserName, value);
    }

    private string? GetConfigSubdirectory(string directoryName) =>
        DefaultConfigPath is { Length: > 0 } configPath
            ? GetOrCreateDirectory(Path.Combine(configPath, directoryName))
            : null;

    public static string? GetDefaultLogPath(IFileSystem fileSystem)
    {
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

            return DefaultPath(fileSystem, logPath);
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
            return null;
        }
    }

    private string? GetOrCreateDirectory(string path) =>
        fileSystem.DirectoryExists(path) || TryCreateOwnedDirectory(fileSystem, path)
            ? path
            : null;

    public HealthThresholds HealthThresholds
    {
        get {
            HealthThresholds defaults = new();

            if (healthSection is not { } section) {
                return defaults;
            }

            return new HealthThresholds {
                ClearSeconds                   = section.GetInt(Constants.Keys.HealthClearSeconds,                   defaults.ClearSeconds),
                CommitCriticalPercent          = section.GetInt(Constants.Keys.HealthCommitCriticalPercent,          defaults.CommitCriticalPercent),
                CommitWarningPercent           = section.GetInt(Constants.Keys.HealthCommitWarningPercent,           defaults.CommitWarningPercent),
                CpuCriticalPercent             = section.GetInt(Constants.Keys.HealthCpuCriticalPercent,             defaults.CpuCriticalPercent),
                CpuWarningPercent              = section.GetInt(Constants.Keys.HealthCpuWarningPercent,              defaults.CpuWarningPercent),
                CriticalSustainSeconds         = section.GetInt(Constants.Keys.HealthCriticalSustainSeconds,         defaults.CriticalSustainSeconds),
                DiskActiveWarningPercent       = section.GetInt(Constants.Keys.HealthDiskActiveWarningPercent,       defaults.DiskActiveWarningPercent),
                DiskLatencyCriticalMs          = section.GetInt(Constants.Keys.HealthDiskLatencyCriticalMs,          defaults.DiskLatencyCriticalMs),
                DiskLatencyWarningMs           = section.GetInt(Constants.Keys.HealthDiskLatencyWarningMs,           defaults.DiskLatencyWarningMs),
                DriverTimeWarningPercent       = section.GetInt(Constants.Keys.HealthDriverTimeWarningPercent,       defaults.DriverTimeWarningPercent),
                EventHistorySize               = section.GetInt(Constants.Keys.HealthEventHistorySize,               defaults.EventHistorySize),
                GpuMemoryFreeWarningPercent    = section.GetInt(Constants.Keys.HealthGpuMemoryFreeWarningPercent,    defaults.GpuMemoryFreeWarningPercent),
                HardFaultsAvailablePercent     = section.GetInt(Constants.Keys.HealthHardFaultsAvailablePercent,     defaults.HardFaultsAvailablePercent),
                HardFaultsWarningPerSecond     = section.GetInt(Constants.Keys.HealthHardFaultsWarningPerSecond,     defaults.HardFaultsWarningPerSecond),
                MemoryAvailableCriticalPercent = section.GetInt(Constants.Keys.HealthMemoryAvailableCriticalPercent, defaults.MemoryAvailableCriticalPercent),
                MemoryAvailableWarningPercent  = section.GetInt(Constants.Keys.HealthMemoryAvailableWarningPercent,  defaults.MemoryAvailableWarningPercent),
                NotRespondingCriticalSeconds   = section.GetInt(Constants.Keys.HealthNotRespondingCriticalSeconds,   defaults.NotRespondingCriticalSeconds),
                NotRespondingWarningSeconds    = section.GetInt(Constants.Keys.HealthNotRespondingWarningSeconds,    defaults.NotRespondingWarningSeconds),
                NvmeWearWarningPercent         = section.GetInt(Constants.Keys.HealthNvmeWearWarningPercent,         defaults.NvmeWearWarningPercent),
                PerformanceLimitWarningPercent = section.GetInt(Constants.Keys.HealthPerformanceLimitWarningPercent, defaults.PerformanceLimitWarningPercent),
                ReportLookbackHours            = section.GetInt(Constants.Keys.HealthReportLookbackHours,            defaults.ReportLookbackHours),
                RunQueueBusyPercent            = section.GetInt(Constants.Keys.HealthRunQueueBusyPercent,            defaults.RunQueueBusyPercent),
                RunQueueWarningPerCore         = section.GetInt(Constants.Keys.HealthRunQueueWarningPerCore,         defaults.RunQueueWarningPerCore),
                SensorCriticalMarginCelsius    = section.GetInt(Constants.Keys.HealthSensorCriticalMarginCelsius,    defaults.SensorCriticalMarginCelsius),
                SensorWarningMarginCelsius     = section.GetInt(Constants.Keys.HealthSensorWarningMarginCelsius,     defaults.SensorWarningMarginCelsius),
                SustainGraceSeconds            = section.GetInt(Constants.Keys.HealthSustainGraceSeconds,            defaults.SustainGraceSeconds),
                SwapOutWarningMbPerSecond      = section.GetInt(Constants.Keys.HealthSwapOutWarningMbPerSecond,      defaults.SwapOutWarningMbPerSecond),
                SystemVolumeFreeCriticalGb     = section.GetInt(Constants.Keys.HealthSystemVolumeFreeCriticalGb,     defaults.SystemVolumeFreeCriticalGb),
                ThermalPressureCritical        = section.GetEnum(Constants.Keys.HealthThermalPressureCritical,       defaults.ThermalPressureCritical),
                ThermalPressureWarning         = section.GetEnum(Constants.Keys.HealthThermalPressureWarning,        defaults.ThermalPressureWarning),
                VolumeFreeCriticalPercent      = section.GetInt(Constants.Keys.HealthVolumeFreeCriticalPercent,      defaults.VolumeFreeCriticalPercent),
                VolumeFreeWarningPercent       = section.GetInt(Constants.Keys.HealthVolumeFreeWarningPercent,       defaults.VolumeFreeWarningPercent),
                WarningSustainSeconds          = section.GetInt(Constants.Keys.HealthWarningSustainSeconds,          defaults.WarningSustainSeconds)
            }.Validated();
        }
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

    public bool IsBuiltInLayout(string name) =>
        allLayouts.Any(t => t.IsBuiltIn && t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

    private static bool IsCurrentVersion(ConfigSection section, int currentVersion, string source)
    {
        string version = section.GetString(Constants.Keys.Version);

        if (int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out int found) &&
            found == currentVersion) {

            return true;
        }

        Trace.WriteLine(version.Length == 0
            ? $"Skipping {source}: no version, expected {currentVersion}"
            : $"Skipping {source}: version {version}, expected {currentVersion}");

        return false;
    }

    private bool IsLayoutNameTaken(string name) =>
        allLayouts.Any(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

    private bool IsThemeNameTaken(string name) =>
        allThemes.Any(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

    public string? LastSaveError { get; private set; }

    public List<SummaryControlLayout> Layouts => allLayouts;

    private void LoadLayouts()
    {
        string? layoutPath = GetConfigSubdirectory(Constants.LayoutDirectory);

        Dictionary<string, string> builtIns = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string asset, string layoutText) in ReadAssets(Constants.LayoutExtension)) {
            if (!TryParseIniSection(layoutText, out ConfigSection? section) ||
                !IsCurrentVersion(section!, SummaryControlLayout.CurrentVersion, asset)) {

                Debug.Fail($"Failed to load manifest asset {asset}");
                continue;
            }

            SummaryControlLayout layout = new(section!) { IsBuiltIn = true };

            if (!IsLayoutNameTaken(layout.Name)) {
                allLayouts.Add(layout);
            }

            builtIns[$"{layout.Name}{Constants.LayoutExtension}"] = layoutText;
        }

        DeployBuiltInCopies(layoutPath, Constants.LayoutExtension, builtIns);

        foreach ((string layoutFile, ConfigSection section) in ReadUserFiles(
            layoutPath, Constants.LayoutExtension, SummaryControlLayout.CurrentVersion)) {

            if (IsLayoutNameTaken(section.Name)) {
                Trace.WriteLine($"Skipping {layoutFile}: a layout named {section.Name} is already loaded");
                continue;
            }

            allLayouts.Add(new SummaryControlLayout(section));
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

        healthSection = iniConfig.ContainsSection(Constants.Sections.Health)
            ? iniConfig.GetConfigSection(Constants.Sections.Health)
            : new ConfigSection(Constants.Sections.Health);

        HealthThresholds defaults = new();

        healthSection
            .AddIfMissing(Constants.Keys.HealthWarningSustainSeconds,          defaults.WarningSustainSeconds.ToString())
            .AddIfMissing(Constants.Keys.HealthCriticalSustainSeconds,         defaults.CriticalSustainSeconds.ToString())
            .AddIfMissing(Constants.Keys.HealthSustainGraceSeconds,            defaults.SustainGraceSeconds.ToString())
            .AddIfMissing(Constants.Keys.HealthClearSeconds,                   defaults.ClearSeconds.ToString())
            .AddIfMissing(Constants.Keys.HealthEventHistorySize,               defaults.EventHistorySize.ToString())
            .AddIfMissing(Constants.Keys.HealthCpuWarningPercent,              defaults.CpuWarningPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthCpuCriticalPercent,             defaults.CpuCriticalPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthMemoryAvailableWarningPercent,  defaults.MemoryAvailableWarningPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthMemoryAvailableCriticalPercent, defaults.MemoryAvailableCriticalPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthSwapOutWarningMbPerSecond,      defaults.SwapOutWarningMbPerSecond.ToString())
            .AddIfMissing(Constants.Keys.HealthDiskActiveWarningPercent,       defaults.DiskActiveWarningPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthVolumeFreeWarningPercent,       defaults.VolumeFreeWarningPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthVolumeFreeCriticalPercent,      defaults.VolumeFreeCriticalPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthSystemVolumeFreeCriticalGb,     defaults.SystemVolumeFreeCriticalGb.ToString())
            .AddIfMissing(Constants.Keys.HealthSensorWarningMarginCelsius,     defaults.SensorWarningMarginCelsius.ToString())
            .AddIfMissing(Constants.Keys.HealthSensorCriticalMarginCelsius,    defaults.SensorCriticalMarginCelsius.ToString())
            .AddIfMissing(Constants.Keys.HealthThermalPressureWarning,         defaults.ThermalPressureWarning.ToString())
            .AddIfMissing(Constants.Keys.HealthThermalPressureCritical,        defaults.ThermalPressureCritical.ToString())
            .AddIfMissing(Constants.Keys.HealthGpuMemoryFreeWarningPercent,    defaults.GpuMemoryFreeWarningPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthCommitWarningPercent,           defaults.CommitWarningPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthCommitCriticalPercent,          defaults.CommitCriticalPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthHardFaultsWarningPerSecond,     defaults.HardFaultsWarningPerSecond.ToString())
            .AddIfMissing(Constants.Keys.HealthHardFaultsAvailablePercent,     defaults.HardFaultsAvailablePercent.ToString())
            .AddIfMissing(Constants.Keys.HealthRunQueueWarningPerCore,         defaults.RunQueueWarningPerCore.ToString())
            .AddIfMissing(Constants.Keys.HealthRunQueueBusyPercent,            defaults.RunQueueBusyPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthPerformanceLimitWarningPercent, defaults.PerformanceLimitWarningPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthDriverTimeWarningPercent,       defaults.DriverTimeWarningPercent.ToString())
            .AddIfMissing(Constants.Keys.HealthDiskLatencyWarningMs,           defaults.DiskLatencyWarningMs.ToString())
            .AddIfMissing(Constants.Keys.HealthDiskLatencyCriticalMs,          defaults.DiskLatencyCriticalMs.ToString())
            .AddIfMissing(Constants.Keys.HealthReportLookbackHours,            defaults.ReportLookbackHours.ToString())
            .AddIfMissing(Constants.Keys.HealthNotRespondingWarningSeconds,    defaults.NotRespondingWarningSeconds.ToString())
            .AddIfMissing(Constants.Keys.HealthNotRespondingCriticalSeconds,   defaults.NotRespondingCriticalSeconds.ToString())
            .AddIfMissing(Constants.Keys.HealthNvmeWearWarningPercent,         defaults.NvmeWearWarningPercent.ToString());

        if (!iniConfig.ContainsSection(healthSection.Name)) {
            iniConfig.AddConfigSection(healthSection);
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
            .AddIfMissing(Constants.Keys.Delay, WorkerService.DefaultDelayInMilliseconds.ToString());

        if (!iniConfig.ContainsSection(statsSection.Name)) {
            iniConfig.AddConfigSection(statsSection);
        }
        
        uxSection = iniConfig.ContainsSection(Constants.Sections.UX)
            ? iniConfig.GetConfigSection(Constants.Sections.UX)
            : new ConfigSection(Constants.Sections.UX);

        uxSection
            .AddIfMissing(Constants.Keys.ConfirmTaskDelete,           true.ToString())
            .AddIfMissing(Constants.Keys.DefaultTheme,                Constants.Sections.ThemeTaskmonDefault)
            .AddIfMissing(Constants.Keys.DefaultSummaryLayout,        Constants.Sections.LayoutDefault)
            .AddIfMissing(Constants.Keys.HighlightDaemons,            true.ToString())
            .AddIfMissing(Constants.Keys.HighlightStatsColUpdate,     true.ToString())
            .AddIfMissing(Constants.Keys.MetreStyle,                  MetreControlStyle.Dots.ToString())
            .AddIfMissing(Constants.Keys.MultiSelectProcesses,        false.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreCpuNumerically,     true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreDiskNumerically,    true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreGpuNumerically,     true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreMemNumerically,     true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreGpuMemNumerically,  true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreNetworkNumerically, true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreNpuNumerically,     true.ToString())
            .AddIfMissing(Constants.Keys.ShowMetreSwapNumerically,    true.ToString())
            .AddIfMissing(Constants.Keys.ShowSmallMetreGrid,          false.ToString())
            .AddIfMissing(Constants.Keys.ShowLargeMetreGrid,          false.ToString())
            .AddIfMissing(Constants.Keys.ShowTips,                    true.ToString())
            .AddIfMissing(Constants.Keys.ShowYAxisScale,              true.ToString())
            .AddIfMissing(Constants.Keys.UiLanguage,                  string.Empty)
            .AddIfMissing(Constants.Keys.UseIrixCpuReporting,         useIrixMode.ToString());

        if (!iniConfig.ContainsSection(uxSection.Name)) {
            iniConfig.AddConfigSection(uxSection);
        }
    }

    private void LoadThemes()
    {
        string? themePath = GetConfigSubdirectory(Constants.ThemeDirectory);

        Dictionary<string, string> builtIns = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string asset, string themeText) in ReadAssets(Constants.ThemeExtension)) {
            if (!TryParseIniSection(themeText, out ConfigSection? section) ||
                !IsCurrentVersion(section!, Theme.CurrentVersion, asset)) {

                Debug.Fail($"Failed to load manifest asset {asset}");
                continue;
            }

            Theme theme = new(section!);
            theme.Normalize();

            if (!IsThemeNameTaken(theme.Name)) {
                allThemes.Add(theme);
            }

            builtIns[$"{theme.Name}{Constants.ThemeExtension}"] = themeText;
        }

        DeployBuiltInCopies(themePath, Constants.ThemeExtension, builtIns);

        foreach ((string themeFile, ConfigSection section) in ReadUserFiles(
            themePath, Constants.ThemeExtension, Theme.CurrentVersion)) {

            if (IsThemeNameTaken(section.Name)) {
                Trace.WriteLine($"Skipping {themeFile}: a theme named {section.Name} is already loaded");
                continue;
            }

            Theme theme = new(section);
            theme.Normalize();
            allThemes.Add(theme);
        }
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

    private static IEnumerable<(string Name, string Text)> ReadAssets(string extension)
    {
        Assembly asm = Assembly.GetExecutingAssembly();

        foreach (string name in asm.GetManifestResourceNames()) {
            if (!name.EndsWith(extension)) {
                continue;
            }

            using StreamReader reader = new(asm.GetManifestResourceStream(name)!);

            Trace.WriteLine($"Parsing asset {name}");
            yield return (name, reader.ReadToEnd());
        }
    }

    private static string ReadTemplate()
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ConfigTemplateResource);

        if (stream == null) {
            Debug.Fail($"Missing manifest asset {ConfigTemplateResource}");
            return string.Empty;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd().ReplaceLineEndings();
    }

    private List<(string File, ConfigSection Section)> ReadUserFiles(string? folderPath, string extension, int currentVersion)
    {
        List<(string File, ConfigSection Section)> files = new();

        if (folderPath == null) {
            return files;
        }

        foreach (string file in fileSystem.GetFiles(folderPath, $"*{extension}")) {
            string text;

            try {
                text = fileSystem.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                Trace.WriteLine($"Skipping {file}: {ex.Message}");
                continue;
            }

            Trace.WriteLine($"Parsing file {file}");

            if (!TryParseIniSection(text, out ConfigSection? section)) {
                Trace.WriteLine($"Failed to parse: \n{text}\n");
                continue;
            }

            if (!IsCurrentVersion(section!, currentVersion, file)) {
                continue;
            }

            if (!Path.GetFileNameWithoutExtension(file).Equals(section!.Name, StringComparison.OrdinalIgnoreCase)) {
                Trace.WriteLine($"{file} is named {section.Name} by its header");
            }

            files.Add((file, section));
        }

        return files;
    }

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

            // Re-saving the layout currently in use replaces its entry above - re-point the default
            // at the new instance, or it would keep serving the pre-edit copy.
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

    private void SelectConfiguredLayout()
    {
        uxSection = iniConfig.GetConfigSection(Constants.Sections.UX);

        string preferredTree = uxSection.GetString(Constants.Keys.DefaultSummaryLayout);

        SummaryControlLayout? configured = allLayouts.FirstOrDefault(
            t => t.Name.Equals(preferredTree, StringComparison.CurrentCultureIgnoreCase));

        if (configured != null) {
            DefaultLayout = configured;
        }
        else if (allLayouts.Count > 0 && defaultLayout == null) {
            DefaultLayout = allLayouts.FirstOrDefault(
                t => t.Name.Equals(Constants.Sections.LayoutDefault, StringComparison.CurrentCultureIgnoreCase))
                ?? allLayouts[0];
        }
    }

    private void SelectConfiguredTheme()
    {
        uxSection = iniConfig.GetConfigSection(Constants.Sections.UX);

        if (allThemes.Any(t => t.Name.Equals(uxSection.GetString(Constants.Keys.DefaultTheme), StringComparison.CurrentCultureIgnoreCase))) {
            Theme = allThemes.First(t => t.Name.Equals(
                uxSection.GetString(Constants.Keys.DefaultTheme), StringComparison.CurrentCultureIgnoreCase));
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

    public bool ShowLargeMetreGrid
    {
        get => uxSection?.GetBool(Constants.Keys.ShowLargeMetreGrid, false) ?? false;
        set => uxSection?.Add(Constants.Keys.ShowLargeMetreGrid, value.ToString());
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

    public bool ShowMetreGpuMemNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreGpuMemNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreGpuMemNumerically, value.ToString());
    }

    public bool ShowMetreGpuNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreGpuNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreGpuNumerically, value.ToString());
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

    public bool ShowMetreNpuNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreNpuNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreNpuNumerically, value.ToString());
    }

    public bool ShowMetreSwapNumerically
    {
        get => uxSection?.GetBool(Constants.Keys.ShowMetreSwapNumerically, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowMetreSwapNumerically, value.ToString());
    }

    public bool ShowSmallMetreGrid
    {
        get => uxSection?.GetBool(Constants.Keys.ShowSmallMetreGrid, false) ?? false;
        set => uxSection?.Add(Constants.Keys.ShowSmallMetreGrid, value.ToString());
    }

    public bool ShowTips
    {
        get => uxSection?.GetBool(Constants.Keys.ShowTips, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowTips, value.ToString());
    }

    public bool ShowYAxisScale
    {
        get => uxSection?.GetBool(Constants.Keys.ShowYAxisScale, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ShowYAxisScale, value.ToString());
    }

    public bool SortAscending
    {
        get => sortSection?.GetBool(Constants.Keys.Asc, false) ?? false;
        set => sortSection?.Add(Constants.Keys.Asc, value.ToString());
    }

    public Statistics SortColumn
    {
        get => sortSection?.GetEnum(Constants.Keys.Col, Statistics.Cpu) ?? Statistics.Cpu;
        set => sortSection?.Add(Constants.Keys.Col, value.ToString());
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

    public List<Theme> Themes => allThemes;

    public override string ToString()
    {
        StringBuilder buffer = new(1024 * iniConfig.ConfigSections.Count);
        
        foreach (ConfigSection section in iniConfig.ConfigSections) {
            buffer.AppendLine(section.ToString());
        }

        return buffer.ToString();
    } 

    private static bool TryCreateOwnedDirectory(IFileSystem fileSystem, string path)
    {
        if (path.Length == 0) {
            return false;
        }

        // Undo sudo we have to apply user ownership at each level of the dir path,
        // to ensure none is owned by root, otherwise non-root will fail on next startup.
        Stack<string> missing = new();

        for (string? folder = path;
            !string.IsNullOrEmpty(folder) && !fileSystem.DirectoryExists(folder);
            folder = Path.GetDirectoryName(folder)) {

            missing.Push(folder);
        }

        while (missing.TryPop(out string? folder)) {
            if (!fileSystem.TryCreateDirectory(folder)) {
                return false;
            }
            
            PathPermissions.EnsureUserOwnership(folder);
        }

        return true;
    }

    public bool TryLoad(Config config)
    {
        try {
            iniConfig = config;
            LoadSections();
            SelectConfiguredTheme();
            SelectConfiguredLayout();
            savedConfig = CopyConfig();
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

    public bool TrySave(string path)
    {
        LastSaveError = null;

        try {
            bool   fileExists = fileSystem.FileExists(path);
            string template   = ReadTemplate();
            string fileText   = fileExists ? fileSystem.ReadAllText(path) : template;

            Config fileConfig = Config.FromString(fileText) ?? new();

            if (!fileExists) {
                Trace.WriteLine($"Creating {path} from the built-in template");
            }

            string merged = ConfigFileMerger.Merge(fileText, fileExists ? savedConfig : fileConfig, iniConfig, template);
            _ = Config.FromString(merged);

            if (!fileExists || merged != fileText) {
                WriteConfigFile(path, merged, fileExists);
            }

            savedConfig = CopyConfig();
            return true;
        }
        catch (ConfigParseException ex) {
            LastSaveError = string.Format(CultureInfo.CurrentCulture, Strings.ConfigSaveParseError, ConfigFile, ex.Message);
            ExceptionHelper.LogException(ex, $"{ConfigFile} has an error and was not saved.\n{ex.Message}");
            return false;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error saving config: {ex.Message} to path {path}");
            return false;
        }
    }

    public string UiLanguage
    {
        get => uxSection?.GetString(Constants.Keys.UiLanguage, string.Empty) ?? string.Empty;
        set => uxSection?.Add(Constants.Keys.UiLanguage, value);
    }

    public bool UseIrixReporting
    {
        get => uxSection?.GetBool(Constants.Keys.UseIrixCpuReporting, useIrixMode) ?? useIrixMode;
        set => uxSection?.Add(Constants.Keys.UseIrixCpuReporting, value.ToString());
    }

    public Statistics VisibleColumns
    {
        get => statsSection?.GetEnum(Constants.Keys.Cols, DefaultVisibleColumns) ?? DefaultVisibleColumns;
        set => statsSection?.Add(Constants.Keys.Cols, value.ToString());
    }

    private void WriteConfigFile(string path, string text, bool fileExists)
    {
        if (fileExists) {
            fileSystem.ReplaceAllText(path, text);
        }
        else {
            fileSystem.WriteAllText(path, text);
        }

        PathPermissions.EnsureUserOwnership(path);
    }
}
