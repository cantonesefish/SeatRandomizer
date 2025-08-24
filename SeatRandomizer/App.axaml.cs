// App.axaml.cs
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SeatRandomizer.Services;
using SeatRandomizer.ViewModels;
using SeatRandomizer.Views;
using System.IO;
using LibVLCSharp.Shared;

namespace SeatRandomizer;

public partial class App : Application
{
    public override void Initialize()
    {
        LibVLCSharp.Shared.Core.Initialize();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            CreateDefaultFilesIfNotExists();

            var fileService = new FileService();
            var seatArrangerService = new SeatArrangerService();
            var localizationService = new LocalizationService(); 
            localizationService.SetCulture("zh");

            var mainWindowViewModel = new MainWindowViewModel(fileService, seatArrangerService, localizationService);
            var mainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };
            mainWindowViewModel.LoadData();

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void CreateDefaultFilesIfNotExists()
    {
        if (!File.Exists("people.csv"))
        {
            var defaultCsvContent = @"Number,Name,Sex
1,张三,male
2,李四,female
3,王五,male
4,赵六,male
5,孙七,female
6,周八,male
7,吴九,male
8,郑十,female";
            File.WriteAllText("people.csv", defaultCsvContent);
        }

        if (!File.Exists("config.yaml"))
        {
            var defaultYamlContent = @"layout:
  rows: 3
  columns: 4
disabled_seats:
  - [0, 0]
aisles:
  columns:
    - [1, 2]
  rows:
    - [1, 2]";
            File.WriteAllText("config.yaml", defaultYamlContent);
        }
        var videoDir = Path.Combine(Directory.GetCurrentDirectory(), "video");
        if (!Directory.Exists(videoDir))
        {
            Directory.CreateDirectory(videoDir);
        }
    }
}