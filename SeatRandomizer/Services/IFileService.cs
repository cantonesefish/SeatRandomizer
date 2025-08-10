// Services/IFileService.cs
using SeatRandomizer.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SeatRandomizer.Services;

public interface IFileService
{
    Task<List<Person>> LoadPeopleAsync(string filePath);
    Task<AppConfig> LoadConfigAsync(string configPath);
}

public class AppConfig
{
    public int Rows { get; set; } = 5;
    public int Columns { get; set; } = 6;
    public List<(int Row, int Col)> DisabledSeats { get; set; } = [];
    public List<(int Start, int End)> AisleColumns { get; set; } = [];
    public List<(int Start, int End)> AisleRows { get; set; } = [];
}