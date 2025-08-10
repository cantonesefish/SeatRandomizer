// Services/ILocalizationService.cs
namespace SeatRandomizer.Services;

public interface ILocalizationService
{
    string this[string key] { get; }
    void SetCulture(string cultureName);
}