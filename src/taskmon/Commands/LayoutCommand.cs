using Task.Monitor.Configuration;
using Task.Monitor.Gui;

namespace Task.Monitor.Commands;

public class LayoutCommand : AbstractCommand
{
    private int currLayoutIndex = 0;
    private readonly MainScreen mainScreen1;
    private readonly AppConfig appConfig1;
    
    public LayoutCommand(string text, MainScreen mainScreen, AppConfig appConfig) : base(text)
    {
        mainScreen1 = mainScreen;
        appConfig1 = appConfig;

        currLayoutIndex = Math.Max(0, appConfig.Layouts.FindIndex(layout => layout == appConfig.DefaultLayout));
    }

    public override void Execute()
    {
        if (++currLayoutIndex >= appConfig1.Layouts.Count) {
            currLayoutIndex = 0;
        }
        
        Layout nextLayout = appConfig1.Layouts[currLayoutIndex];
        appConfig1.DefaultLayout = nextLayout;
        
        mainScreen1.Resize();
        mainScreen1.Draw();
    }

    public override bool IsEnabled => true;
}
