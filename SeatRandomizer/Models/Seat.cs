// Models/Seat.cs
using ReactiveUI;

namespace SeatRandomizer.Models;

public class Seat : ReactiveObject
{
    private Person? _occupant;
    private bool _isEnabled = true;

    public int Row { get; set; }
    public int Column { get; set; }

    public Person? Occupant
    {
        get => _occupant;
        set => this.RaiseAndSetIfChanged(ref _occupant, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }
}