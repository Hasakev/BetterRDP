using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BetterRdp_App.Dialogs;

public sealed partial class ChangeMasterPasswordDialog : ContentDialog
{
    public ChangeMasterPasswordDialog() => InitializeComponent();

    public string CurrentPassword => CurrentPasswordInput.Password;
    public string NewPassword => NewPasswordInput.Password;

    public bool HasValidInput(out string error)
    {
        if (CurrentPassword.Length == 0 || NewPassword.Length == 0)
        {
            error = "Enter your current and new Master Passwords.";
            return false;
        }
        if (NewPassword != ConfirmPasswordInput.Password)
        {
            error = "The new password confirmation does not match.";
            return false;
        }
        error = "";
        return true;
    }

    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
