using System.Windows;
using AutoTest.ErpAutomation.Services;
using AutoTest.ErpAutomation.ViewModels;

namespace AutoTest.ErpAutomation;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(
            new ChromeConnectionService(),
            new ErpAutomationService(),
            new AutomationSettingsService(),
            new AutomationRunLogService(),
            new FolderOpenService());
    }
}
