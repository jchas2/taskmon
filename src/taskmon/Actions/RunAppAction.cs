using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.DiskSpace;
using Task.Monitor.System.Services.Drivers;
using Task.Monitor.System.Services.InstalledApps;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Services.Startup;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Services.WindowsServices;
using ProcessService = Task.Monitor.System.Services.Process.ProcessService;

namespace Task.Monitor.Actions;

public sealed class RunAppAction(RunContext runContext) : IAction
{
    public int Run()
    {
        ConsoleEx.SetAlternateScreenBuffer();

        runContext.ServiceController
            .AddService(() => new CpuService())
            .AddService(() => new MemoryService())
            .AddService(() => new GpuService())
            .AddService(() => new DiskService())
            .AddService(() => new DiskSpaceService())
            .AddService(() => new NetworkService())
            .AddService(() => new ProcessService {
                IrixMode = runContext.AppConfig.UseIrixReporting
            })
            .AddService(() => new StartupService())
            .AddService(() => new InstalledAppsService())
            .AddService(() => new WindowsServicesService())
            .AddService(() => new DriversService())
            .AddService(() => new ThermalService())
            .AddService(() => new PowerService());

        // After the chain, so it reaches every service registered above as well as the controller's
        // own publish cycle. Setup calls the same method when the delay is changed at runtime.
        runContext.ServiceController.SetSamplingDelay(runContext.AppConfig.DelayInMilliseconds);

        ScreenApplication screenApp = new(runContext.Terminal);
        MainScreen2 mainScreen = new(runContext, screenApp);

        screenApp
            .RegisterScreen(mainScreen)
            .RegisterScreen(new HelpScreen(runContext))
            .RegisterScreen(new SetupScreen(runContext))
            .RegisterScreen(new AboutScreen(runContext));
        
        runContext.ServiceController.Start();

        // Run the App event loop.
        screenApp.Run(mainScreen);
        
        runContext.ServiceController.Stop();
        return Program.ExitSuccess;
    }
}