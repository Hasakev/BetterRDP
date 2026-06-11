using BetterRdp.Core;
using Microsoft.UI.Xaml.Controls;

namespace BetterRdp_App.Dialogs;

/// <summary>Collects a Server (display name + address).</summary>
public sealed partial class ServerDialog : ContentDialog
{
    public ServerDialog()
    {
        InitializeComponent();
    }

    /// <summary>The entered Server, or null if either field is blank.</summary>
    public Server? Result
    {
        get
        {
            var name = NameInput.Text.Trim();
            var address = AddressInput.Text.Trim();
            if (name.Length == 0 || address.Length == 0)
                return null;
            return new Server { Name = name, Address = address };
        }
    }
}
