// Services/SeatArrangerService.cs
using SeatRandomizer.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using SeatRandomizer.Services;

namespace SeatRandomizer.Services;

public class SeatArrangerService : ISeatArrangerService
{
    private readonly Random _random = new();

    public List<Seat> ArrangeSeats(List<Person> people, AppConfig config, bool isSameSexAdjacent = false)
    {
        System.Console.WriteLine($"Service: Arranging {people.Count} people (SameSexAdjacent: {isSameSexAdjacent}) in {config.Rows}x{config.Columns} grid.");
        var totalSeats = config.Rows * config.Columns;
        var seats = new List<Seat>(totalSeats);

        for (int r = 0; r < config.Rows; r++)
        {
            for (int c = 0; c < config.Columns; c++)
            {
                seats.Add(new Seat
                {
                    Row = r,
                    Column = c,
                    IsEnabled = !config.DisabledSeats.Contains((r, c))
                });
            }
        }

        var enabledSeats = seats.Where(s => s.IsEnabled).ToList();
        System.Console.WriteLine($"Service: Found {enabledSeats.Count} enabled seats.");

        if (isSameSexAdjacent && enabledSeats.Count > 1)
        {
            System.Console.WriteLine("Service: Using same-sex adjacent arrangement logic.");
            ArrangeWithSameSexAdjacent(people, enabledSeats);
        }
        else
        {
            System.Console.WriteLine("Service: Using standard random arrangement logic.");
            // 标准随机排列逻辑
            var shuffledPeople = people.OrderBy(p => _random.Next()).ToList();
            var shuffledSeats = enabledSeats.OrderBy(s => _random.Next()).ToList();

            int assignmentCount = Math.Min(shuffledPeople.Count, shuffledSeats.Count);
            for (int i = 0; i < assignmentCount; i++)
            {
                shuffledSeats[i].Occupant = shuffledPeople[i];
                System.Console.WriteLine($"  - Assigned Person '{shuffledPeople[i].Name}' ({shuffledPeople[i].Sex}) to Seat ({shuffledSeats[i].Row}, {shuffledSeats[i].Column})");
            }

            for (int i = assignmentCount; i < shuffledSeats.Count; i++)
            {
                shuffledSeats[i].Occupant = null;
            }
        }

        return seats;
    }

    private void ArrangeWithSameSexAdjacent(List<Person> people, List<Seat> enabledSeats)
    {
        // 1. 按性别分组人员
        var malePeople = people.Where(p => p.Sex.Equals("male", StringComparison.OrdinalIgnoreCase)).ToList();
        var femalePeople = people.Where(p => p.Sex.Equals("female", StringComparison.OrdinalIgnoreCase)).ToList();
        var otherPeople = people.Where(p => !p.Sex.Equals("male", StringComparison.OrdinalIgnoreCase) && !p.Sex.Equals("female", StringComparison.OrdinalIgnoreCase)).ToList();

        System.Console.WriteLine($"Service: Grouped people - Male: {malePeople.Count}, Female: {femalePeople.Count}, Other: {otherPeople.Count}");

        // 2. 随机打乱各组人员
        ShuffleList(malePeople);
        ShuffleList(femalePeople);
        ShuffleList(otherPeople);

        // 3. 创建一个座位对列表 (用于同桌)
        // 简单策略：将座位列表两两配对
        var seatPairs = new List<(Seat, Seat)>();
        for (int i = 0; i < enabledSeats.Count - 1; i += 2)
        {
            seatPairs.Add((enabledSeats[i], enabledSeats[i + 1]));
        }
        // 如果座位数是奇数，最后一个座位单独作为一个"对"
        if (enabledSeats.Count % 2 == 1)
        {
            (Seat, Seat) item = (enabledSeats[^1], null);
            seatPairs.Add(item);
        }

        // 4. 随机打乱座位对
        ShuffleList(seatPairs);

        // 5. 分配人员到座位对
        int maleIndex = 0, femaleIndex = 0, otherIndex = 0;

        foreach (var (seat1, seat2) in seatPairs)
        {
            // 决定这个座位对优先分配给哪个性别
            // 简单策略：随机选择一个有剩余人员的性别组
            List<Person>? sourceGroup = null;
            if (maleIndex < malePeople.Count && femaleIndex < femalePeople.Count)
            {
                // 两者都有剩余，随机选择
                sourceGroup = _random.NextDouble() < 0.5 ? malePeople : femalePeople;
            }
            else if (maleIndex < malePeople.Count)
            {
                sourceGroup = malePeople;
            }
            else if (femaleIndex < femalePeople.Count)
            {
                sourceGroup = femalePeople;
            }
            else if (otherIndex < otherPeople.Count)
            {
                sourceGroup = otherPeople;
            }
            else
            {
                // 所有人员都已分配
                break;
            }

            // 从选定的组中分配人员
            if (sourceGroup == malePeople && maleIndex < malePeople.Count)
            {
                AssignPersonToSeatPair(seat1, seat2, malePeople, ref maleIndex);
            }
            else if (sourceGroup == femalePeople && femaleIndex < femalePeople.Count)
            {
                AssignPersonToSeatPair(seat1, seat2, femalePeople, ref femaleIndex);
            }
            else if (sourceGroup == otherPeople && otherIndex < otherPeople.Count)
            {
                AssignPersonToSeatPair(seat1, seat2, otherPeople, ref otherIndex);
            }
        }
    }

    private static void AssignPersonToSeatPair(Seat seat1, Seat seat2, List<Person> peopleGroup, ref int index)
    {
        if (index < peopleGroup.Count)
        {
            seat1.Occupant = peopleGroup[index];
            System.Console.WriteLine($"  - Assigned Person '{peopleGroup[index].Name}' ({peopleGroup[index].Sex}) to Seat ({seat1.Row}, {seat1.Column})");
            index++;
        }
        if (index < peopleGroup.Count && seat2 != null)
        {
            seat2.Occupant = peopleGroup[index];
            System.Console.WriteLine($"  - Assigned Person '{peopleGroup[index].Name}' ({peopleGroup[index].Sex}) to Seat ({seat2.Row}, {seat2.Column})");
            index++;
        }
        else if (seat2 != null)
        {
            seat2.Occupant = null; // 座位对的第二个座位可能空着
        }
    }

    // 辅助方法：随机打乱列表
    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = _random.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}