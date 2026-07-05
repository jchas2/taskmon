using System.Diagnostics;
using System.Net.Sockets;
using Task.Monitor.Commands;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.InputBox;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui;

public sealed class MainScreen : Screen
{
    private readonly RunContext runContext;
    private readonly ProcessControl processControl;
    private readonly ProcessInfoControl processInfoControl;
    private readonly HeaderControl headerControl;
    private readonly CommandControl commandControl;
    private readonly FilterControl filterControl;
    private Control activeControl;
    private Control footerControl;
    
    private const int FooterHeight = 1;

    public MainScreen(ScreenApplication screenApp, RunContext runContext)
    : base(runContext.Terminal)
    {
        this.runContext = runContext;

        headerControl = new HeaderControl(
            runContext.Processor,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = false
        };

        processControl = new ProcessControl(
            runContext.Processor,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 1
        };

        processInfoControl = new ProcessInfoControl(
            runContext.ProcessService,
            runContext.ModuleService,
            runContext.ThreadService,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 2
        };

        commandControl = new CommandControl(runContext.Terminal, runContext.AppConfig) {
            TabStop = false
        };

        commandControl
            .AddCommand(ConsoleKey.F1, () => new HelpCommand("Help", screenApp))
            .AddCommand(ConsoleKey.F2, () => new SetupCommand("Setup", screenApp))
            .AddCommand(ConsoleKey.F3, () => new ProcessSortCommand("Sort", this))
            .AddCommand(ConsoleKey.F4, () => new FilterCommand("Filter", this))
            .AddCommand(ConsoleKey.F5, () => new ProcessInfoCommand("Info", this))
            .AddCommand(ConsoleKey.F6, () => new EndTaskCommand("End Task", this, runContext.AppConfig))
            .AddCommand(ConsoleKey.F7, () => new ThemeCommand("Theme", this, runContext.AppConfig))
            .AddCommand(ConsoleKey.F8, () => new LayoutCommand("Layout", this, runContext.AppConfig))
            .AddCommand(ConsoleKey.F9, () => new AboutCommand("About", screenApp))
            .AddCommand(ConsoleKey.F10, () => new ExitCommand("Quit"));

        filterControl = new FilterControl(runContext.Terminal, runContext.AppConfig) {
            TabStop = false
        };

        activeControl = processControl;
        footerControl = commandControl;

        Controls
            .Add(headerControl)
            .Add(processControl)
            .Add(processInfoControl)
            .Add(commandControl)
            .Add(filterControl);
    } 

    internal Control? GetActiveControl => activeControl;

    internal T GetControl<T>() where T : Control => (T)Controls.Single(ctrl => ctrl is T);
    
    private int HeaderHeight => (int)(runContext.AppConfig.DefaultLayout.Ratio * Height)
    ;
    protected override void OnDraw()
    {
        Debug.Assert(activeControl != null);

        headerControl.Draw();
        activeControl.Draw();
        footerControl.Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        base.OnKeyPressed(keyInfo, ref handled);
        
        if (handled) {
            return;
        }
        
        if (keyInfo.Key == ConsoleKey.Escape && activeControl != processControl) {
            SetActiveControl<ProcessControl>();
            Draw();
            handled = true;
            return;
        }
        
        activeControl.KeyPressed(keyInfo, ref handled);

        if (handled) {
            return;
        }

        commandControl.KeyPressed(keyInfo, ref handled);
    }

    protected override void OnLoad()
    {
        Terminal.CursorVisible = false;

        BackgroundColour = runContext.AppConfig.DefaultTheme.Background;
        ForegroundColour = runContext.AppConfig.DefaultTheme.Foreground;
        
        foreach (Control ctrl in Controls) {
            ctrl.BackgroundColour = BackgroundColour;
            ctrl.ForegroundColour = ForegroundColour;
        }

        activeControl = processControl;

        headerControl.Load();
        activeControl.Load();
        footerControl.Load();
        
        processControl.ProcessItemSelected += OnProcessItemSelected;
    }

    private void OnProcessItemSelected(object? sender, ListViewItemEventArgs e)
    {
        // Send the F5 key to the command control to invoke the Process Info Command.
        ConsoleKeyInfo keyInfo = new(
            (char)ConsoleKey.F5, 
            ConsoleKey.F5, 
            shift: false, 
            alt: false, 
            control: false);
        
        bool handled = false;        
        commandControl.KeyPressed(keyInfo, ref handled);
    }

    protected override void OnResize()
    {
        int headerHeight = HeaderHeight;
        
        headerControl.X = 0;
        headerControl.Y = 0;
        headerControl.Height = headerHeight;
        headerControl.Width = Width;
        
        SizeControl(processControl);
        SizeControl(processInfoControl);

        footerControl.X = 0;
        footerControl.Y = Height - FooterHeight;
        footerControl.Width = Width;
        footerControl.Height = FooterHeight;
        
        base.OnResize();
    }
    
    protected override void OnUnload()
    {
        processControl.ProcessItemSelected -= OnProcessItemSelected;
        base.OnUnload();
        Terminal.CursorVisible = true;
    }

    internal T SetActiveControl<T>() where T : Control
    {
        Debug.Assert(activeControl != null);
       
        Control? nextControl = Controls.ToList().SingleOrDefault(c => c.GetType() == typeof(T));
        
        if (nextControl == null) {
            throw new InvalidOperationException();
        }
        
        activeControl.Unload();
        activeControl = nextControl;
        
        SizeControl(activeControl);
        
        activeControl.Load();
        activeControl.Clear();
        activeControl.Resize();

        return (T)activeControl;
    }

    internal void ShowCommandControl()
    {
        filterControl.Visible = false;
        
        ShowFooterControl(commandControl);   
    }

    internal void ShowFilterControl(Action<string, InputBoxResult> onInputBoxResult)
    {
        commandControl.Visible = false;
        
        ShowFooterControl(filterControl);
        
        ShowInputBox(
            filterControl.X + filterControl.NeededWidth,
            filterControl.Y,
            40,
            "Filter: ",
            onInputBoxResult);
    }

    private void ShowFooterControl(Control control)
     {
        footerControl = control;
        footerControl.Visible = true;
        
        Resize();
        Draw();
    }
    
    private void SizeControl(Control control)
    {
        int headerHeight = HeaderHeight;
        
        control.X = 0;
        control.Y = headerHeight;
        control.Width = Width;
        control.Height = Height - headerHeight - FooterHeight;
    }
}
