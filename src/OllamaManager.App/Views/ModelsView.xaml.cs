using System.Windows.Controls;
using System.Windows.Input;
using OllamaManager.App.ViewModels;

namespace OllamaManager.App.Views;

public partial class ModelsView : UserControl
{
    public ModelsView()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ModelsViewModel vm) return;

        if (e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            var idx = (int)(e.Key - Key.D1) + 1;
            vm.QuickStartByIndex(idx);
            e.Handled = true;
        }
        else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
        {
            var idx = (int)(e.Key - Key.NumPad1) + 1;
            vm.QuickStartByIndex(idx);
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            if (vm.LoadModelsCommand.CanExecute(null))
            {
                vm.LoadModelsCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            if (vm.IsOperating)
            {
                vm.IsOperating = false;
                e.Handled = true;
            }
        }
    }
}