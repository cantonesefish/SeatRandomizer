// Services/FileService.cs
using System.Globalization;
using CsvHelper;
using SeatRandomizer.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace SeatRandomizer.Services;

public class FileService : IFileService
{
    public async Task<List<Person>> LoadPeopleAsync(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<Person>().ToList();
        return records;
    }

    public async Task<AppConfig> LoadConfigAsync(string configPath)
    {
        var config = new AppConfig();

        if (!File.Exists(configPath))
        {
            System.Console.WriteLine($"Config file {configPath} not found, using defaults.");
            return config;
        }

        var yamlContent = await File.ReadAllTextAsync(configPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        if (yamlObject == null) return config;

        if (yamlObject.TryGetValue("layout", out var layoutObj) && layoutObj is Dictionary<object, object> layout)
        {
            if (layout.TryGetValue("rows", out var rowsObj) && int.TryParse(rowsObj.ToString(), out int r))
                config.Rows = r;
            if (layout.TryGetValue("columns", out var colsObj) && int.TryParse(colsObj.ToString(), out int c))
                config.Columns = c;
        }

        if (yamlObject.TryGetValue("disabled_seats", out var disabledSeatsObj) && disabledSeatsObj is List<object> disabledList)
        {
            foreach (var item in disabledList)
            {
                if (item is List<object> coordList && coordList.Count == 2)
                {
                    if (int.TryParse(coordList[0].ToString(), out int row) &&
                        int.TryParse(coordList[1].ToString(), out int col))
                    {
                        config.DisabledSeats.Add((row, col));
                    }
                }
            }
        }

        if (yamlObject.TryGetValue("aisles", out var aislesObj) && aislesObj is Dictionary<object, object> aislesDict)
        {
            if (aislesDict.TryGetValue("columns", out var colAislesObj) && colAislesObj is List<object> colAisleList)
            {
                foreach (var item in colAisleList)
                {
                    if (item is List<object> aislePair && aislePair.Count == 2)
                    {
                        if (int.TryParse(aislePair[0].ToString(), out int start) &&
                            int.TryParse(aislePair[1].ToString(), out int end))
                        {
                            config.AisleColumns.Add((start, end));
                        }
                    }
                }
            }

            if (aislesDict.TryGetValue("rows", out var rowAislesObj) && rowAislesObj is List<object> rowAisleList)
            {
                foreach (var item in rowAisleList)
                {
                    if (item is List<object> aislePair && aislePair.Count == 2)
                    {
                        if (int.TryParse(aislePair[0].ToString(), out int start) &&
                            int.TryParse(aislePair[1].ToString(), out int end))
                        {
                            config.AisleRows.Add((start, end));
                        }
                    }
                }
            }
        }

        return config;
    }
}