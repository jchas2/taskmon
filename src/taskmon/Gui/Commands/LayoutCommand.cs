using Task.Monitor.Configuration;
using Task.Monitor.Gui;

namespace Task.Monitor.Gui.Commands;

public class LayoutCommand : AbstractCommand
{
    private int currLayoutIndex = 0;
    private readonly MainScreen mainScreen;
    private readonly AppConfig appConfig;
    
    public LayoutCommand(string text, MainScreen mainScreen, AppConfig appConfig) : base(text)
    {
        this.mainScreen = mainScreen;
        this.appConfig = appConfig;

        currLayoutIndex = Math.Max(0, appConfig.Layouts.FindIndex(layout => layout == appConfig.DefaultLayout));
    }

    public override void Execute()
    {
        if (++currLayoutIndex >= appConfig.Layouts.Count) {
            currLayoutIndex = 0;
        }
        
        Layout nextLayout = appConfig.Layouts[currLayoutIndex];
        appConfig.DefaultLayout = nextLayout;

        mainScreen.Clear();
        mainScreen.Resize();
        mainScreen.Draw();
    }

    public override bool IsEnabled => true;
}
