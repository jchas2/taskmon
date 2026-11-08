using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.Tests.Gui.Controls;

// For the controls that pair a main list with a DETAIL list: STARTUP, APPS, SERVICES and DRIVERS.
internal static class ListPaneTestHelper
{
    public static ListView Detail(Control ctrl) =>
        ctrl.Controls.OfType<ListView>().Single(view => view.HeaderText == "DETAIL");

    // The main list's STATUS cell for the selected row, found by the status text DETAIL shows for it.
    // The control holds other lists too (e.g. the sort picker), so look only outside DETAIL.
    public static ListViewSubItem SelectedStatusCell(Control ctrl)
    {
        string status = Detail(ctrl).Items.Cast<ListViewItem>()
            .Single(row => row.SubItems[0].Text == "Status")
            .SubItems[1].Text;

        return ctrl.Controls.OfType<ListView>()
            .Where(view => view.HeaderText != "DETAIL")
            .SelectMany(view => view.Items.Cast<ListViewItem>())
            .SelectMany(row => row.SubItems.Cast<ListViewSubItem>())
            .Single(cell => cell.Text == status);
    }
}
