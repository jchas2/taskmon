using Task.Monitor.Configuration;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Process;

namespace Task.Monitor.Gui.Controls;

public partial class ProcessInfoControl
{
    public class ModuleListViewItem : ListViewItem
    {
        public ModuleListViewItem(ModuleInfo moduleInfo, AppConfig appConfig)
            : base(moduleInfo.ModuleName)
        {
            SubItems.AddRange(
                new ListViewSubItem(this, moduleInfo.FileName));
            
            for (int i = 0; i < (int)ModuleColumns.Count; i++) {
                SubItems[i].BackgroundColor = appConfig.DefaultTheme.Background;
                SubItems[i].ForegroundColor = appConfig.DefaultTheme.Foreground;
            }
        }
    }
}