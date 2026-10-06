using System.Diagnostics;
using System.Reflection;

namespace AfterPub.App;

/// <summary>
/// Simple About dialog. Everything shown comes from assembly metadata, which is
/// set once in Directory.Build.props, so credits never drift out of sync.
/// </summary>
internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Assembly assembly = typeof(AboutForm).Assembly;
        string version = GetVersion(assembly);
        string copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        string repoUrl = GetMetadata(assembly, "RepositoryUrl");

        Text = "About AfterPub";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        FlowLayoutPanel panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill
        };

        Label title = new Label
        {
            Text = "AfterPub",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };
        panel.Controls.Add(title);
        panel.Controls.Add(CreateLabel("Version " + version));
        panel.Controls.Add(CreateLabel("AfterPub searches a folder for Microsoft Publisher (.pub) files and shows which ones already have matching PDF, ODG and SLA files, so you can see what is still missing."));
        panel.Controls.Add(CreateLabel("It can also convert Publisher files to PDF and editable formats. PDF results are best with Publisher itself; LibreOffice and Scribus are best-effort, so check their output against the originals."));
        panel.Controls.Add(CreateLabel(copyright));
        panel.Controls.Add(CreateLabel("Licensed under the Apache License, Version 2.0."));

        // Trademark notice: the app works with Publisher files but is not a Microsoft product.
        Label disclaimer = CreateLabel(
            "AfterPub is an independent project and is not affiliated with, endorsed by, or sponsored by Microsoft. "
            + "Microsoft and Publisher are trademarks of the Microsoft group of companies.");
        disclaimer.ForeColor = SystemColors.GrayText;
        disclaimer.Margin = new Padding(0, 8, 0, 4);
        panel.Controls.Add(disclaimer);

        if (repoUrl.Length > 0)
        {
            LinkLabel link = new LinkLabel
            {
                Text = repoUrl,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 8)
            };
            // Opens the user's browser only when clicked; the app itself makes no network calls.
            link.LinkClicked += (object? sender, LinkLabelLinkClickedEventArgs e) =>
            {
                Process.Start(new ProcessStartInfo(repoUrl) { UseShellExecute = true });
            };
            panel.Controls.Add(link);
        }

        Button ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };
        panel.Controls.Add(ok);

        Controls.Add(panel);
        AcceptButton = ok;
        CancelButton = ok;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            Margin = new Padding(0, 0, 0, 4)
        };
    }

    private static string GetVersion(Assembly assembly)
    {
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        // The SDK appends "+<commit hash>" to the informational version; hide it.
        int plus = version.IndexOf('+');
        return plus >= 0 ? version.Substring(0, plus) : version;
    }

    private static string GetMetadata(Assembly assembly, string key)
    {
        foreach (AssemblyMetadataAttribute attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (attribute.Key == key)
            {
                return attribute.Value ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
