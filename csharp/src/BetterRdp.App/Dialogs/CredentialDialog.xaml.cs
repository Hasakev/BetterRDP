using BetterRdp.Core;
using Microsoft.UI.Xaml.Controls;

namespace BetterRdp_App.Dialogs;

/// <summary>Collects a Credential (username, optional domain, password).</summary>
public sealed partial class CredentialDialog : ContentDialog
{
    private bool _isEdit;

    public CredentialDialog()
    {
        InitializeComponent();
    }

    /// <summary>Pre-fill username/domain and switch to "edit" wording. The password is left
    /// blank: a blank password on save means "keep the existing one" (we never decrypt a
    /// stored password back into the form).</summary>
    public void LoadForEdit(Credential credential)
    {
        _isEdit = true;
        Title = "Edit credential";
        PrimaryButtonText = "Save";
        UsernameInput.Text = credential.Username;
        DomainInput.Text = credential.Domain ?? "";
        PasswordInput.PlaceholderText = "leave blank to keep current";
    }

    /// <summary>The entered Credential, or null if required fields are blank. When adding, a
    /// password is required; when editing, a blank password yields <c>Password == null</c>
    /// (keep the stored secret). Id mirrors the Python build: <c>domain\username</c> when a
    /// domain is given, else the username.</summary>
    public Credential? Result
    {
        get
        {
            var username = UsernameInput.Text.Trim();
            var domainText = DomainInput.Text.Trim();
            var domain = domainText.Length == 0 ? null : domainText;
            var password = PasswordInput.Password;
            if (username.Length == 0)
                return null;
            if (!_isEdit && password.Length == 0)
                return null; // adding requires a password
            var id = domain is null ? username : $"{domain}\\{username}";
            string? pw = password.Length == 0 ? null : password; // null => keep existing (edit)
            return new Credential(id, username, domain, pw);
        }
    }
}
