using Task.Monitor.Configuration;
using Task.Monitor.Gui;

namespace Task.Monitor.Commands;

public class ThemeCommand(string text, MainScreen mainScreen, AppConfig appConfig) : AbstractCommand(text)
{
    private int index = 0;
    
    public override void Execute()
    {
        if (++index == appConfig.Themes.Count) {
            index = 0;
        }

        Theme nextTheme = appConfig.Themes[index];
        appConfig.DefaultTheme = nextTheme;
        
        mainScreen.Close();
        mainScreen.Show();
    }

    public override bool IsEnabled => appConfig.Themes.Count > 0;
}