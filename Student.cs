using CsvHelper.Configuration.Attributes;

namespace SeatRandomizer
{
    public class Student
    {
        [Name("学号")]
        public string Id { get; set; }

        [Name("姓名")]
        public string Name { get; set; }

        [Name("性别")]
        public string Gender { get; set; }
    }
}