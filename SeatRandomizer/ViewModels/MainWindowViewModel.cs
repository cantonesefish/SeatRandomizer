// ViewModels/MainWindowViewModel.cs
using ReactiveUI;
using SeatRandomizer.Models;
using SeatRandomizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using ClosedXML.Excel;
using Avalonia.Platform.Storage;
using Avalonia.Controls;


namespace SeatRandomizer.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IFileService? _fileService;
    private readonly ISeatArrangerService? _seatArrangerService;
    private readonly ILocalizationService? _localizationService;
    private string _title = "Seat Randomizer";
    private AppConfig _currentConfig = new();
    private List<Person> _loadedPeople = [];
    private bool _isSameSexAdjacentPreference = false;

    public bool IsSameSexAdjacentPreference
    {
        get => _isSameSexAdjacentPreference;
        set => this.RaiseAndSetIfChanged(ref _isSameSexAdjacentPreference, value);
    }

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public int Rows => _currentConfig.Rows;
    public int Columns => _currentConfig.Columns;
    public AppConfig CurrentConfig => _currentConfig;

    public ObservableCollection<SeatViewModel> Seats { get; } = [];

    // 枚举定义在类内部是好的
    private enum CellType
    {
        Seat,
        Aisle
    }

    // 设计时构造函数
    public MainWindowViewModel()
    {
        Title = "Seat Randomizer (Design)";
        _currentConfig = new AppConfig();
    }

    // 运行时构造函数
    public MainWindowViewModel(IFileService fileService, ISeatArrangerService seatArrangerService, ILocalizationService localizationService)
    {
        _fileService = fileService;
        _seatArrangerService = seatArrangerService;
        _localizationService = localizationService;
        Title = _localizationService?["AppName"] ?? "Seat Randomizer";
    }

    public async void LoadData()
    {
        System.Console.WriteLine("VM: LoadData started.");
        if (_fileService == null)
        {
            System.Console.WriteLine("VM: Design time, calling CreateSeatsFromConfig.");
            CreateSeatsFromConfig();
            return;
        }

        try
        {
            var people = await _fileService.LoadPeopleAsync("people.csv");
            _loadedPeople = people;
            System.Console.WriteLine($"VM: Loaded {people.Count} people.");

            var config = await _fileService.LoadConfigAsync("config.yaml");
            _currentConfig = config;
            System.Console.WriteLine($"VM: Loaded config: {config.Rows}x{config.Columns}");

            this.RaisePropertyChanged(nameof(Rows));
            this.RaisePropertyChanged(nameof(Columns));

            CreateSeatsFromConfig();
            System.Console.WriteLine("VM: LoadData finished.");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"VM: Error loading  {ex}");
            CreateSeatsFromConfig();
        }
    }

    private void CreateSeatsFromConfig()
    {
        System.Console.WriteLine("VM: CreateSeatsFromConfig started.");
        Seats.Clear();
        var config = _currentConfig;

        if (config.Rows <= 0 || config.Columns <= 0)
        {
            System.Console.WriteLine("VM: Invalid config dimensions.");
            return;
        }

        // --- 阶段一：计算布局 ---

        // 1. 计算最终的 Grid 行数和列数
        int totalGridRows = config.Rows + config.AisleRows.Count;
        int totalGridColumns = config.Columns + config.AisleColumns.Count;
        System.Console.WriteLine($"VM: Final Grid size: {totalGridRows} x {totalGridColumns}");

        // 2. 创建一个二维数组来表示 Grid 布局
        CellType[,] gridLayout = new CellType[totalGridRows, totalGridColumns];

        // 3. 初始化所有单元格为过道
        for (int r = 0; r < totalGridRows; r++)
        {
            for (int c = 0; c < totalGridColumns; c++)
            {
                gridLayout[r, c] = CellType.Aisle;
            }
        }

        // 4. 确定行过道在 Grid 中的位置
        List<int> gridAisleRowIndices = [];
        var sortedRowAisles = config.AisleRows.OrderBy(a => a.Start).ToList();
        int rowOffset = 0;
        for (int i = 0; i < sortedRowAisles.Count; i++)
        {
            var (Start, End) = sortedRowAisles[i];
            int gridAisleRowIndex = Start + rowOffset + 1;
            if (gridAisleRowIndex >= 0 && gridAisleRowIndex < totalGridRows)
            {
                gridAisleRowIndices.Add(gridAisleRowIndex);
                rowOffset++;
            }
        }
        System.Console.WriteLine($"VM: Grid Row Aisles will be at indices: {string.Join(", ", gridAisleRowIndices)}");

        // 5. 确定列过道在 Grid 中的位置
        List<int> gridAisleColIndices = [];
        var sortedColAisles = config.AisleColumns.OrderBy(a => a.Start).ToList();
        int colOffset = 0;
        for (int i = 0; i < sortedColAisles.Count; i++)
        {
            var (Start, End) = sortedColAisles[i];
            int gridAisleColIndex = Start + colOffset + 1;
            if (gridAisleColIndex >= 0 && gridAisleColIndex < totalGridColumns)
            {
                gridAisleColIndices.Add(gridAisleColIndex);
                colOffset++;
            }
        }
        System.Console.WriteLine($"VM: Grid Col Aisles will be at indices: {string.Join(", ", gridAisleColIndices)}");

        // 6. 在 GridLayout 中放置座位
        int logicalRowIndex = 0;
        for (int gridRow = 0; gridRow < totalGridRows; gridRow++)
        {
            if (gridAisleRowIndices.Contains(gridRow))
            {
                continue;
            }

            int logicalColIndex = 0;
            for (int gridCol = 0; gridCol < totalGridColumns; gridCol++)
            {
                if (gridAisleColIndices.Contains(gridCol))
                {
                    continue;
                }

                gridLayout[gridRow, gridCol] = CellType.Seat;
                logicalColIndex++;
            }
            logicalRowIndex++;
        }

        // --- 阶段二：根据布局创建 SeatViewModel ---
        System.Console.WriteLine("VM: Creating SeatViewModels based on layout...");
        logicalRowIndex = 0;
        for (int gridRow = 0; gridRow < totalGridRows; gridRow++)
        {
            bool isAisleRow = gridAisleRowIndices.Contains(gridRow);
            int logicalColIndex = 0;
            for (int gridCol = 0; gridCol < totalGridColumns; gridCol++)
            {
                bool isAisleCol = gridAisleColIndices.Contains(gridCol);
                CellType cellType = gridLayout[gridRow, gridCol];

                if (cellType == CellType.Aisle)
                {
                    var aisleSeat = new Seat { Row = -1, Column = -1, IsEnabled = false };
                    var aisleVm = new SeatViewModel(aisleSeat, _localizationService!, true, -1, -1)
                    {
                        GridRow = gridRow,
                        GridColumn = gridCol
                    };
                    Seats.Add(aisleVm);
                    System.Console.WriteLine($"VM: Added Aisle at Grid({gridRow},{gridCol})");
                }
                else if (cellType == CellType.Seat)
                {
                    bool isEnabled = !config.DisabledSeats.Contains((logicalRowIndex, logicalColIndex));
                    var seat = new Seat
                    {
                        Row = logicalRowIndex,
                        Column = logicalColIndex,
                        IsEnabled = isEnabled
                    };
                    var seatVm = new SeatViewModel(seat, _localizationService!, false, logicalRowIndex, logicalColIndex)
                    {
                        GridRow = gridRow,
                        GridColumn = gridCol
                    };
                    Seats.Add(seatVm);
                    System.Console.WriteLine($"VM: Added Seat at Grid({gridRow},{gridCol}) -> Logical({logicalRowIndex},{logicalColIndex}), Enabled: {isEnabled}");
                    logicalColIndex++;
                }
            }
            if (!isAisleRow)
            {
                logicalRowIndex++;
            }
        }

        System.Console.WriteLine($"VM: CreateSeatsFromConfig finished. Total SeatViewModels: {Seats.Count}");
    }

    public void RearrangeSeats()
    {
        System.Console.WriteLine("VM: RearrangeSeats started.");
        if (_fileService == null || _seatArrangerService == null)
        {
            System.Console.WriteLine("VM: Services not initialized.");
            return;
        }

        if (_loadedPeople == null || _loadedPeople.Count == 0)
        {
            System.Console.WriteLine("VM: No people to arrange.");
            return;
        }

        // --- 传递 IsSameSexAdjacentPreference 属性 ---
        var arrangedSeats = _seatArrangerService.ArrangeSeats(_loadedPeople, _currentConfig, _isSameSexAdjacentPreference);
        System.Console.WriteLine($"VM: Service arranged {arrangedSeats.Count} seats.");

        var arrangedSeatDict = new Dictionary<(int, int), Seat>();
        foreach (var s in arrangedSeats.Where(seat => seat.IsEnabled))
        {
            arrangedSeatDict[(s.Row, s.Column)] = s;
            System.Console.WriteLine($"  - Service has result for Logical({s.Row},{s.Column}): {s.Occupant?.Name ?? "Empty"}");
        }

        int updateCount = 0;
        foreach (var seatVm in Seats)
        {
            if (!seatVm.IsAisle && seatVm.IsEnabled)
            {
                if (arrangedSeatDict.TryGetValue((seatVm.LogicalRow, seatVm.LogicalColumn), out var arrangedSeat))
                {
                    seatVm.Occupant = arrangedSeat.Occupant;
                    updateCount++;
                    System.Console.WriteLine($"VM: Updated Seat VM Grid({seatVm.Row},{seatVm.Column}) <- Person {arrangedSeat.Occupant?.Name ?? "None"}");
                }
                else
                {
                    seatVm.Occupant = null;
                }
            }
            else if (seatVm.IsAisle || !seatVm.IsEnabled)
            {
                seatVm.Occupant = null;
            }
        }
        System.Console.WriteLine($"VM: RearrangeSeats finished. Updated {updateCount} seat view models.");
    }

    public async Task ExportToExcel(Window parentWindow)
    {
        System.Console.WriteLine("VM: ExportToExcel started.");
        try
        {
            // 1. 使用 SaveFileDialog 让用户选择路径
            var storageProvider = TopLevel.GetTopLevel(parentWindow)?.StorageProvider;
            if (storageProvider == null)
            {
                System.Console.WriteLine("VM: StorageProvider not available.");
                return;
            }

            var saveOptions = new FilePickerSaveOptions
            {
                Title = "导出座位布局到...",
                DefaultExtension = "xlsx",
                FileTypeChoices =
                [
                    new("Excel 文件")
                    {
                        Patterns = ["*.xlsx"]
                    }
                ],
                SuggestedFileName = $"座位布局_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            var file = await storageProvider.SaveFilePickerAsync(saveOptions);
            if (file == null)
            {
                System.Console.WriteLine("VM: User cancelled save dialog.");
                return; // 用户取消
            }

            var filePath = file.TryGetLocalPath();
            if (string.IsNullOrEmpty(filePath))
            {
                System.Console.WriteLine("VM: Could not get local file path.");
                return;
            }

            // 2. 执行导出逻辑
            await DoExportToPath(filePath);
            System.Console.WriteLine("VM: ExportToExcel finished successfully.");

        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"VM: Error during export: {ex}");
        }
    }

    private async Task DoExportToPath(string filePath)
    {
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("座位布局");

                int totalGridRows = _currentConfig.Rows + _currentConfig.AisleRows.Count;
                int totalGridColumns = _currentConfig.Columns + _currentConfig.AisleColumns.Count;

                // 填充座位数据
                for (int r = 1; r <= totalGridRows; r++)
                {
                    for (int c = 1; c <= totalGridColumns; c++)
                    {
                        worksheet.Cell(r, c).Value = ""; // Default to aisle
                    }
                }

                foreach (var seatVm in Seats)
                {
                    int excelRow = seatVm.GridRow + 1;
                    int excelCol = seatVm.GridColumn + 1;

                    if (seatVm.IsAisle)
                    {
                        // Keep as "过道"
                    }
                    else if (seatVm.IsEnabled)
                    {
                        if (seatVm.Occupant != null)
                        {
                            worksheet.Cell(excelRow, excelCol).Value = $"{seatVm.Occupant.Number}. {seatVm.Occupant.Name}";
                        }
                        else
                        {
                            worksheet.Cell(excelRow, excelCol).Value = "";
                        }
                    }
                    else
                    {
                        worksheet.Cell(excelRow, excelCol).Value = "";
                    }
                }

                // 添加讲台 (使用 i18n)
                string podiumText = _localizationService?["Podium"] ?? "讲台";
                int podiumRow = totalGridRows + 1;
                int podiumStartCol = 1;
                int podiumEndCol = totalGridColumns;

                var podiumRange = worksheet.Range(podiumRow, podiumStartCol, podiumRow, podiumEndCol);
                podiumRange.Merge();
                podiumRange.Value = podiumText;
                podiumRange.Style.Font.SetBold();
                podiumRange.Style.Font.SetFontSize(14);
                podiumRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                podiumRange.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                podiumRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                podiumRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thick);

                // 美化
                worksheet.Columns().AdjustToContents();
                worksheet.Row(podiumRow).Height = 30;
                var fullRange = worksheet.Range(1, 1, podiumRow, totalGridColumns);
                fullRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                fullRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Hair);

                workbook.SaveAs(filePath);
            }

            System.Console.WriteLine($"VM: File saved to {filePath}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"VM: Error saving file: {ex}");
        }
    }
}