// Views/MainWindow.axaml.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DocumentFormat.OpenXml.Office2010.PowerPoint;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using SeatRandomizer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysIO = System.IO;

namespace SeatRandomizer.Views;

public class SeatControlRef
{
    public Border Border { get; set; }
    public TextBlock NumberTextBlock { get; set; }
    public TextBlock NameTextBlock { get; set; }
    public SeatViewModel ViewModel { get; set; }
    public SeatControlRef(Border border, TextBlock numberTb, TextBlock nameTb, SeatViewModel vm)
    {
        Border = border;
        NumberTextBlock = numberTb;
        NameTextBlock = nameTb;
        ViewModel = vm;
        Border.Tag = vm;
    }
}

public partial class MainWindow : Window
{
    private SeatControlRef?[,]? _seatControlRefs;
    private bool _layoutInitialized = false;

    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;

    private WindowState _originalWindowState;
    private SystemDecorations _originalSystemDecorations;

    private CancellationTokenSource? _pollingCts;
    private bool _userRequestedStop = false;

    public MainWindow()
    {
        InitializeComponent();

        _originalWindowState = this.WindowState;
        _originalSystemDecorations = this.SystemDecorations;

        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
        VideoPlayer.MediaPlayer = _mediaPlayer;

        _mediaPlayer.Playing += OnVideoPlaybackStarted;

        this.KeyDown += OnKeyDown;

        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnVideoPlaybackEnded(object? sender, EventArgs e)
    {
        System.Console.WriteLine("View: Video playback ended (event fired, but not used for exit logic).");
    }

    private void OnVideoPlaybackStarted(object? sender, EventArgs e)
    {
        System.Console.WriteLine("View: Video playback started.");
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        System.Console.WriteLine("View: DataContextChanged.");
        if (DataContext is MainWindowViewModel vm)
        {
            System.Console.WriteLine("View: Subscribing to VM events.");
            vm.Seats.CollectionChanged += OnSeatsCollectionChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            System.Console.WriteLine("View: ESC key pressed. Setting stop flag.");
            _userRequestedStop = true;
        }
    }

    private async Task StopVideoPlaybackAsync()
    {
        System.Console.WriteLine("View: StopVideoPlaybackAsync called for cleanup.");

        _pollingCts?.Cancel();

        try
        {
            System.Console.WriteLine("View: Attempting to stop MediaPlayer...");
            _mediaPlayer?.Stop();
            System.Console.WriteLine("View: MediaPlayer.Stop() called in cleanup.");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"View: Exception during MediaPlayer stop in cleanup: {ex}");
        }


        try
        {
            System.Console.WriteLine("View: *** RESETTING VideoPlayer.MediaPlayer to clear display ***");
            VideoPlayer.MediaPlayer = null; 
            await Task.Delay(50); 
            VideoPlayer.MediaPlayer = _mediaPlayer; 
            System.Console.WriteLine("View: VideoPlayer.MediaPlayer reference reset.");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"View: Exception resetting VideoPlayer.MediaPlayer: {ex}");
        }

        try { await Task.Delay(50); } catch { }

        try
        {
            System.Console.WriteLine("View: Restoring UI visibility...");
            this.VideoPlayer.IsVisible = false;
            this.SeatScrollView.IsVisible = true;
            System.Console.WriteLine("View: UI visibility restored.");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"View: Exception restoring UI visibility: {ex}");
        }


        System.Console.WriteLine("View: StopVideoPlaybackAsync cleanup finished.");
    }

    private void OnSeatsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        System.Console.WriteLine("View: Seats CollectionChanged, initializing layout.");
        Avalonia.Threading.Dispatcher.UIThread.Post(InitializeOrResetLayout);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.Rows) ||
                e.PropertyName == nameof(MainWindowViewModel.Columns) ||
                e.PropertyName == nameof(MainWindowViewModel.CurrentConfig))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(InitializeOrResetLayout);
            }
        }
    }

    private string? GetRandomVideoPath()
    {
        try
        {
            var videoFolderPath = SysIO.Path.Combine(SysIO.Directory.GetCurrentDirectory(), "video");
            if (!SysIO.Directory.Exists(videoFolderPath))
            {
                System.Console.WriteLine("View: Video folder does not exist.");
                return null;
            }

            var mp4Files = SysIO.Directory.GetFiles(videoFolderPath, "*.mp4");
            if (mp4Files.Length == 0)
            {
                System.Console.WriteLine("View: No .mp4 files found in video folder.");
                return null;
            }

            var random = new Random();
            var randomFile = mp4Files[random.Next(mp4Files.Length)];
            System.Console.WriteLine($"View: Selected random video: {randomFile}");
            return randomFile;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"View: Error getting random video: {ex.Message}");
            return null;
        }
    }

    private async void RearrangeButton_Click(object sender, RoutedEventArgs e)
    {
        System.Console.WriteLine("View: Rearrange button clicked.");
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RearrangeSeats();
            UpdateSeatContent();

            bool shouldPlayVideo = this.PlayVideoOnRearrangeCheckBox?.IsChecked ?? false;

            if (shouldPlayVideo)
            {
                System.Console.WriteLine("View: PlayVideoOnRearrange is checked. Starting video playback...");
                _ = PlayVideoAsync(); 
            }
            else
            {
                System.Console.WriteLine("View: PlayVideoOnRearrange is unchecked. Rearrangement complete.");
            }
        }
    }
    private async Task PlayVideoAsync()
    {
        System.Console.WriteLine("View: *** FINAL PlayVideoAsync (polling) started. ***");

        _userRequestedStop = false;
        _pollingCts = new CancellationTokenSource();

        try
        {
            var videoPath = GetRandomVideoPath();
            if (string.IsNullOrEmpty(videoPath))
            {
                System.Console.WriteLine("View: No video found. Playback aborted.");
                await StopVideoPlaybackAsync();
                return;
            }
            VideoPlayer.MediaPlayer = _mediaPlayer;
            System.Console.WriteLine("View: Ensured VideoPlayer.MediaPlayer is set.");
            this.SeatScrollView.IsVisible = false;
            this.VideoPlayer.IsVisible = true;

            System.Console.WriteLine($"View: Playing video: {videoPath}");
            using var media = new LibVLCSharp.Shared.Media(_libVLC, new Uri(videoPath));

            _mediaPlayer?.Play(media);
            System.Console.WriteLine("View: Playback started.");

            using var overallTimeoutCts = new CancellationTokenSource(TimeSpan.FromHours(4));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_pollingCts.Token, overallTimeoutCts.Token);
            var cancellationToken = linkedCts.Token;

            System.Console.WriteLine("View: Starting polling loop...");
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken); 

                var state = _mediaPlayer?.State;
                System.Console.WriteLine($"View: Polled MediaPlayer state: {state}");

                if (_userRequestedStop)
                {
                    System.Console.WriteLine("View: Stop requested by user (ESC).");
                    _mediaPlayer?.Stop();
                    break;
                }

                if (state == VLCState.Ended || state == VLCState.Stopped)
                {
                    System.Console.WriteLine("View: MediaPlayer state is Ended or Stopped.");
                    break;
                }
            }
            System.Console.WriteLine("View: Polling loop finished.");

        }
        catch (OperationCanceledException)
        {
            System.Console.WriteLine("View: PlayVideoAsync polling was cancelled (timeout or user stop).");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"View: Exception in PlayVideoAsync polling: {ex}");
        }
        finally
        {
            System.Console.WriteLine("View: PlayVideoAsync (polling) finished. Calling StopVideoPlaybackAsync.");
            await StopVideoPlaybackAsync();
        }
    }


    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        System.Console.WriteLine("View: Export button clicked.");
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.ExportToExcel(this);
        }
    }

    private async void EditConfigButton_Click(object sender, RoutedEventArgs e) =>
        await EditFileWithConfirmationAsync("config.yaml");

    private async void EditPeopleButton_Click(object sender, RoutedEventArgs e) =>
        await EditFileWithConfirmationAsync("people.csv");

    private async void OpenVideoFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var videoFolderPath = SysIO.Path.Combine(SysIO.Directory.GetCurrentDirectory(), "video");
            if (!SysIO.Directory.Exists(videoFolderPath))
            {
                SysIO.Directory.CreateDirectory(videoFolderPath);
            }
            await OpenFileInDefaultEditor(videoFolderPath);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"View: Error opening video folder: {ex.Message}");
        }
    }

    private async Task EditFileWithConfirmationAsync(string fileName)
    {
        System.Console.WriteLine($"View: Request to edit '{fileName}'.");

        string title = "确认";
        string message = $"您即将编辑 '{fileName}' 文件。修改完成后，请重启应用程序以使更改生效。是否继续？";
        string yesText = "确认";
        string noText = "取消";

        bool result = await MessageBoxWindow.ShowAsync(this, title, message, yesText, noText);

        if (result)
        {
            System.Console.WriteLine("View: User confirmed editing.");
            await OpenFileInDefaultEditor(fileName);
        }
        else
        {
            System.Console.WriteLine("View: User cancelled editing.");
        }
    }

    private async Task OpenFileInDefaultEditor(string fileName)
    {
        try
        {
            if (SysIO.Directory.Exists(fileName))
            {
                await LaunchFolderFallback(fileName);
                return;
            }

            var fullPath = SysIO.Path.GetFullPath(fileName);
            if (!SysIO.File.Exists(fullPath)) SysIO.File.WriteAllText(fullPath, "");
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null) { await LaunchFileFallback(fullPath); return; }
            var storageFile = await topLevel.StorageProvider.TryGetFileFromPathAsync(new Uri(fullPath));
            if (storageFile != null && topLevel.Launcher != null)
                await topLevel.Launcher.LaunchFileAsync(storageFile);
            else await LaunchFileFallback(fullPath);
        }
        catch (Exception ex) { System.Console.WriteLine($"View: Error opening file {fileName}: {ex.Message}"); }
    }

    private async Task LaunchFolderFallback(string folderPath)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                process.StartInfo.FileName = "explorer.exe";
                process.StartInfo.Arguments = $"\"{folderPath}\"";
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                process.StartInfo.FileName = "xdg-open";
                process.StartInfo.Arguments = $"\"{folderPath}\"";
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                process.StartInfo.FileName = "open";
                process.StartInfo.Arguments = $"\"{folderPath}\"";
            }
            else
            {
                System.Console.WriteLine("View: Unsupported OS for folder opening.");
                return;
            }
            process.StartInfo.UseShellExecute = false;
            await Task.Run(() => process.Start());
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"View: Folder fallback failed: {ex.Message}");
        }
    }

    private static async Task LaunchFileFallback(string filePath)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = filePath;
            process.StartInfo.UseShellExecute = true;
            await Task.Run(() => process.Start());
        }
        catch (Exception ex) { System.Console.WriteLine($"View: Fallback failed: {ex.Message}"); }
    }

    private void InitializeOrResetLayout()
    {
        System.Console.WriteLine("View: InitializeOrResetLayout called.");
        if (DataContext is not MainWindowViewModel vm) return;

        var config = vm.CurrentConfig;
        if (config.Rows <= 0 || config.Columns <= 0) return;

        SeatGrid.Children.Clear();
        SeatGrid.RowDefinitions.Clear();
        SeatGrid.ColumnDefinitions.Clear();

        int totalGridRows = config.Rows + config.AisleRows.Count;
        int totalGridColumns = config.Columns + config.AisleColumns.Count;

        _seatControlRefs = new SeatControlRef[totalGridRows, totalGridColumns];
        _layoutInitialized = true;

        for (int i = 0; i < totalGridRows; i++)
            SeatGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        for (int i = 0; i < totalGridColumns; i++)
            SeatGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        for (int i = 0; i < vm.Seats.Count; i++)
        {
            var seatVm = vm.Seats[i];
            int gridRow = seatVm.GridRow;
            int gridCol = seatVm.GridColumn;

            if (gridRow < 0 || gridRow >= totalGridRows || gridCol < 0 || gridCol >= totalGridColumns)
            {
                System.Console.WriteLine($"View: Skipping Seat VM at invalid Grid coord ({gridRow},{gridCol})");
                continue;
            }

            var border = new Border
            {
                Margin = new Thickness(5),
                Padding = new Thickness(6),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5),
                Background = seatVm.BackgroundBrush,
                BorderBrush = seatVm.BorderBrush,
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Spacing = 5
            };

            var numberTextBlock = new TextBlock
            {
                Text = seatVm.DisplayNumber,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontSize = 14,
                IsVisible = seatVm.IsTextVisible
            };

            var nameTextBlock = new TextBlock
            {
                Text = seatVm.DisplayName,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontSize = 14,
                IsVisible = seatVm.IsTextVisible
            };

            stackPanel.Children.Add(numberTextBlock);
            stackPanel.Children.Add(nameTextBlock);

            Grid.SetRow(stackPanel, 1);
            Grid.SetColumn(stackPanel, 1);
            grid.Children.Add(stackPanel);

            border.Child = grid;

            Grid.SetRow(border, gridRow);
            Grid.SetColumn(border, gridCol);

            SeatGrid.Children.Add(border);
            _seatControlRefs[gridRow, gridCol] = new SeatControlRef(border, numberTextBlock, nameTextBlock, seatVm);
        }
        System.Console.WriteLine("View: InitializeOrResetLayout finished.");
    }

    private void UpdateSeatContent()
    {
        System.Console.WriteLine("View: UpdateSeatContent called.");
        if (!_layoutInitialized || _seatControlRefs == null) return;

        for (int r = 0; r < _seatControlRefs.GetLength(0); r++)
        {
            for (int c = 0; c < _seatControlRefs.GetLength(1); c++)
            {
                var controlRef = _seatControlRefs[r, c];
                if (controlRef == null) continue;

                var seatVm = controlRef.ViewModel;
                controlRef.Border.Background = seatVm.BackgroundBrush;
                controlRef.Border.BorderBrush = seatVm.BorderBrush;
                controlRef.NumberTextBlock.Text = seatVm.DisplayNumber;
                controlRef.NumberTextBlock.IsVisible = seatVm.IsTextVisible;
                controlRef.NameTextBlock.Text = seatVm.DisplayName;
                controlRef.NameTextBlock.IsVisible = seatVm.IsTextVisible;
            }
        }
        System.Console.WriteLine("View: UpdateSeatContent finished.");
    }

    protected override void OnClosed(EventArgs e)
    {
        System.Console.WriteLine("View: Window is closing, cleaning up resources...");

        _pollingCts?.Cancel();

        if (_mediaPlayer != null)
        {
            _mediaPlayer.Playing -= OnVideoPlaybackStarted;
        }
        this.KeyDown -= OnKeyDown;

        try
        {
            System.Console.WriteLine("View: Stopping and disposing MediaPlayer...");
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _mediaPlayer = null;
            System.Console.WriteLine("View: MediaPlayer stopped and disposed.");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"View: Exception disposing MediaPlayer: {ex}");
        }

        try
        {
            System.Console.WriteLine("View: Disposing LibVLC...");
            _libVLC?.Dispose();
            _libVLC = null;
            System.Console.WriteLine("View: LibVLC disposed.");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"View: Exception disposing LibVLC: {ex}");
        }

        _pollingCts?.Dispose();
        _pollingCts = null;

        base.OnClosed(e);
        System.Console.WriteLine("View: Window closed and base.OnClosed called.");
    }
}