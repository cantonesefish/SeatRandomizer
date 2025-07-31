using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CsvHelper;
using Microsoft.Win32;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SeatRandomizer
{
    public partial class MainWindow : Window
    {
        private AppConfig config;
        private List<Student> students = new List<Student>();
        private List<SeatControl> availableSeats = new List<SeatControl>();
        private string configPath = "config.yaml";
        private string defaultCsvPath = "student.csv";
        private const double SeatWidth = 80;
        private const double SeatHeight = 80;
        private const double AisleWidth = 30; // 过道宽度
        private const double ToolbarHeight = 40;
        private const double LecternHeight = 60;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeApplication();
            LoadConfiguration();
            LoadStudents();
            SetupSeatGrid();

            // 调整窗口大小
            AdjustWindowSize();
        }

        private void AdjustWindowSize()
        {
            // 计算座位区域所需宽度
            int totalCols = config.Cols + (config.Cols / 2);
            double seatsWidth = config.Cols * SeatWidth + (config.Cols / 2) * AisleWidth;

            // 计算座位区域所需高度
            double seatsHeight = config.Rows * SeatHeight;

            // 设置窗口大小，添加边距和工具栏、讲台高度
            double windowWidth = Math.Max(800, seatsWidth + 40); // 40是左右边距
            double windowHeight = ToolbarHeight + LecternHeight + seatsHeight + 80; // 80是上下边距

            // 限制最大窗口大小为屏幕尺寸的90%
            double maxWidth = SystemParameters.PrimaryScreenWidth * 0.9;
            double maxHeight = SystemParameters.PrimaryScreenHeight * 0.9;

            windowWidth = Math.Min(windowWidth, maxWidth);
            windowHeight = Math.Min(windowHeight, maxHeight);

            // 设置窗口大小
            this.Width = windowWidth;
            this.Height = windowHeight;

            // 将窗口居中显示
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void InitializeApplication()
        {
            // 首次运行时生成配置文件
            if (!File.Exists(configPath))
            {
                var defaultConfig = new AppConfig
                {
                    Rows = 5,
                    Cols = 6,
                    ExcludedColumns = new List<int> { 3 } // 默认第3列最后一排不可选
                };

                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                string yaml = serializer.Serialize(defaultConfig);
                File.WriteAllText(configPath, yaml);
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                string yaml = File.ReadAllText(configPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                config = deserializer.Deserialize<AppConfig>(yaml);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置文件出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                config = new AppConfig(); // 使用默认配置
            }
        }

        private void LoadStudents()
        {
            string csvPath;

            // 确定学生名单路径
            if (string.IsNullOrWhiteSpace(config.StudentCsvPath))
            {
                csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, defaultCsvPath);
            }
            else
            {
                csvPath = config.StudentCsvPath;
                // 如果是相对路径，转换为绝对路径
                if (!Path.IsPathRooted(csvPath))
                {
                    csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, csvPath);
                }
            }

            // 如果学生名单不存在，创建默认文件
            if (!File.Exists(csvPath))
            {
                CreateDefaultStudentCsv(csvPath);
            }

            // 读取学生名单
            try
            {
                using (var reader = new StreamReader(csvPath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    students = csv.GetRecords<Student>().ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取学生名单出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateDefaultStudentCsv(string path)
        {
            using (var writer = new StreamWriter(path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                // 写入CSV头部
                csv.WriteField("学号");
                csv.WriteField("姓名");
                csv.WriteField("性别");
                csv.NextRecord();

                // 添加示例学生
                csv.WriteField("2023001");
                csv.WriteField("张三");
                csv.WriteField("男");
                csv.NextRecord();

                csv.WriteField("2023002");
                csv.WriteField("李四");
                csv.WriteField("女");
                csv.NextRecord();

                csv.WriteField("2023003");
                csv.WriteField("王五");
                csv.WriteField("男");
                csv.NextRecord();

                csv.WriteField("2023004");
                csv.WriteField("赵六");
                csv.WriteField("女");
            }

            MessageBox.Show($"已创建默认学生名单文件: {path}\n请编辑文件添加更多学生信息。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SetupSeatGrid()
        {
            seatGridContainer.Children.Clear();
            seatGridContainer.RowDefinitions.Clear();
            seatGridContainer.ColumnDefinitions.Clear();
            availableSeats.Clear();

            // 计算实际需要的总列数 (每2列座位后加1个空列)
            // 例如: 8列 -> 8个座位列 + 4个过道列 (位置2,5,8,11) = 12总列
            int totalCols = config.Cols + (config.Cols / 2);

            // 设置行定义
            for (int row = 0; row < config.Rows; row++)
            {
                seatGridContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SeatHeight) });
            }

            // 设置列定义 (座位列和空列交替)
            int colIndex = 0;
            for (int col = 0; col < config.Cols; col++)
            {
                // 座位列
                seatGridContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SeatWidth) });
                colIndex++;
                // 每两列座位后添加一个空列(过道)
                // 注意：不要在最后一列后面加过道
                if ((col + 1) % 2 == 0 && col < config.Cols - 1)
                {
                    seatGridContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AisleWidth) });
                    colIndex++;
                }
            }

            // 创建座位网格
            for (int row = 0; row < config.Rows; row++)
            {
                int seatColIndex = 0; // 每行开始时重置座位列索引 (0-based)
                for (int col = 0; col < totalCols; col++)
                {
                    // 判断当前Grid列索引是否为过道
                    if (IsAisleColumn(col, config.Cols))
                    {
                        continue; // 跳过过道位置，不创建座位
                    }

                    // 计算配置中的列号 (1-based)，用于显示和排除逻辑
                    int configCol = seatColIndex + 1;

                    // 判断该座位是否被排除
                    // 当前逻辑：仅最后一排 (row == config.Rows - 1) 中，列号在 ExcludedColumns 列表里的座位被排除
                    bool isExcluded = config.ExcludedColumns.Contains(configCol) && row == config.Rows - 1;

                    var seat = new SeatControl
                    {
                        Row = row,         // 逻辑行号 (0-based)
                        Column = seatColIndex, // 逻辑列号 (0-based, 不包括过道列)
                        IsExcluded = isExcluded
                    };

                    // 设置座位在Grid中的位置
                    Grid.SetRow(seat, row);
                    Grid.SetColumn(seat, col);

                    // 设置座位样式
                    if (isExcluded)
                    {
                        seat.SeatText = "不可选";
                        seat.Background = Brushes.LightGray;
                    }

                    seatGridContainer.Children.Add(seat);

                    if (!isExcluded)
                    {
                        availableSeats.Add(seat);
                    }

                    seatColIndex++; // 只有在处理完一个座位后，才增加座位列索引
                }
            }
        }

        // 判断给定的 Grid 列索引是否对应一个过道列
        private bool IsAisleColumn(int gridColumnIndex, int seatColumns)
        {
            // 每2列座位后有一个空列(过道)。
            // 座位列索引: 0,1, 2,3, 4,5, 6,7 ...
            // Grid列索引: 0,1, 2, 3,4, 5, 6,7, 8, 9,10, 11 ...
            //             S,S, A, S,S, A, S,S, A, S,S,  A  (S=Seat, A=Aisle)
            // 过道列索引:      2     5     8      11
            // 规律：(gridColumnIndex + 1) % 3 == 0
            // 同时确保索引在有效范围内，防止超出计算的总列数。
            // totalCols = seatColumns + (seatColumns / 2)
            // 最后一个可能的过道索引是 totalCols - 1 (如果 totalCols 是过道) 或 totalCols - 2 (如果最后一个元素是座位)
            // 由于我们只在 seatColumns-1 列前添加过道，最后一个元素必是座位。
            // 所以最大的过道索引是 totalCols - 2
            int totalCols = seatColumns + (seatColumns / 2);
            return (gridColumnIndex > 0) &&
                   ((gridColumnIndex + 1) % 3 == 0) &&
                   (gridColumnIndex < totalCols); // 确保在总列数范围内
        }

        private void BtnRandomize_Click(object sender, RoutedEventArgs e)
        {
            if (students.Count == 0)
            {
                MessageBox.Show("没有学生数据，请先添加学生信息。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (availableSeats.Count < students.Count)
            {
                MessageBox.Show($"座位数量({availableSeats.Count})不足容纳所有学生({students.Count})。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 获取随机种子
            string seed = txtSeed.Text.Trim();

            // 随机排序学生
            var shuffledStudents = ShuffleStudents(students, seed);

            // 使用相同的随机种子对座位进行随机排序
            var shuffledSeats = ShuffleSeats(availableSeats, seed);

            // 分配座位
            for (int i = 0; i < shuffledSeats.Count; i++)
            {
                if (i < shuffledStudents.Count)
                {
                    var student = shuffledStudents[i];
                    var seat = shuffledSeats[i];
                    // 显示格式: "学号 姓名 (行,列)"
                    int displayRow = seat.Row + 1; // 行号从1开始显示
                    // 列号显示逻辑修正：通常从左到右，1,2,3...
                    int displayCol = seat.Column + 1; // 列号从1开始显示
                    seat.SeatText = $"{student.Id} {student.Name}\n({displayRow},{displayCol})";
                    // 根据性别设置颜色
                    if (student.Gender == "男")
                    {
                        seat.Background = new SolidColorBrush(Color.FromRgb(173, 216, 230)); // 淡蓝色
                    }
                    else if (student.Gender == "女")
                    {
                        seat.Background = new SolidColorBrush(Color.FromRgb(255, 182, 193)); // 粉红色
                    }
                }
                else
                {
                    // 清空多余座位
                    shuffledSeats[i].SeatText = "";
                    shuffledSeats[i].Background = Brushes.White;
                }
            }
        }

        // 添加座位随机排序方法
        private List<SeatControl> ShuffleSeats(List<SeatControl> seats, string seed)
        {
            // 如果没有种子，使用随机种子
            if (string.IsNullOrWhiteSpace(seed))
            {
                return seats.OrderBy(s => Guid.NewGuid()).ToList();
            }

            // 使用种子生成确定性的随机序列
            using (var sha256 = SHA256.Create())
            {
                byte[] seedBytes = Encoding.UTF8.GetBytes(seed);
                byte[] hash = sha256.ComputeHash(seedBytes);

                // 使用哈希值作为随机数生成器的种子
                int seedValue = BitConverter.ToInt32(hash, 0);
                Random random = new Random(seedValue);

                // 创建带权重的列表用于排序
                var weightedSeats = seats.Select(s => new
                {
                    Seat = s,
                    Weight = random.NextDouble()
                }).ToList();

                return weightedSeats.OrderBy(ws => ws.Weight).Select(ws => ws.Seat).ToList();
            }
        }

        private List<Student> ShuffleStudents(List<Student> students, string seed)
        {
            // 如果没有种子，使用随机种子
            if (string.IsNullOrWhiteSpace(seed))
            {
                return students.OrderBy(s => Guid.NewGuid()).ToList();
            }

            // 使用种子生成确定性的随机序列
            using (var sha256 = SHA256.Create())
            {
                byte[] seedBytes = Encoding.UTF8.GetBytes(seed);
                byte[] hash = sha256.ComputeHash(seedBytes);

                // 使用哈希值作为随机数生成器的种子
                int seedValue = BitConverter.ToInt32(hash, 0);
                Random random = new Random(seedValue);

                // 创建带权重的列表用于排序
                var weightedStudents = students.Select(s => new
                {
                    Student = s,
                    Weight = random.NextDouble()
                }).ToList();

                return weightedStudents.OrderBy(ws => ws.Weight).Select(ws => ws.Student).ToList();
            }
        }

        private void BtnEditConfig_Click(object sender, RoutedEventArgs e)
        {
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string fullPath = Path.GetFullPath(configPath);

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{fullPath}\"",
                    UseShellExecute = true
                });

                MessageBox.Show($"已用记事本打开配置文件:\n{fullPath}\n修改后请重新点击'随机排序'按钮。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开配置文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            // 修复问题：确保导出的座位布局与窗口显示完全一致
            // 1. 强制完成布局更新
            this.UpdateLayout();
            seatScrollViewer.UpdateLayout();

            // 2. 计算完整内容尺寸
            double seatsWidth = config.Cols * SeatWidth + (config.Cols / 2) * AisleWidth;
            double seatsHeight = config.Rows * SeatHeight;
            double lecternTotalHeight = lecternBorder.ActualHeight + lecternBorder.Margin.Top + lecternBorder.Margin.Bottom;

            // 3. 计算总尺寸
            double width = Math.Max(seatsWidth, 100);
            double height = lecternTotalHeight + seatsHeight;

            // 4. 创建一个临时容器，包含讲台和完整座位区域
            Grid tempContainer = new Grid();
            tempContainer.Width = width;
            tempContainer.Height = height;

            // 添加行定义
            tempContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(lecternTotalHeight) });
            tempContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(seatsHeight) });

            // 复制讲台
            Border lecternCopy = new Border
            {
                Background = lecternBorder.Background,
                BorderBrush = lecternBorder.BorderBrush,
                BorderThickness = lecternBorder.BorderThickness,
                CornerRadius = lecternBorder.CornerRadius,
                Margin = new Thickness(0, lecternBorder.Margin.Top, 0, lecternBorder.Margin.Bottom)
            };

            TextBlock lecternText = new TextBlock
            {
                Text = "讲台",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            lecternCopy.Child = lecternText;
            Grid.SetRow(lecternCopy, 0);
            tempContainer.Children.Add(lecternCopy);

            // 创建座位区域
            Grid seatsCopy = new Grid();
            Grid.SetRow(seatsCopy, 1);

            // 设置行定义
            for (int row = 0; row < config.Rows; row++)
            {
                seatsCopy.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SeatHeight) });
            }

            // 设置列定义 (座位列和空列交替)
            int colIndex = 0;
            for (int col = 0; col < config.Cols; col++)
            {
                // 座位列
                seatsCopy.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SeatWidth) });
                colIndex++;
                // 每两列座位后添加一个空列(过道)
                // 注意：不要在最后一列后面加过道
                if ((col + 1) % 2 == 0 && col < config.Cols - 1)
                {
                    seatsCopy.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AisleWidth) });
                    colIndex++;
                }
            }

            // 创建座位网格（应用与原始UI完全相同的过道逻辑）
            int totalCols = config.Cols + (config.Cols / 2);
            for (int row = 0; row < config.Rows; row++)
            {
                int seatColIndex = 0; // 每行开始时重置座位列索引 (0-based)
                for (int col = 0; col < totalCols; col++)
                {
                    // 判断当前Grid列索引是否为过道
                    if (IsAisleColumn(col, config.Cols))
                    {
                        continue; // 跳过过道位置，不创建座位
                    }

                    // 查找原始座位网格中对应的座位
                    SeatControl originalSeat = seatGridContainer.Children
                        .OfType<SeatControl>()
                        .FirstOrDefault(s => Grid.GetRow(s) == row && Grid.GetColumn(s) == col);

                    if (originalSeat != null)
                    {
                        SeatControl seatCopy = new SeatControl
                        {
                            Row = originalSeat.Row,
                            Column = originalSeat.Column,
                            IsExcluded = originalSeat.IsExcluded,
                            SeatText = originalSeat.SeatText,
                            Background = originalSeat.Background
                        };

                        Grid.SetRow(seatCopy, row);
                        Grid.SetColumn(seatCopy, col);
                        seatsCopy.Children.Add(seatCopy);
                    }

                    seatColIndex++; // 只有在处理完一个座位后，才增加座位列索引
                }
            }

            tempContainer.Children.Add(seatsCopy);

            // 5. 确保临时容器已布局
            tempContainer.Measure(new Size(width, height));
            tempContainer.Arrange(new Rect(0, 0, width, height));

            // 6. 创建渲染位图
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(width),
                (int)Math.Ceiling(height),
                96d, 96d,
                PixelFormats.Pbgra32);

            // 7. 渲染临时容器
            renderBitmap.Render(tempContainer);

            // 8. 保存文件
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG图片|*.png|JPEG图片|*.jpg|BMP图片|*.bmp",
                Title = "保存座位布局",
                FileName = $"座位布局_{DateTime.Now:yyyyMMddHHmmss}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    BitmapEncoder encoder;
                    string ext = Path.GetExtension(saveFileDialog.FileName).ToLower();

                    switch (ext)
                    {
                        case ".jpg":
                            encoder = new JpegBitmapEncoder();
                            break;
                        case ".bmp":
                            encoder = new BmpBitmapEncoder();
                            break;
                        default:
                            encoder = new PngBitmapEncoder();
                            break;
                    }

                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                    using (FileStream fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }

                    MessageBox.Show($"座位布局已成功保存到:\n{saveFileDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出座位布局失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class SeatControl : UserControl
    {
        private TextBlock textBlock;

        public int Row { get; set; }
        public int Column { get; set; } // 逻辑列号(0-based, 不包括空列)
        public bool IsExcluded { get; set; }

        public string SeatText
        {
            get { return textBlock.Text; }
            set { textBlock.Text = value; }
        }

        public SeatControl()
        {
            Border border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Background = Brushes.White
            };

            textBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(5),
                FontSize = 12
            };

            border.Child = textBlock;
            Content = border;
        }
    }
}