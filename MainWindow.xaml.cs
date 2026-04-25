using System.ComponentModel;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace uc2m4a;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    #pragma warning disable CS8622 // 参数类型中引用类型的为 Null 性与目标委托不匹配(可能是由于为 Null 性特性)。

    private readonly DispatcherTimer _progressTimer;
    private int _progressValue;

    private enum SaveType
    {
        DefaultFolder = 0,
        CustomFolder = 1
    }
    private SaveType _saveType = SaveType.DefaultFolder;
    private string _saveFolderPath = string.Empty;

    public MainWindow()
    {
        InitializeComponent();

        // 模拟进度用的计时器
        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _progressTimer.Tick += ProgressTimer_Tick;

        CmbSaveType.SelectedIndex = 0; // 默认选择“默认目录”
        CmbSaveType.Foreground = TxtSavePath.Foreground;

        if (CmbSaveType.SelectedIndex == 0 && string.IsNullOrEmpty(TxtSavePath.Text))
        {
            TxtStatus.Text = "请选择保存目录";
            TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 182, 193)); // 粉色
        }

        Drop += async (sender, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                try
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length > 0)
                    {
                        var file_uc = files[0];
                        TxtSrcPath.Text = file_uc;
                        var ret = await ConvertFileAsync(file_uc);
                    }
                }
                catch { }
            }
        };
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CmbSaveType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && CmbSaveType.SelectedIndex >= 0)
        {
            switch (CmbSaveType.SelectedIndex)
            {
                case (int)SaveType.DefaultFolder:
                    _saveType = SaveType.DefaultFolder;
                    break;
                case (int)SaveType.CustomFolder:
                    _saveType = SaveType.CustomFolder;
                    break;
            }
            CmbSaveType.Text = (e.AddedItems[0] as ComboBoxItem)?.Content.ToString();
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BtnSrcBrowse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                Title = "请选择来源文件",
                Filter = "UC文件 (*.uc)|*.uc",
                DefaultExt = ".uc",
                CheckFileExists = true,
                CheckPathExists = true,
            };

            if (dialog.ShowDialog() ?? false)
            {
                TxtSrcPath.Text = dialog.FileName;
                TxtStatus.Text = $"已选择文件: {dialog.FileName}";
            }
        }
        catch { }
    }

    /// <summary>
    /// 浏览按钮 - 打开文件夹选择对话框
    /// </summary>
    private void BtnSaveBrowse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new FolderPicker()
            {
                Title = "请选择保存目录",
                //FileNameLabel = "选择文件夹",
                //ForceFileSystem = true,
            };

            if (dialog.ShowDialog() ?? false)
            {
                TxtSavePath.Text = dialog.ResultPath;
                _saveFolderPath = dialog.ResultPath;
                TxtStatus.Text = $"已选择目录: {dialog.ResultPath}";
                //TxtStatus.Foreground = (System.Windows.Media.Brush)FindResource("Foreground"); // 使用默认前景色
            }
        }
        catch { }
    }

    /// <summary>
    /// 模拟进度更新（实际使用时替换为真实的转换逻辑）
    /// </summary>
    private void ProgressTimer_Tick(object sender, EventArgs e)
    {
        if (ProgressBar.Value >= 100)
        {
            _progressTimer.Stop();
            TxtStatus.Text = "转换完成！";
            TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(166, 227, 161)); // 绿色
            return;
        }

        if (_progressValue > ProgressBar.Value)
        {
            ProgressBar.Value = _progressValue;
            TxtProgress.Text = $"{_progressValue:F0}%";
        }

        // 根据进度更新状态
        if (ProgressBar.Value < 10)
            TxtStatus.Text = "正在读取文件...";
        else if (ProgressBar.Value < 90)
            TxtStatus.Text = "正在转换编码...";
        else if (ProgressBar.Value < 100)
            TxtStatus.Text = "正在写入文件...";
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void BtnConvert_Click(object sender, RoutedEventArgs e)
    {
        var file_uc = TxtSrcPath.Text;
        if (!string.IsNullOrEmpty(file_uc) && File.Exists(file_uc))
        {
            await ConvertFileAsync(file_uc);
        }
    }

    /// <summary>
    /// 供外部调用的方法：开始模拟转换
    /// </summary>
    public void StartConvert()
    {
        ProgressBar.Value = 0;
        TxtProgress.Text = "0%";
        TxtStatus.Text = "正在转换...";
        TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(137, 180, 250)); // 蓝色
        _progressTimer.Start();
    }

    private async Task<(bool, string)> ConvertFileAsync(string file_uc)
    {
        var result = false;
        var reason = string.Empty;

        (result, reason) = await Task.Run(() =>
        {
            var ret = ConvertFile(file_uc, out string reason);
            return (ret, reason);
        });

        return (result, reason);
    }

    private bool ConvertFile(string file_uc, out string reason)
    {
        var result = false;
        reason = string.Empty;

        if (!File.Exists(file_uc))
        {
            reason = $"错误，您所访问的文件不存在或您无权访问该文件！{Environment.NewLine}请检查文件路径拼写是否有误 (文件路径的开头和结尾不能包含引号)";
            return (result);
        }

        var ext = System.IO.Path.GetExtension(file_uc).ToLower();
        if (!ext.Equals(".uc"))
        {
            reason = $"错误，文件名必须以[.uc]结尾！";
            return (result);
        }

        var targetFolder = string.IsNullOrEmpty(_saveFolderPath) ? System.IO.Path.GetDirectoryName(file_uc) : _saveFolderPath;
        var targetFileName = System.IO.Path.GetFileNameWithoutExtension(file_uc) + ".m4a";
        if (string.IsNullOrEmpty(targetFolder)) targetFolder = System.IO.Path.GetDirectoryName(file_uc);

        reason = "您要转换的文件是：" + file_uc;
        var file_m4a = string.IsNullOrEmpty(targetFolder) ? targetFileName : System.IO.Path.Combine(targetFolder, targetFileName);
        try
        {
            Dispatcher.Invoke(() => StartConvert());

            byte[] data_in = File.ReadAllBytes(file_uc);
            for (int i = 0; i < data_in.Length; i++)
            {
                data_in[i] ^= 0xa3;
                _progressValue = (int)Math.Ceiling(i / (double)data_in.Length * 100.0);
            }
            File.WriteAllBytes(file_m4a, data_in);
            result = true;
        }
        catch (IOException e)
        {
            reason = $"非法操作{Environment.NewLine}{e.StackTrace}";
        }
        return (result);
    }

}