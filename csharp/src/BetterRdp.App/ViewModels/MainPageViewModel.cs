using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BetterRdp_App.ViewModels;

/// <summary>
/// Placeholder ViewModel from the template, converted to the classic field-based
/// <see cref="ObservableProperty"/> pattern (no partial-property source generation).
/// This gets replaced by the real Better RDP view models when the WinUI shell is built.
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string greeting = "Hello, WinUI!";

    [ObservableProperty]
    private int counter;

    [RelayCommand]
    private void Increment() => Counter++;

    [RelayCommand]
    private void Decrement() => Counter--;
}
