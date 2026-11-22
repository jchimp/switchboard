using System.Windows;
using System.Windows.Interop;
using Switchboard.App.ViewModels;
using Switchboard.Windows;

namespace Switchboard.App.Settings;

/// <summary>
/// Bulk "adopt as new" over every unbound Windows Terminal window (R27). Modal on the
/// settings window: it is a decision you finish before going back to the table.
/// </summary>
public partial class ImportWindowsDialog : Window
{
    public ImportWindowsDialog(ImportViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(new WindowInteropHelper(this).Handle);

        // The list is an EnumWindows sweep; take it once as the dialog opens, then on Refresh.
        Loaded += (_, _) => vm.Refresh();
        vm.Finished += () => Close();
    }
}
