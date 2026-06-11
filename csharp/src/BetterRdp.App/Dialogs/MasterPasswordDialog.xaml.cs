using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BetterRdp_App.Dialogs;

/// <summary>Prompts for the Master Password. The unlock loop (create-vs-open, retry on a
/// wrong password) lives in MainPage; this dialog just collects one attempt.</summary>
public sealed partial class MasterPasswordDialog : ContentDialog
{
    public MasterPasswordDialog()
    {
        InitializeComponent();
    }

    public string Password => PasswordInput.Password;

    /// <summary>Switch to the first-run wording ("create a password").</summary>
    public void SetFirstRun()
        => Instruction.Text = "Create a Master Password for your new vault:";

    /// <summary>Show a retry message after a failed unlock and clear the field.</summary>
    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        Instruction.Text = "Enter your Master Password:";
        PasswordInput.Password = "";
    }
}
