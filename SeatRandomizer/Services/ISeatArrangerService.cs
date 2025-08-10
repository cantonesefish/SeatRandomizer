// Services/ISeatArrangerService.cs
using SeatRandomizer.Models;
using System.Collections.Generic;
using SeatRandomizer.Services;

namespace SeatRandomizer.Services;

public interface ISeatArrangerService
{
    List<Seat> ArrangeSeats(List<Person> people, AppConfig config, bool isSameSexAdjacent = false);
}