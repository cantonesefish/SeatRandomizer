using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace SeatRandomizer
{
    public class AppConfig
    {
        [YamlMember(Alias = "rows", ApplyNamingConventions = false)]
        public int Rows { get; set; } = 5;

        [YamlMember(Alias = "columns", ApplyNamingConventions = false)]
        public int Cols { get; set; } = 6;

        [YamlMember(Alias = "excluded_columns", ApplyNamingConventions = false)]
        public List<int> ExcludedColumns { get; set; } = new List<int>();

        [YamlMember(Alias = "student_csv_path", ApplyNamingConventions = false)]
        public string StudentCsvPath { get; set; } = "";
    }
}