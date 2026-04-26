using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.ConstrainedExecution;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
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

using _163music;

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
    private string _cacheFolder = "%LOCALAPPDATA%\\NetEase\\CloudMusic\\Cache\\Cache"; // "\NetEase\CloudMusic\Cache\Cache\3368793123-320-decf7cd0a74c6a97703663018dcb0335.uc"
    private string _saveFolderPath = string.Empty;
    private string _m4a_filename = string.Empty;

    private readonly SolidColorBrush COLOR_ERROR = new(Color.FromRgb(255, 32, 32)); // 红色
    private readonly SolidColorBrush COLOR_SUCCESS = new(Color.FromRgb(0x00, 0x7F, 0x46)); // 绿色
    private readonly SolidColorBrush COLOR_PROCESSING = new(Color.FromRgb(137, 180, 250)); // 蓝色

    private CancellationTokenSource? _cancel_convert_ = null;

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
            TxtStatus.Foreground = COLOR_ERROR;
        }

        TxtSrcPath.AutoWordSelection = true;
        TxtSrcPath.GotFocus += (sender, e) =>
        {
            TxtSrcPath.SelectionStart = 0;
            TxtSrcPath.SelectionLength = TxtSrcPath.Text.Length;
            TxtSrcPath.SelectAll();
        };

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
                        var (ret, reason) = await ConvertFileAsync(file_uc);
                        TxtStatus.Text = reason;
                        TxtStatus.ToolTip = TxtStatus.Text;
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
            TxtStatus.Foreground = COLOR_SUCCESS;
            return;
        }
        else if (IsCancelConvert)
        {
            _progressTimer.Stop();
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
        if (!string.IsNullOrEmpty(file_uc))
        {
            if (File.Exists(file_uc))
                await ConvertFileAsync(file_uc);
            else
            {
                var id = GetIdFromWebLink(file_uc);
                if (id > 0)
                {
                    var site = new NetEaseMusic();
                    var song = await site.GetSongDetail(id);
                    if (song != null)
                    {
                        TxtStatus.Text = $"已识别链接，歌曲名称：{song.Title}，正在下载...";
                        // 下载并转换
                        var uc_file = GetUCFromCache(song.ID);
                        if (!string.IsNullOrEmpty(uc_file) && File.Exists(uc_file))
                        {
                            TxtSrcPath.Text = uc_file;
                            var (ret, reason) = await ConvertFileAsync(uc_file);
                            if (ret)
                            {
                                await UpdateMetaAsync(_m4a_filename, song);
                            }
                            else
                            {
                                TxtStatus.Text = $"转换失败！{Environment.NewLine}{reason}";
                            }
                        }
                        else
                        {
                            TxtStatus.Text = $"未找到缓存文件，无法转换！";
                        }
                    }
                    else
                    {
                        TxtStatus.Text = $"无法获取歌曲信息！";
                    }
                }
                else
                {
                    TxtStatus.Text = $"错误，您所访问的文件不存在或您无权访问该文件！{Environment.NewLine}请检查文件路径拼写是否有误 (文件路径的开头和结尾不能包含引号)";
                }
                TxtStatus.ToolTip = TxtStatus.Text;
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BtnPlayM4A_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_m4a_filename) && File.Exists(_m4a_filename))
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _m4a_filename,
                    UseShellExecute = true
                });
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "openwith.exe",
                    Arguments = _m4a_filename,
                    UseShellExecute = true
                });
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void BtnUpdateMeta_Click(object sender, RoutedEventArgs e)
    {
        await UpdateMetaAsync(_m4a_filename);
    }

    private string GetUCFromCache(int sId)
    {
        var result = string.Empty;
        if (string.IsNullOrEmpty(_cacheFolder)) return (result);
        var folder = Environment.ExpandEnvironmentVariables(_cacheFolder);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return (result);

        var files = Directory.EnumerateFiles(folder, $"{sId}-*-*.uc");
        if (files.Any())
        {
            result = files.FirstOrDefault();
        }
        return (result);
    }

    private string CleanWebLink(string link)
    {
        //https://music.163.com/song?id=5087878&uct2=U2FsdGVkX1+t0HRqsklYooXR1bHa8tZ+WfSZscVNrtk=

        var result = link;
        if (string.IsNullOrEmpty(link)) return (result);
        result = Regex.Replace(link, @"^(https?://music\.163\.com/)(song|album|playlist)\?(.*?&)?id=\d+)&.*?$", "$1$2?id=$3", RegexOptions.IgnoreCase);
        return (result);
    }

    private int GetIdFromWebLink(string link)
    {
        var result = 0;
        if (string.IsNullOrEmpty(link)) return (result);
        var match = Regex.Match(link, @"^(https?://music\.163\.com/(song|album|playlist)\?(.*?&)?id=)?(\d+)(&.*?)?$", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[4].Value, out result))
        {
            //result = id;
        }
        return (result);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    private int GetSongIdFromFileName(string filename)
    {
        var result = 0;
        if (string.IsNullOrEmpty(filename)) return (result);
        var match = Regex.Match(filename, @"^(\d+)-\d+-.*?$", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out result))
        {
            //result = id;
        }
        return (result);
    }

    /// <summary>
    /// 供外部调用的方法：开始模拟转换
    /// </summary>
    public void StartConvert()
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = 0;
            TxtProgress.Text = "0%";
            TxtStatus.Text = "正在转换...";
            TxtStatus.Foreground = COLOR_PROCESSING;
            _progressTimer.Start();
        });
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reason"></param>
    public void StopConvert(string reason = "")
    {
        _cancel_convert_?.Cancel();
        Dispatcher.Invoke(() => 
        {
            ProgressBar.Value = _progressValue;
            TxtProgress.Text = $"{_progressValue:F0}%";
            if (!string.IsNullOrEmpty(reason))
            {
                TxtStatus.Text = reason;
                TxtStatus.Foreground = COLOR_ERROR;
                TxtStatus.ToolTip = TxtStatus.Text;
            }
        });
    }

    /// <summary>
    /// 
    /// </summary>
    private bool IsCancelConvert => _cancel_convert_?.IsCancellationRequested ?? false;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="file_uc"></param>
    /// <returns></returns>
    private async Task<(bool, string)> ConvertFileAsync(string file_uc)
    {
        var result = false;
        var reason = string.Empty;

        _cancel_convert_?.Cancel();
        await Task.Delay(250); // 等待之前的转换任务取消完成

        (result, reason) = await Task.Run(() =>
        {
            _cancel_convert_ = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var ret = ConvertFile(file_uc, out string reason);
            return (ret, reason);
        });

        await Dispatcher.InvokeAsync(() => { TxtStatus.Text = reason; });
        return (result, reason);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="file_uc"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    private bool ConvertFile(string file_uc, out string reason)
    {
        var result = false;
        reason = string.Empty;
        _m4a_filename = string.Empty;

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
            if (IsCancelConvert)
            {
                reason = "转换已取消！";
                return (result);
            }

            StartConvert();

            _progressValue = 0;

            byte[] data_in = File.ReadAllBytes(file_uc);
            for (int i = 0; i < data_in.Length; i++)
            {
                data_in[i] ^= 0xa3;
                _progressValue = (int)Math.Ceiling(i / (double)data_in.Length * 100.0);

                if (IsCancelConvert)
                {
                    reason = "转换已取消！";
                    break;
                }
            }

            if (IsCancelConvert)
            {
                reason = "转换已取消！";
                return (result);
            }
            File.WriteAllBytes(file_m4a, data_in);
            _m4a_filename = file_m4a;
            result = true;
        }
        catch (IOException e)
        {
            reason = $"非法操作{Environment.NewLine}{e.StackTrace}";
            StopConvert(reason);
        }
        finally {  }
        return (result);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="file_m4a"></param>
    /// <returns></returns>
    private async Task<bool> UpdateMetaAsync(string file_m4a, Song? song = null)
    {
        var result = false;
        if (File.Exists(file_m4a))
        {
            result = await Task.Run(async () =>
            {
                var ret = await UpdateMeta(file_m4a, song);
                return (ret);
            });
        }
        return (result);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="file_m4a"></param>
    /// <returns></returns>
    private async Task<bool> UpdateMeta(string file_m4a, Song? song = null)
    {
        var result = false;

        if (File.Exists(file_m4a))
        {
            try
            {
                // 32098510-320-41ba35bdfb1d9199a27f4c32e85872dd
                if (song is null && int.TryParse(Regex.Replace(System.IO.Path.GetFileNameWithoutExtension(file_m4a), @"^(\d+)-\d+-.*?$", "$1", RegexOptions.IgnoreCase), out int id))
                {
                    var site = new NetEaseMusic();
                    song = await site.GetSongDetail(id);
                }
                if (song != null)
                {
                    // var m4a_file = m4a_meta.GetFileAs<TagLibSharp2.Mp4.Mp4File>();
                    //var m4a_file = await TagLibSharp2.Mp4.Mp4File.ReadFromFileAsync(file_m4a);
                    //if (m4a_file.IsSuccess && m4a_file.File?.Tag is not null)
                    //{
                    //    var m4a_meta = m4a_file.File;
                    //    m4a_meta.Tag.Title = song.Title;
                    //    m4a_meta.Tag.Subtitle = song.Alias;
                    //    m4a_meta.Tag.PodcastFeedUrl = song.URL;
                    //    m4a_meta.Tag.Track = (uint)song.Track;
                    //    m4a_meta.Tag.Artist = string.Join(" ; ", song.Artists.Select(a => a.Name)) + ";";
                    //    m4a_meta.Tag.Album = song.Album?.Title;
                    //    m4a_meta.Tag.AlbumArtists = [song.Album?.Artist];
                    //    m4a_meta.Tag.DiscSubtitle = song.Album?.Subtitle;
                    //    if (!string.IsNullOrEmpty(song.Album?.Cover))
                    //    {
                    //        var data = await song.Album.DownloadCover();
                    //        if (data is not null)
                    //        {
                    //            m4a_meta.Tag.Pictures = [new Mp4Picture(data, true)];
                    //        }
                    //    }
                    //    var m4a_result = await m4a_file.File?.SaveToFileAsync(file_m4a);
                    //    result = m4a_result.IsSuccess;
                    //}

                    using var m4a = TagLib.Mpeg4.File.Create(file_m4a);
                    if (m4a != null)
                    {
                        m4a.Tag.Title = song.Title;
                        m4a.Tag.TitleSort = song.Alias;
                        //m4a.Tag. = song.URL;
                        m4a.Tag.Comment = song.URL;
                        m4a.Tag.Track = (uint)song.Track;
                        m4a.Tag.Performers = song.Artists.Select(a => a.Name).ToArray();
                        m4a.Tag.Album = song.Album?.Title;
                        m4a.Tag.AlbumArtists = [song.Album?.Artist];
                        m4a.Tag.AlbumSort = song.Album?.Subtitle;
                        if (!string.IsNullOrEmpty(song.Album?.Cover))
                        {
                            var data = await song.Album.DownloadCover();
                            if (data is not null)
                            {
                                m4a.Tag.Pictures = [new TagLib.Picture(data)];
                            }
                        }
                        m4a.Save();
                    }
                    var file_m4a_new = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file_m4a) ?? string.Empty, $"{song.ID} - {song.Track:#00}_{song.Title}.m4a");
                    File.Move(file_m4a, file_m4a_new, true);
                    _m4a_filename = file_m4a_new;
                    Dispatcher.Invoke(() => { TxtStatus.Text = "简单更新元数据和文件名称完成"; });
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"更新元数据失败！{Environment.NewLine}{e.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        return (result);
    }

}