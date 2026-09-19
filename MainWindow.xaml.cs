using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ZCodePluginManager.Models;
using ZCodePluginManager.Services;

namespace ZCodePluginManager;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ObservableCollection<PluginViewModel> _plugins = [];
    private readonly AppSettings _settings;
    private PluginViewModel? _currentPlugin;

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsService.Load();
        Plugins = _plugins;
        DataContext = this;
        PluginsRootBox.Text = _settings.PluginsRoot;
        ReloadPlugins();
    }

    public ObservableCollection<PluginViewModel> Plugins { get; }

    public string PluginsRootSummary => $"插件根目录：{_settings.PluginsRoot}";

    public PluginViewModel? CurrentPlugin
    {
        get => _currentPlugin;
        private set
        {
            _currentPlugin = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPlugin)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void ReloadPlugins()
    {
        _plugins.Clear();
        foreach (var plugin in PluginCatalog.Discover(_settings.PluginsRoot))
        {
            _plugins.Add(new PluginViewModel(plugin, PluginOperations.ReadStatus(plugin)));
        }

        EmptyState.Visibility = _plugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRefreshPlugins(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        ReloadPlugins();
    }

    private void OnOpenAddPlugin(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        AddPluginPanel.Visibility = Visibility.Visible;
        GitHubUrlBox.Focus();
    }

    private void OnCancelAddPlugin(object sender, RoutedEventArgs e)
    {
        AddPluginPanel.Visibility = Visibility.Collapsed;
        GitHubUrlBox.Clear();
    }

    private async void OnAddPlugin(object sender, RoutedEventArgs e)
    {
        var repository = GitHubUrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(repository))
        {
            MessageBox.Show("请先粘贴 GitHub 仓库链接。", "添加插件", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConfirmAddPluginButton.IsEnabled = false;
        CancelAddPluginButton.IsEnabled = false;
        try
        {
            var plugin = await PluginDirectoryService.AddFromGitHubAsync(_settings.PluginsRoot, repository);
            if (plugin is null)
            {
                MessageBox.Show("插件已下载，但未能识别插件信息。", "添加插件", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            GitHubUrlBox.Clear();
            AddPluginPanel.Visibility = Visibility.Collapsed;
            ReloadPlugins();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "添加插件失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ConfirmAddPluginButton.IsEnabled = true;
            CancelAddPluginButton.IsEnabled = true;
        }
    }

    private async void OnDeletePlugin(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: PluginViewModel plugin })
        {
            await DeletePluginAsync(plugin);
        }
    }

    private async void OnDeleteDetailPlugin(object sender, RoutedEventArgs e)
    {
        if (CurrentPlugin is { } plugin)
        {
            await DeletePluginAsync(plugin);
        }
    }

    private async Task DeletePluginAsync(PluginViewModel plugin)
    {
        var title = plugin.Definition.Title;
        if (MessageBox.Show(
                $"确定删除插件 {title} 及其本地目录吗？\n{plugin.Definition.Directory}",
                "删除插件",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await PluginDirectoryService.DeleteAsync(plugin.Definition, _settings.PluginsRoot);
            if (CurrentPlugin == plugin)
            {
                MainPage.Visibility = Visibility.Visible;
                DetailPage.Visibility = Visibility.Collapsed;
                CurrentPlugin = null;
            }

            ReloadPlugins();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "删除插件失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        AddPluginPanel.Visibility = Visibility.Collapsed;
        PluginsRootBox.Text = _settings.PluginsRoot;
        PluginsRootBox.CaretIndex = PluginsRootBox.Text.Length;
        SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnBrowsePluginsRoot(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder("选择插件根目录");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            PluginsRootBox.Text = folder;
            PluginsRootBox.CaretIndex = PluginsRootBox.Text.Length;
        }
    }

    private void OnSavePluginsRoot(object sender, RoutedEventArgs e)
    {
        var selected = PluginsRootBox.Text.Trim();
        if (!Directory.Exists(selected))
        {
            MessageBox.Show("插件根目录不存在。", "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.PluginsRoot = selected;
        SettingsService.Save(_settings);
        SettingsPanel.Visibility = Visibility.Collapsed;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PluginsRootSummary)));
        ReloadPlugins();
    }

    private void OnPluginClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PluginViewModel plugin })
        {
            OpenPlugin(plugin);
        }
    }

    private void OnBackToMainPage(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        MainPage.Visibility = Visibility.Visible;
        DetailPage.Visibility = Visibility.Collapsed;
        RefreshStatuses();
        CurrentPlugin = null;
    }

    private void OpenPlugin(PluginViewModel plugin)
    {
        CurrentPlugin = plugin;
        ZCodeRootBox.Text = _settings.ZCodeRoot;
        PythonPathBox.Text = _settings.PythonPath;
        OutputBox.Clear();
        MainPage.Visibility = Visibility.Collapsed;
        DetailPage.Visibility = Visibility.Visible;
        UpdateButtonStates(plugin);
    }

    private void OnBrowseZCode(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder("选择 ZCode 安装目录");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ZCodeRootBox.Text = folder;
        }
    }

    private void OnBrowsePython(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 Python 可执行文件",
            Filter = "Python (py.exe, python.exe)|py.exe;python.exe|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            PythonPathBox.Text = dialog.FileName;
        }
    }

    private async void OnInstallPlugin(object sender, RoutedEventArgs e)
    {
        await RunPluginActionAsync("install");
    }

    private async void OnUninstallPlugin(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("确定要卸载当前插件吗？", "卸载确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        await RunPluginActionAsync("uninstall");
    }

    private async void OnUpdatePlugin(object sender, RoutedEventArgs e)
    {
        await RunPluginActionAsync("update");
    }

    private async Task RunPluginActionAsync(string action)
    {
        if (CurrentPlugin is not { } plugin)
        {
            return;
        }

        SaveSettings();
        plugin.Busy = true;
        UpdateButtonStates(plugin);
        OutputBox.AppendText($"\n[{DateTime.Now:HH:mm:ss}] {plugin.Definition.Title} {action}...\n");
        OutputBox.ScrollToEnd();

        try
        {
            ProcessResult result;
            if (action == "update")
            {
                result = await GitService.PullAsync(plugin.Definition.Directory);
                if (result.Success && plugin.Status.Installed && plugin.Status.Supported)
                {
                    OutputBox.AppendText("\n代码已更新，开始重新安装...\n");
                    OutputBox.ScrollToEnd();
                    result = await RunInstallAsync(plugin.Definition);
                }
            }
            else
            {
                result = action == "install"
                    ? await RunInstallAsync(plugin.Definition)
                    : await RunUninstallAsync(plugin.Definition);
            }

            OutputBox.AppendText(string.IsNullOrWhiteSpace(result.Output)
                ? $"退出码：{result.ExitCode}\n"
                : $"{result.Output}\n退出码：{result.ExitCode}\n");
            OutputBox.ScrollToEnd();
        }
        catch (Exception exception)
        {
            OutputBox.AppendText($"执行失败：{exception.Message}\n");
            OutputBox.ScrollToEnd();
        }
        finally
        {
            plugin.Busy = false;
            RefreshStatuses();
            UpdateButtonStates(plugin);
        }
    }

    private async Task<ProcessResult> RunInstallAsync(PluginDefinition definition)
    {
        var fileName = PluginOperations.BuildFileName(definition, _settings);
        var arguments = PluginOperations.BuildInstall(definition, _settings);
        return await ProcessRunner.RunAsync(fileName, arguments, definition.Directory);
    }

    private async Task<ProcessResult> RunUninstallAsync(PluginDefinition definition)
    {
        var fileName = PluginOperations.BuildFileName(definition, _settings);
        var arguments = PluginOperations.BuildUninstall(definition, _settings);
        return await ProcessRunner.RunAsync(fileName, arguments, definition.Directory);
    }

    private void SaveSettings()
    {
        _settings.ZCodeRoot = ZCodeRootBox.Text;
        _settings.PythonPath = PythonPathBox.Text;
        SettingsService.Save(_settings);
    }

    private void RefreshStatuses()
    {
        foreach (var plugin in _plugins)
        {
            plugin.Status = PluginOperations.ReadStatus(plugin.Definition);
        }
    }

    private void UpdateButtonStates(PluginViewModel plugin)
    {
        InstallButton.IsEnabled = !plugin.Busy && plugin.Status.Supported && !plugin.Status.Installed;
        UninstallButton.IsEnabled = !plugin.Busy && plugin.Status.Supported && plugin.Status.Installed;
        UpdateButton.IsEnabled = !plugin.Busy && Directory.Exists(Path.Combine(plugin.Definition.Directory, ".git"));
        BrowseZCodeButton.IsEnabled = !plugin.Busy;
        BrowsePythonButton.IsEnabled = !plugin.Busy;
        ZCodeRootBox.IsEnabled = !plugin.Busy;
        PythonPathBox.IsEnabled = !plugin.Busy;
        DetailDeleteButton.IsEnabled = !plugin.Busy;

        InstallButton.Content = plugin.Status.Installed ? "已安装" : "安装";
        InstallButton.Background = plugin.Status.Installed
            ? System.Windows.Media.Brushes.Transparent
            : System.Windows.Media.Brushes.DodgerBlue;
        InstallButton.Foreground = plugin.Status.Installed
            ? System.Windows.Media.Brushes.DarkGreen
            : System.Windows.Media.Brushes.White;
    }

    private string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }
}
