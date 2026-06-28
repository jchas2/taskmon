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
    
    private const string ConfigFile = "taskmon.ini";
    
    private readonly string[,] colourMap = {
        { Constants.Keys.Background,            "transparent" },
        { Constants.Keys.BackgroundHighlight,   "#00FFFF"        },
        { Constants.Keys.ColCmdNormalUserSpace, "#00FF00"       },
        { Constants.Keys.ColCmdLowPriority,     "#0000FF"        },
        { Constants.Keys.ColCmdHighCpu,         "#FF0000"         },
        { Constants.Keys.ColCmdIoBound,         "#00FFFF"        },
        { Constants.Keys.ColCmdScript,          "#FFFF00"      },
        { Constants.Keys.ColUserCurrentNonRoot, "#00FF00"       },
        { Constants.Keys.ColUserOtherNonRoot,   "#FF00FF"     },
        { Constants.Keys.ColUserSystem,         "#C0C0C0"        },
        { Constants.Keys.ColUserRoot,           "#FFFFFF"       },
        { Constants.Keys.CommandForeground,     "#000000"       },
        { Constants.Keys.CommandBackground,     "#00FFFF"        },
        { Constants.Keys.Error,                 "#FF0000"         },
        { Constants.Keys.Foreground,            "#FFFFFF"       },
        { Constants.Keys.ForegroundHighlight,   "#000000"       },
        { Constants.Keys.MenubarForeground,     "#FFFFFF"       },
        { Constants.Keys.MenubarBackground,     "#000080"    },
        { Constants.Keys.RangeHighBackground,   "#FF0000"         },
        { Constants.Keys.RangeLowBackground,    "#00FF00"       },
        { Constants.Keys.RangeMidBackground,    "#FFFF00"      },
        { Constants.Keys.RangeHighForeground,   "#FFFFFF"       },
        { Constants.Keys.RangeLowForeground,    "#FFFFFF"       },
        { Constants.Keys.RangeMidForeground,    "#808000"  },
        { Constants.Keys.HeaderBackground,      "#008000"   },
        { Constants.Keys.HeaderForeground,      "#000000"       }};

    private readonly string[,] monoMap = {
        { Constants.Keys.Background,            "transparent" },
        { Constants.Keys.BackgroundHighlight,   "#808080"    },
        { Constants.Keys.ColCmdNormalUserSpace, "#C0C0C0"        },
        { Constants.Keys.ColCmdLowPriority,     "#808080"    },
        { Constants.Keys.ColCmdHighCpu,         "#FFFFFF"       },
        { Constants.Keys.ColCmdIoBound,         "#FFFFFF"       },
        { Constants.Keys.ColCmdScript,          "#808080"    },
        { Constants.Keys.ColUserCurrentNonRoot, "#808080"    },
        { Constants.Keys.ColUserOtherNonRoot,   "#808080"    },
        { Constants.Keys.ColUserSystem,         "#C0C0C0"        },
        { Constants.Keys.ColUserRoot,           "#FFFFFF"       },
        { Constants.Keys.CommandForeground,     "#000000"       },
        { Constants.Keys.CommandBackground,     "#C0C0C0"        },
        { Constants.Keys.Error,                 "#C0C0C0"        },
        { Constants.Keys.Foreground,            "#808080"    },
        { Constants.Keys.ForegroundHighlight,   "#FFFFFF"       },
        { Constants.Keys.MenubarForeground,     "#FFFFFF"       },
        { Constants.Keys.MenubarBackground,     "#C0C0C0"        },
        { Constants.Keys.RangeHighBackground,   "#C0C0C0"        },
        { Constants.Keys.RangeLowBackground,    "#C0C0C0"        },
        { Constants.Keys.RangeMidBackground,    "#C0C0C0"        },
        { Constants.Keys.RangeHighForeground,   "#808080"    },
        { Constants.Keys.RangeLowForeground,    "#808080"    },
        { Constants.Keys.RangeMidForeground,    "#808080"    },
        { Constants.Keys.HeaderBackground,      "#808080"    },
        { Constants.Keys.HeaderForeground,      "#FFFFFF"       }};

    private readonly string[,] msDosMap = {
        { Constants.Keys.Background,            "#000080"    },
        { Constants.Keys.BackgroundHighlight,   "#00FFFF"        },
        { Constants.Keys.ColCmdNormalUserSpace, "#FFFF00"      },
        { Constants.Keys.ColCmdLowPriority,     "#C0C0C0"        },
        { Constants.Keys.ColCmdHighCpu,         "#FF0000"         },
        { Constants.Keys.ColCmdIoBound,         "#FF0000"         },
        { Constants.Keys.ColCmdScript,          "#FFFF00"      },
        { Constants.Keys.ColUserCurrentNonRoot, "#C0C0C0"        },
        { Constants.Keys.ColUserOtherNonRoot,   "#808080"    },
        { Constants.Keys.ColUserSystem,         "#FFFF00"      },
        { Constants.Keys.ColUserRoot,           "#FF0000"         },
        { Constants.Keys.CommandForeground,     "#FFFF00"      },
        { Constants.Keys.CommandBackground,     "#008080"    },
        { Constants.Keys.Error,                 "#FF0000"         },
        { Constants.Keys.Foreground,            "#808080"    },
        { Constants.Keys.ForegroundHighlight,   "#000000"       },
        { Constants.Keys.MenubarForeground,     "#FFFF00"      },
        { Constants.Keys.MenubarBackground,     "#008080"    },
        { Constants.Keys.RangeHighBackground,   "#FF0000"         },
        { Constants.Keys.RangeLowBackground,    "#00FF00"       },
        { Constants.Keys.RangeMidBackground,    "#FFFF00"      },
        { Constants.Keys.RangeHighForeground,   "#FF0000"         },
        { Constants.Keys.RangeLowForeground,    "#00FFFF"        },
        { Constants.Keys.RangeMidForeground,    "#FFFF00"      },
        { Constants.Keys.HeaderBackground,      "#008080"    },
        { Constants.Keys.HeaderForeground,      "#FFFF00"      }};

    private readonly string[,] tokyoNightMap = {
        { Constants.Keys.Background,            "transparent" },
        { Constants.Keys.BackgroundHighlight,   "#00FFFF"        },
        { Constants.Keys.ColCmdNormalUserSpace, "#808080"    },
        { Constants.Keys.ColCmdLowPriority,     "#C0C0C0"        },
        { Constants.Keys.ColCmdHighCpu,         "#FF0000"         },
        { Constants.Keys.ColCmdIoBound,         "#00FFFF"        },
        { Constants.Keys.ColCmdScript,          "#FFFF00"      },
        { Constants.Keys.ColUserCurrentNonRoot, "#FFFF00"      },
        { Constants.Keys.ColUserOtherNonRoot,   "#FF00FF"     },
        { Constants.Keys.ColUserSystem,         "#C0C0C0"        },
        { Constants.Keys.ColUserRoot,           "#FFFFFF"       },
        { Constants.Keys.CommandForeground,     "#FF00FF"     },
        { Constants.Keys.CommandBackground,     "#000080"    },
        { Constants.Keys.Error,                 "#FF0000"         },
        { Constants.Keys.Foreground,            "#00FFFF"        },
        { Constants.Keys.ForegroundHighlight,   "#800080" },
        { Constants.Keys.MenubarForeground,     "#FF00FF"     },
        { Constants.Keys.MenubarBackground,     "#000080"    },
        { Constants.Keys.RangeHighBackground,   "#FF0000"         },
        { Constants.Keys.RangeLowBackground,    "#FF00FF"     },
        { Constants.Keys.RangeMidBackground,    "#FF00FF"     },
        { Constants.Keys.RangeHighForeground,   "#00FFFF"        },
        { Constants.Keys.RangeLowForeground,    "#00FFFF"        },
        { Constants.Keys.RangeMidForeground,    "#00FFFF"        },
        { Constants.Keys.HeaderBackground,      "#0000FF"        },
        { Constants.Keys.HeaderForeground,      "#FF00FF"     }};

    private readonly string[,] matrixMap = {
        { Constants.Keys.Background,            "transparent" },
        { Constants.Keys.BackgroundHighlight,   "#00FF00"       },
        { Constants.Keys.ColCmdNormalUserSpace, "#00FF00"       },
        { Constants.Keys.ColCmdLowPriority,     "#008000"   },
        { Constants.Keys.ColCmdHighCpu,         "#00FF00"       },
        { Constants.Keys.ColCmdIoBound,         "#00FF00"       },
        { Constants.Keys.ColCmdScript,          "#008000"   },
        { Constants.Keys.ColUserCurrentNonRoot, "#008000"   },
        { Constants.Keys.ColUserOtherNonRoot,   "#008000"   },
        { Constants.Keys.ColUserSystem,         "#C0C0C0"        },
        { Constants.Keys.ColUserRoot,           "#00FF00"       },
        { Constants.Keys.CommandForeground,     "#000000"       },
        { Constants.Keys.CommandBackground,     "#008000"   },
        { Constants.Keys.Error,                 "#FF0000"         },
        { Constants.Keys.Foreground,            "#00FF00"       },
        { Constants.Keys.ForegroundHighlight,   "#000000"       },
        { Constants.Keys.MenubarForeground,     "#000000"       },
        { Constants.Keys.MenubarBackground,     "#008000"   },
        { Constants.Keys.RangeHighBackground,   "#008000"   },
        { Constants.Keys.RangeLowBackground,    "#00FF00"       },
        { Constants.Keys.RangeMidBackground,    "#008000"   },
        { Constants.Keys.RangeHighForeground,   "#000000"       },
        { Constants.Keys.RangeLowForeground,    "#000000"       },
        { Constants.Keys.RangeMidForeground,    "#008000"   },
        { Constants.Keys.HeaderBackground,      "#00FF00"       },
        { Constants.Keys.HeaderForeground,      "#000000"       }};
    
    private readonly string[,] solarMap = {
        { Constants.Keys.Background,            "transparent" },
        { Constants.Keys.BackgroundHighlight,   "#FFFF00"      },
        { Constants.Keys.ColCmdNormalUserSpace, "#808080"    },
        { Constants.Keys.ColCmdLowPriority,     "#808000"  },
        { Constants.Keys.ColCmdHighCpu,         "#FF0000"         },
        { Constants.Keys.ColCmdIoBound,         "#808000"  },
        { Constants.Keys.ColCmdScript,          "#FFFF00"      },
        { Constants.Keys.ColUserCurrentNonRoot, "#FFFF00"      },
        { Constants.Keys.ColUserOtherNonRoot,   "#FFFF00"      },
        { Constants.Keys.ColUserSystem,         "#FFFFFF"       },
        { Constants.Keys.ColUserRoot,           "#FFFFFF"       },
        { Constants.Keys.CommandForeground,     "#000000"       },
        { Constants.Keys.CommandBackground,     "#808000"  },
        { Constants.Keys.Error,                 "#FF0000"         },
        { Constants.Keys.Foreground,            "#FFFF00"      },
        { Constants.Keys.ForegroundHighlight,   "#000000"       },
        { Constants.Keys.MenubarForeground,     "#000000"       },
        { Constants.Keys.MenubarBackground,     "#808000"  },
        { Constants.Keys.RangeHighBackground,   "#FF0000"         },
        { Constants.Keys.RangeLowBackground,    "#FFFF00"      },
        { Constants.Keys.RangeMidBackground,    "#808000"  },
        { Constants.Keys.RangeHighForeground,   "#000000"       },
        { Constants.Keys.RangeLowForeground,    "#000000"       },
        { Constants.Keys.RangeMidForeground,    "#808000"  },
        { Constants.Keys.HeaderBackground,      "#808000"  },
        { Constants.Keys.HeaderForeground,      "#000000"       }};

    private readonly string[,] layoutAll = {
        { Constants.Keys.Ratio,   "0.4" },
        { Constants.Keys.NumRows, "2" },
        { Constants.Keys.NumCols, "4" },
        { Constants.Keys.Charts,  "0,1,2,3,4,5,6,7" }
    };
    
    private readonly string[,] layoutAllLarge = {
        { Constants.Keys.Ratio,   "0.75" },
        { Constants.Keys.NumRows, "2" },
        { Constants.Keys.NumCols, "4" },
        { Constants.Keys.Charts,  "0,1,2,3,4,5,6,7" }
    };

    private readonly string[,] layoutCpuAndMemory = {
        { Constants.Keys.Ratio,   "0.4" },
        { Constants.Keys.NumRows, "1" },
        { Constants.Keys.NumCols, "2" },
        { Constants.Keys.Charts,  "0,4" }
    };

    private readonly string[,] layoutCpuAndMemoryLarge = {
        { Constants.Keys.Ratio,   "0.75" },
        { Constants.Keys.NumRows, "1" },
        { Constants.Keys.NumCols, "2" },
        { Constants.Keys.Charts,  "0,4" }
    };

    private readonly string[,] layoutGpuAndMemory = {
        { Constants.Keys.Ratio,   "0.4" },
        { Constants.Keys.NumRows, "1" },
        { Constants.Keys.NumCols, "2" },
        { Constants.Keys.Charts,  "1,5" }
    };
    
    private readonly string[,] layoutGpuAndMemoryLarge = {
        { Constants.Keys.Ratio,   "0.75" },
        { Constants.Keys.NumRows, "1" },
        { Constants.Keys.NumCols, "2" },
        { Constants.Keys.Charts,  "1,5" }
    };
    
    private readonly string[,] layoutNetSendReceive = {
        { Constants.Keys.Ratio,   "0.4" },
        { Constants.Keys.NumRows, "1" },
        { Constants.Keys.NumCols, "2" },
        { Constants.Keys.Charts,  "3,7" }
    };
    
    private readonly string[,] layoutNetSendReceiveLarge = {
        { Constants.Keys.Ratio,   "0.75" },
        { Constants.Keys.NumRows, "1" },
        { Constants.Keys.NumCols, "2" },
        { Constants.Keys.Charts,  "3,7" }
    };

    private readonly string[,] layoutDisk = {
        { Constants.Keys.Ratio,   "0.4" },
        { Constants.Keys.NumRows, "1" },
        { Constants.Keys.NumCols, "1" },
        { Constants.Keys.Charts,  "2" }
    };
    
    private readonly string[,] layoutDiskLarge = {
        { Constants.Keys.Ratio,   "0.75" },
        { Constants.Keys.NumRows, "1" },
        { Constants.Keys.NumCols, "1" },
        { Constants.Keys.Charts,  "2" }
    };
    
    public AppConfig(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
        this.iniConfig = new();
        LoadSections();
    }

    public AppConfig(IFileSystem fileSystem, Config iniConfig)
    {
        this.fileSystem = fileSystem;
        this.iniConfig = iniConfig;
        LoadSections();
    }

    public ColourMode ColourMode
    {
        get => uxSection?.GetEnum(Constants.Keys.ColourMode, ColourMode.Auto) ?? ColourMode.Auto;
        set => uxSection?.Add(Constants.Keys.ColourMode, value.ToString());
    }

    public bool ConfirmTaskDelete
    {
        get => uxSection?.GetBool(Constants.Keys.ConfirmTaskDelete, true) ?? true;
        set => uxSection?.Add(Constants.Keys.ConfirmTaskDelete, value.ToString());
    }

    public string? DefaultConfigPath
    {
        get {
            try {
                return Path.Combine(AppContext.BaseDirectory, ConfigFile);
            }
            catch (Exception ex) {
                ExceptionHelper.HandleException(ex);
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
            
            if (iniConfig.ConfigSections.Any(cs => cs.Name.Equals(value.Name, StringComparison.CurrentCultureIgnoreCase))) {
                uxSection?.Add(Constants.Keys.DefaultTheme, value.Name);
            }
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
            .AddIfMissing(Constants.Keys.ColourMode, ColourMode.Auto.ToString())
            .AddIfMissing(Constants.Keys.ConfirmTaskDelete, true.ToString())
            .AddIfMissing(Constants.Keys.DefaultLayout, Constants.Sections.LayoutAll)
            .AddIfMissing(Constants.Keys.DefaultTheme, Constants.Sections.ThemeColour)
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
        
        var themeMap = new Dictionary<string, string[,]> { 
            [Constants.Sections.ThemeColour] = colourMap,
            [Constants.Sections.ThemeMono] = monoMap,
            [Constants.Sections.ThemeMsDos] = msDosMap,
            [Constants.Sections.ThemeTokyoNight] = tokyoNightMap,
            [Constants.Sections.ThemeMatrix] = matrixMap,
            [Constants.Sections.ThemeSolar] = solarMap
        };

        foreach (string themeName in themeMap.Keys) {
            if (!iniConfig.ContainsSection(themeName)) {
                ConfigSection themeSection = new(themeName);
                
                for (int i = 0; i < themeMap[themeName].GetLength(dimension: 0); i++) {
                    themeSection.AddIfMissing(themeMap[themeName][i, 0], themeMap[themeName][i, 1]);
                }

                iniConfig.AddConfigSection(themeSection);
            }
        }

        List<ConfigSection> themeSections = iniConfig.ConfigSections
            .Where(cs => cs.Name.StartsWith("theme-", StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        foreach (ConfigSection configSection in themeSections) {
            Theme? theme = allThemes.FirstOrDefault(t => t.Name.Equals(configSection.Name, StringComparison.CurrentCultureIgnoreCase));
            if (theme != null) {
                theme.Update(configSection);
            }
            else {
                theme = new Theme(configSection);
                allThemes.Add(theme);
            }

            // Convert any colour names to hex so they persist as hex on save.
            theme.Normalize();
        }

        if (allThemes.Any(t => t.Name.Equals(uxSection.GetString(Constants.Keys.DefaultTheme), StringComparison.CurrentCultureIgnoreCase))) {
            defaultTheme = allThemes
                .Where(t => t.Name == uxSection.GetString(Constants.Keys.DefaultTheme))
                .First();
        }

        var layoutMap = new Dictionary<string, string[,]> {
            [Constants.Sections.LayoutAll] = layoutAll,
            [Constants.Sections.LayoutAllLarge] = layoutAllLarge,
            [Constants.Sections.LayoutCpuAndMemory] = layoutCpuAndMemory,
            [Constants.Sections.LayoutCpuAndMemoryLarge] = layoutCpuAndMemoryLarge,
            [Constants.Sections.LayoutGpuAndMemory] = layoutGpuAndMemory,
            [Constants.Sections.LayoutGpuAndMemoryLarge] = layoutGpuAndMemoryLarge,
            [Constants.Sections.LayoutNetSendReceive] = layoutNetSendReceive,
            [Constants.Sections.LayoutNetSendReceiveLarge] = layoutNetSendReceiveLarge,
            [Constants.Sections.LayoutDisk] = layoutDisk,
            [Constants.Sections.LayoutDiskLarge] = layoutDiskLarge
        };

        foreach (string layoutName in layoutMap.Keys) {
            if (!iniConfig.ContainsSection(layoutName)) {
                ConfigSection layoutSection = new(layoutName);
                
                for (int i = 0; i < layoutMap[layoutName].GetLength(dimension: 0); i++) {
                    layoutSection.AddIfMissing(layoutMap[layoutName][i, 0], layoutMap[layoutName][i, 1]);
                }

                iniConfig.AddConfigSection(layoutSection);
            }
        }

        List<ConfigSection> layoutSections = iniConfig.ConfigSections
            .Where(cs => cs.Name.StartsWith("layout-", StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        foreach (ConfigSection configSection in layoutSections) {
            Layout? layout = allLayouts.FirstOrDefault(t => t.Name.Equals(configSection.Name, StringComparison.CurrentCultureIgnoreCase));
            if (layout != null) {
                layout.Update(configSection);
            }
            else {
                allLayouts.Add(new Layout(configSection));
            }
        }
        
        if (allLayouts.Any(t => t.Name.Equals(uxSection.GetString(Constants.Keys.DefaultLayout), StringComparison.CurrentCultureIgnoreCase))) {
            defaultLayout = allLayouts
                .Where(l => l.Name == uxSection.GetString(Constants.Keys.DefaultLayout))
                .First();
        }
    }

    public List<Layout> Layouts => allLayouts;
    
    public List<Theme> Themes => allThemes;

    public bool TryLoad(Config config)
    {
        try {
            iniConfig = config;
            LoadSections();
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
            iniConfig = Config.FromFile(fileSystem, path);
            LoadSections();
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException || ex is IOException) {
            ExceptionHelper.HandleException(ex, $"Error loading config: ${ex.Message}.");
        }
        catch (Exception ex) when (ex is ConfigParseException) {
            ExceptionHelper.HandleException(ex, $"Error parsing config: {ex.Message}.");
        }

        return false;
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
