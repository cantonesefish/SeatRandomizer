// Views/MainWindow.axaml.cs
using Avalonia.Controls;
using SeatRandomizer.ViewModels;
using System;
using System.Threading.Tasks;
using SysIO = System.IO; // Alias for System.IO
using System.Linq;
using Avalonia;
using System.Collections.Generic;
using Avalonia.Threading;
using Avalonia.Interactivity;
using Avalonia.Media;

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

    public MainWindow()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
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

    private void OnSeatsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        System.Console.WriteLine("View: Seats CollectionChanged, initializing layout.");
        Avalonia.Threading.Dispatcher.UIThread.Post(InitializeOrResetLayout);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.Rows) ||
            e.PropertyName == nameof(MainWindowViewModel.Columns) ||
            e.PropertyName == nameof(MainWindowViewModel.CurrentConfig))
        {
            System.Console.WriteLine("View: Config changed, initializing layout.");
            Avalonia.Threading.Dispatcher.UIThread.Post(InitializeOrResetLayout);
        }
    }

    private void RearrangeButton_Click(object sender, RoutedEventArgs e)
    {
        System.Console.WriteLine("View: Rearrange button clicked.");
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RearrangeSeats();
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateSeatContent);
        }
    }

    // --- 修改：更新导出按钮点击事件 ---
    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        System.Console.WriteLine("View: Export button clicked.");
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.ExportToExcel(this); // 传递 this 作为 parent window
        }
    }
    // --- 修改结束 ---

    // --- 修改：更新编辑按钮点击事件 ---
    private async void EditConfigButton_Click(object sender, RoutedEventArgs e) =>
        await EditFileWithConfirmationAsync("config.yaml");

    private async void EditPeopleButton_Click(object sender, RoutedEventArgs e) =>
        await EditFileWithConfirmationAsync("people.csv");
    // --- 修改结束 ---

    // --- 修改：使用自定义 MessageBox ---
    private async Task EditFileWithConfirmationAsync(string fileName)
    {
        System.Console.WriteLine($"View: Request to edit '{fileName}'.");

        // 获取本地化字符串 (简化处理，实际应用中可从资源获取)
        string title = "确认";
        string message = $"您即将编辑 '{fileName}' 文件。修改完成后，请重启应用程序以使更改生效。是否继续？";
        string yesText = "确认";
        string noText = "取消";

        // 显示自定义消息框
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
    // --- 修改结束 ---

    private async Task OpenFileInDefaultEditor(string fileName)
    {
        try
        {
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
}