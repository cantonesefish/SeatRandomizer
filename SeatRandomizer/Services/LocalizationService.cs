// Services/LocalizationService.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SeatRandomizer.Services;

public class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, string> _currentStrings = [];
    private string _currentCulture = "zh"; // 默认中文

    public string this[string key]
    {
        get
        {
            if (Application.Current != null)
            {
                if (Application.Current.TryFindResource(key, out var resourceValue))
                {
                    // 确保返回的是字符串
                    if (resourceValue is string stringValue)
                    {
                        return stringValue;
                    }
                    else if (resourceValue != null)
                    {
                        // 如果资源存在但不是字符串，转换为字符串
                        return resourceValue.ToString() ?? key;
                    }
                }
                else
                {
                    System.Console.WriteLine($"LocalizationService: Resource key '{key}' not found.");
                }
            }
            else
            {
                System.Console.WriteLine("LocalizationService: Application.Current is null.");
            }
            // 如果找不到资源，则返回键本身作为 fallback
            return key;
        }
    }

    public void SetCulture(string cultureName)
    {
        _currentCulture = cultureName;
        var app = Application.Current;
        if (app == null) return;

        // 清除旧的资源字典
        var stringResources = app.Resources.MergedDictionaries.OfType<ResourceInclude>().ToList();
        foreach (var resource in stringResources)
        {
            app.Resources.MergedDictionaries.Remove(resource);
        }

        // 加载新的资源字典
        var resourceUri = new Uri($"avares://SeatRandomizer/Assets/Strings.{cultureName}.axaml");
        var resourceInclude = new ResourceInclude(new Uri("resm:Styles?assembly=SeatRandomizer"))
        {
            Source = resourceUri
        };
        app.Resources.MergedDictionaries.Add(resourceInclude);

        // 更新内部字符串字典（简化处理，实际应用中可能需要更复杂的绑定刷新）
        LoadStringsIntoDictionary(resourceUri);
    }

    private void LoadStringsIntoDictionary(Uri resourceUri)
    {
        ArgumentNullException.ThrowIfNull(resourceUri);
        _currentStrings.Clear();
        if (_currentCulture == "en")
        {
            _currentStrings["AppName"] = "Seat Randomizer";
            _currentStrings["LoadData"] = "Load Data";
            _currentStrings["Rearrange"] = "Rearrange";
        }
        else // 默认中文
        {
            _currentStrings["AppName"] = "座位宝";
            _currentStrings["LoadData"] = "加载数据";
            _currentStrings["Rearrange"] = "重新排列";
        }
    }
}