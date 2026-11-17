using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcessorTycoon.Setup;

internal static class Program
{
    internal const string CurrentVersion = UpdateChecker.CurrentVersion;
    [DllImport("kernel32.dll")] static extern bool AttachConsole(uint processId);

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            AttachConsole(uint.MaxValue);
            int code;
            string message;
            try
            {
                if ((args.Length != 2 && args.Length != 4) || !new[] { "--install", "--uninstall", "--verify" }.Contains(args[0]) || (args.Length == 4 && args[2] != "--result"))
                    throw new SetupException("Usage: --install|--uninstall|--verify <game-folder> [--result <result.json>]", 2);
                message = new InstallerEngine(args[1]).Run(args[0]);
                code = 0;
            }
            catch (Exception ex) { message = ex.Message; code = ex is SetupException se ? se.ExitCode : 1; }
            try { Console.WriteLine(message); } catch { }
            if (args.Length == 4 && args[2] == "--result")
            {
                try { File.WriteAllText(args[3], JsonSerializer.Serialize(new { exitCode = code, message })); }
                catch { return 1; }
            }
            return code;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
        return 0;
    }

    internal static Task<LatestRelease?> CheckLatestReleaseAsync(CancellationToken cancellationToken = default) =>
        UpdateChecker.CheckLatestReleaseAsync(cancellationToken);
}

internal sealed class InstallerForm : Form
{
    readonly TextBox folder = new() { Dock = DockStyle.Fill };
    readonly Label status = new() { AutoSize = true, MaximumSize = new Size(600, 0), Text = "Перед установкой закройте игру. Сохранения и сторонние моды не удаляются." };
    readonly Button install = new() { Text = "Установить", AutoSize = true };
    readonly Button uninstall = new() { Text = "Удалить русификатор", AutoSize = true };
    readonly Button browse = new() { Text = "Обзор…", AutoSize = true };

    public InstallerForm()
    {
        Text = "Processor Tycoon — русификатор";
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(660, 310);
        MinimumSize = new Size(630, 350);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(247, 248, 250);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Processor Tycoon · Русская версия", AutoSize = true, Font = new Font("Segoe UI", 17, FontStyle.Bold) });
        layout.Controls.Add(new Label { Text = "Папка игры (с файлом Processor Tycoon.exe)", AutoSize = true });
        var picker = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Processor Tycoon");
        picker.Controls.Add(folder, 0, 0);
        picker.Controls.Add(browse, 1, 0);
        layout.Controls.Add(picker);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        actions.Controls.Add(install);
        actions.Controls.Add(uninstall);
        layout.Controls.Add(actions);
        layout.Controls.Add(status);
        Controls.Add(layout);
        Shown += async (_, _) => await CheckForUpdatesAsync();
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Выберите папку Processor Tycoon", InitialDirectory = folder.Text, UseDescriptionForTitle = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath;
        };
        install.Click += async (_, _) => await RunOperation("--install");
        uninstall.Click += async (_, _) => await RunOperation("--uninstall");
    }

    async Task CheckForUpdatesAsync()
    {
        try
        {
            var latest = await Program.CheckLatestReleaseAsync();
            if (IsDisposed || Disposing || latest == null || !UpdateChecker.IsNewer(latest.TagName)) return;
            var result = MessageBox.Show(this,
                $"Доступна новая версия русификатора: {latest.TagName}.\n\nУстановлена версия: {Program.CurrentVersion}.\n\nОткрыть страницу новой версии?",
                "Доступно обновление", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result == DialogResult.Yes && !IsDisposed)
                Process.Start(new ProcessStartInfo(UpdateChecker.ReleasePage) { UseShellExecute = true });
        }
        catch (Exception) { }
    }

    async Task RunOperation(string operation)
    {
        if (operation == "--uninstall" && MessageBox.Show(this, "Удалить русификатор и восстановить исходные файлы?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        install.Enabled = uninstall.Enabled = browse.Enabled = folder.Enabled = false;
        ControlBox = false;
        status.Text = "Проверка файлов…";
        var directory = folder.Text;
        try { status.Text = await Task.Run(() => new InstallerEngine(directory).Run(operation)); }
        catch (Exception ex) { status.Text = "Операция остановлена: " + ex.Message; MessageBox.Show(this, status.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { install.Enabled = uninstall.Enabled = browse.Enabled = folder.Enabled = true; ControlBox = true; }
    }
}
