// ViewModels/SeatViewModel.cs
using Avalonia.Media;
using ReactiveUI;
using SeatRandomizer.Models;
using SeatRandomizer.Services;
using System;

namespace SeatRandomizer.ViewModels;

public class SeatViewModel(Seat seat, ILocalizationService localizationService, bool isAisle = false) : ViewModelBase
{
    private readonly ILocalizationService _localizationService = localizationService;
    private Person? _occupant = seat.Occupant;
    private bool _isEnabled = seat.IsEnabled;
    private bool _isAisle = isAisle;

    public int Row { get; } = seat.Row;
    public int Column { get; } = seat.Column;
    public int LogicalRow { get; } = seat.Row;
    public int LogicalColumn { get; } = seat.Column;
    public int GridRow { get; set; } = -1;
    public int GridColumn { get; set; } = -1;
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

    public bool IsAisle
    {
        get => _isAisle;
        set => this.RaiseAndSetIfChanged(ref _isAisle, value);
    }

    public string DisplayName => IsAisle ? "" : (Occupant?.Name ?? "");
    public string DisplayNumber => IsAisle ? "" : (Occupant?.Number.ToString() ?? "");
    public IBrush BorderBrush => GetBorderBrush();
    public IBrush BackgroundBrush => GetBackgroundBrush();
    public bool IsTextVisible => !IsAisle && IsEnabled;

    private IBrush GetBorderBrush()
    {
        if (IsAisle) return Brushes.Transparent;
        if (!IsEnabled) return Brushes.Transparent;
        if (Occupant == null) return Brushes.Transparent;
        return Occupant.Sex.ToLower() switch
        {
            "male" => Brushes.LightBlue,
            "female" => Brushes.LightPink,
            _ => Brushes.Transparent
        };
    }

    private IBrush GetBackgroundBrush()
    {
        if (IsAisle) return Brushes.White;
        if (!IsEnabled) return Brushes.White;
        return Brushes.WhiteSmoke;
    }

    public SeatViewModel(Seat seat, ILocalizationService localizationService, bool isAisle, int logicalRow, int logicalCol) : this(seat, localizationService, isAisle)
    {
        LogicalRow = logicalRow;
        LogicalColumn = logicalCol;
    }
}