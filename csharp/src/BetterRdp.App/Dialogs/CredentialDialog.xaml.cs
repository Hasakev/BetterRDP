using BetterRdp.Core;
using Microsoft.UI.Xaml.Controls;

namespace BetterRdp_App.Dialogs;

/// <summary>Collects a Credential (username, optional domain, password).</summary>
public sealed partial class CredentialDialog : ContentDialog
{
    public CredentialDialog()
    {
        InitializeComponent();
    }

    /// <summary>The entered Credential, or null if username or password is blank.
    /// Id mirrors the Python build: <c>domain\username</c> when a domain is given, else the username.</summary>
    public Credential? Result
    {
        get
        {
            var username = UsernameInput.Text.Trim();
            var domainText = DomainInput.Text.Trim();
            var domain = domainText.Length == 0 ? null : domainText;
            var password = PasswordInput.Password;
            if (username.Length == 0 || password.Length == 0)
                return null;
            var id = domain is null ? username : $"{domain}\\{username}";
            return new Credential(id, username, domain, password);
        }
    }
}
