using RSDWSaveConverter.Core;
using RSDWSaveConverter.Core.Wgs;

namespace RSDWSaveConverter.App;

public sealed class MainForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(16, 17, 19);
    private static readonly Color Surface = Color.FromArgb(25, 26, 29);
    private static readonly Color Border = Color.FromArgb(72, 68, 57);
    private static readonly Color Gold = Color.FromArgb(229, 201, 92);
    private static readonly Color PrimaryText = Color.FromArgb(240, 238, 232);
    private static readonly Color SecondaryText = Color.FromArgb(172, 170, 163);

    private readonly Label _fileName = CreateLabel("No save selected", 14, FontStyle.Bold);
    private readonly Label _fileDetails = CreateLabel("", 9, FontStyle.Regular, SecondaryText);
    private readonly Label _profileStatus = CreateLabel("Searching for Game Pass saves...", 9, FontStyle.Regular, SecondaryText);
    private readonly Label _destinationLabel = CreateLabel("Choose a save to list compatible slots", 9, FontStyle.Regular);
    private readonly Label _status = CreateLabel("Ready", 9, FontStyle.Regular, SecondaryText);
    private readonly ComboBox _profilePicker = CreateComboBox();
    private readonly ComboBox _destinationPicker = CreateComboBox();
    private readonly Button _chooseSave = CreateButton("Choose Save File");
    private readonly Button _exportPayload = CreateButton("Export .xav");
    private readonly Button _chooseProfile = CreateButton("Choose Profile");
    private readonly Button _refreshProfiles = CreateButton("Refresh");
    private readonly Button _import = CreateButton("Import Save", emphasized: true);
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false };

    private LoadedSave? _loadedSave;

    public MainForm()
    {
        Text = "RSDW Save Converter";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        BackColor = Canvas;
        ForeColor = PrimaryText;
        Font = new Font("Segoe UI", 10F);
        MinimumSize = new Size(760, 580);
        ClientSize = new Size(820, 630);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;

        Controls.Add(BuildLayout());
        WireEvents();
        UpdateActions();
        Shown += async (_, _) => await RefreshProfilesAsync();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 24, 28, 20),
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        titlePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        titlePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var title = CreateLabel("RSDW SAVE CONVERTER", 18, FontStyle.Bold, PrimaryText, DockStyle.Fill);
        title.Margin = Padding.Empty;
        title.TextAlign = ContentAlignment.MiddleLeft;
        var subtitle = CreateLabel("Steam to Xbox Game Pass", 10, FontStyle.Regular, Gold, DockStyle.Fill);
        subtitle.Margin = Padding.Empty;
        subtitle.TextAlign = ContentAlignment.TopLeft;
        titlePanel.Controls.Add(title, 0, 0);
        titlePanel.Controls.Add(subtitle, 0, 1);
        root.Controls.Add(titlePanel, 0, 0);

        var dropPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(22),
            Margin = new Padding(0, 0, 0, 14),
            BorderStyle = BorderStyle.FixedSingle,
            AllowDrop = true
        };
        var dropLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        dropLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        dropLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        dropLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        dropLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        _fileName.Dock = DockStyle.Fill;
        _fileName.TextAlign = ContentAlignment.BottomLeft;
        _fileDetails.Dock = DockStyle.Fill;
        _fileDetails.TextAlign = ContentAlignment.TopLeft;
        _chooseSave.Dock = DockStyle.Fill;
        _chooseSave.Margin = new Padding(12, 8, 0, 8);
        _exportPayload.Dock = DockStyle.Fill;
        _exportPayload.Margin = new Padding(12, 2, 0, 2);
        dropLayout.Controls.Add(_fileName, 0, 0);
        dropLayout.Controls.Add(_fileDetails, 0, 1);
        dropLayout.Controls.Add(_chooseSave, 1, 0);
        dropLayout.Controls.Add(_exportPayload, 1, 1);
        dropPanel.Controls.Add(dropLayout);
        EnableDropTarget(dropPanel);
        root.Controls.Add(dropPanel, 0, 1);

        root.Controls.Add(CreateLabel("GAME PASS DESTINATION", 9, FontStyle.Bold, Gold), 0, 2);

        var destination = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(0, 8, 0, 12)
        };
        destination.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        destination.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        destination.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        destination.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        destination.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        destination.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var profileLabelRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        profileLabelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        profileLabelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        profileLabelRow.Controls.Add(CreateLabel("Game Pass profile", 9, FontStyle.Regular), 0, 0);
        _profileStatus.Dock = DockStyle.Fill;
        _profileStatus.TextAlign = ContentAlignment.MiddleRight;
        profileLabelRow.Controls.Add(_profileStatus, 1, 0);
        destination.Controls.Add(profileLabelRow, 0, 0);

        var profileRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        profileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        profileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        profileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        _profilePicker.Dock = DockStyle.Fill;
        _chooseProfile.Dock = DockStyle.Fill;
        _chooseProfile.Margin = new Padding(8, 0, 0, 0);
        _refreshProfiles.Dock = DockStyle.Fill;
        _refreshProfiles.Margin = new Padding(8, 0, 0, 0);
        profileRow.Controls.Add(_profilePicker, 0, 0);
        profileRow.Controls.Add(_chooseProfile, 1, 0);
        profileRow.Controls.Add(_refreshProfiles, 2, 0);
        destination.Controls.Add(profileRow, 0, 1);

        destination.Controls.Add(_destinationLabel, 0, 3);
        _destinationPicker.Dock = DockStyle.Fill;
        destination.Controls.Add(_destinationPicker, 0, 4);
        root.Controls.Add(destination, 0, 3);

        _import.Dock = DockStyle.Fill;
        _import.Margin = new Padding(0, 4, 0, 8);
        root.Controls.Add(_import, 0, 4);

        var statusRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        statusRow.Controls.Add(_status, 0, 0);
        statusRow.Controls.Add(_progress, 1, 0);
        root.Controls.Add(statusRow, 0, 5);
        return root;
    }

    private void WireEvents()
    {
        _chooseSave.Click += async (_, _) => await ChooseSaveAsync();
        _exportPayload.Click += async (_, _) => await ExportPayloadAsync();
        _refreshProfiles.Click += async (_, _) => await RefreshProfilesAsync();
        _chooseProfile.Click += (_, _) => ChooseProfile();
        _profilePicker.SelectedIndexChanged += (_, _) => PopulateDestinations();
        _destinationPicker.SelectedIndexChanged += (_, _) => UpdateActions();
        _import.Click += async (_, _) => await ImportAsync();
        DragEnter += HandleDragEnter;
        DragDrop += async (_, args) => await HandleDropAsync(args);
    }

    private void EnableDropTarget(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += HandleDragEnter;
        control.DragDrop += async (_, args) => await HandleDropAsync(args);
        foreach (Control child in control.Controls)
        {
            EnableDropTarget(child);
        }
    }

    private async Task ChooseSaveAsync()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Choose a Dragonwilds save",
            Filter = "Dragonwilds saves (*.sav;*.xav;*.json)|*.sav;*.xav;*.json|World saves (*.sav;*.xav)|*.sav;*.xav|Character saves (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (picker.ShowDialog(this) == DialogResult.OK)
        {
            await LoadSaveAsync(picker.FileName);
        }
    }

    private async Task LoadSaveAsync(string path)
    {
        _loadedSave = null;
        _fileName.Text = "No save selected";
        _fileDetails.Text = "";
        PopulateDestinations();

        await RunBusyAsync("Reading save...", async () =>
        {
            var fallback = Path.GetFileNameWithoutExtension(path);
            if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                var data = await File.ReadAllBytesAsync(path);
                var metadata = DragonwildsCharacterCodec.ReadMetadata(data, fallback);
                _loadedSave = new LoadedSave(
                    SaveKind.Character,
                    metadata.CharacterName,
                    Path.GetFileName(path),
                    data);
            }
            else
            {
                var raw = await Task.Run(() => DragonwildsSaveCodec.ReadRawSave(path));
                var metadata = DragonwildsSaveCodec.ReadMetadata(raw, fallback);
                _loadedSave = new LoadedSave(
                    SaveKind.World,
                    metadata.WorldName,
                    Path.GetFileName(path),
                    raw);
            }

            _fileName.Text = _loadedSave.DisplayName;
            _fileDetails.Text = $"{_loadedSave.Kind} save  |  {FormatByteSize(_loadedSave.Data.Length)}  |  {_loadedSave.FileName}";
            _status.Text = $"{_loadedSave.Kind} save ready";
        });
        PopulateDestinations();
        UpdateActions();
    }

    private async Task ExportPayloadAsync()
    {
        if (_loadedSave is not { Kind: SaveKind.World } loadedSave)
        {
            return;
        }

        using var picker = new SaveFileDialog
        {
            Title = "Export Game Pass payload",
            Filter = "Dragonwilds Game Pass payload (*.xav)|*.xav",
            FileName = DragonwildsSaveCodec.MakeSafeSlotName(loadedSave.DisplayName) + ".xav",
            OverwritePrompt = true
        };

        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await RunBusyAsync("Converting save...", async () =>
        {
            var wrapped = await Task.Run(() => DragonwildsSaveCodec.Wrap(loadedSave.Data));
            await File.WriteAllBytesAsync(picker.FileName, wrapped);
            _status.Text = $"Exported {Path.GetFileName(picker.FileName)}";
        });
    }

    private async Task RefreshProfilesAsync()
    {
        await RunBusyAsync("Finding Game Pass profiles...", async () =>
        {
            var profiles = await Task.Run(WgsProfile.Discover);
            _profilePicker.DataSource = profiles.Select(profile => new ProfileChoice(profile)).ToList();
            _profileStatus.Text = profiles.Count == 0
                ? "Not detected"
                : profiles.Count == 1 ? "Detected" : $"{profiles.Count} detected";
            _status.Text = profiles.Count == 0
                ? "Choose the WGS profile folder manually"
                : "Game Pass profile ready";
        });
        PopulateDestinations();
    }

    private void ChooseProfile()
    {
        using var picker = new FolderBrowserDialog
        {
            Description = "Choose the WGS profile folder containing containers.index",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var profile = WgsProfile.Load(picker.SelectedPath);
            _profilePicker.DataSource = new List<ProfileChoice> { new(profile) };
            _profileStatus.Text = "Selected";
            _status.Text = "Game Pass profile ready";
            PopulateDestinations();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void PopulateDestinations()
    {
        var profile = (_profilePicker.SelectedItem as ProfileChoice)?.Profile;
        var choices = (_loadedSave?.Kind, profile) switch
        {
            (SaveKind.World, not null) => profile.WorldSlots
                .Select(DestinationChoice.FromWorld)
                .ToList(),
            (SaveKind.Character, not null) => profile.CharacterSlots
                .Select(DestinationChoice.FromCharacter)
                .ToList(),
            _ => []
        };

        _destinationPicker.DataSource = null;
        _destinationPicker.DataSource = choices;
        _destinationLabel.Text = _loadedSave?.Kind switch
        {
            SaveKind.World => "Replace an existing world",
            SaveKind.Character => "Replace an existing character",
            _ => "Choose a save to list compatible slots"
        };
        _import.Text = _loadedSave?.Kind switch
        {
            SaveKind.World => "Import World",
            SaveKind.Character => "Import Character",
            _ => "Import Save"
        };

        if (_loadedSave is not null)
        {
            var matchingIndex = choices.FindIndex(choice =>
                choice.DisplayName.Equals(_loadedSave.DisplayName, StringComparison.OrdinalIgnoreCase)
                || choice.SlotName.Equals(_loadedSave.DisplayName, StringComparison.OrdinalIgnoreCase));
            if (matchingIndex >= 0)
            {
                _destinationPicker.SelectedIndex = matchingIndex;
            }
        }

        UpdateActions();
    }

    private async Task ImportAsync()
    {
        if (_loadedSave is not { } loadedSave
            || _profilePicker.SelectedItem is not ProfileChoice profileChoice
            || _destinationPicker.SelectedItem is not DestinationChoice destination
            || destination.Kind != loadedSave.Kind)
        {
            return;
        }

        var saveType = loadedSave.Kind.ToString().ToLowerInvariant();
        var confirmation = MessageBox.Show(
            this,
            $"Replace '{destination.DisplayName}' with '{loadedSave.DisplayName}'?\n\n"
            + "A complete WGS backup will be created first. The existing in-game backup slot will remain unchanged.",
            $"Import Game Pass {saveType}",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.OK)
        {
            return;
        }

        await RunBusyAsync($"Importing {saveType}...", async () =>
        {
            var result = await Task.Run(() => loadedSave.Kind switch
            {
                SaveKind.World => new WgsImporter().ReplaceWorld(
                    profileChoice.Profile.Path,
                    destination.FileName,
                    loadedSave.Data),
                SaveKind.Character => new WgsImporter().ReplaceCharacter(
                    profileChoice.Profile.Path,
                    destination.FileName,
                    loadedSave.Data),
                _ => throw new InvalidOperationException("Unsupported save type.")
            });
            _status.Text = $"Imported {result.SaveName}";

            MessageBox.Show(
                this,
                $"{result.SaveName} is ready in Game Pass.\n\nBackup:\n{result.BackupPath}\n\n"
                + "Launch Dragonwilds and choose the local save if Xbox asks which version to use.",
                "Import complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });

        await RefreshProfilesAsync();
    }

    private async Task RunBusyAsync(string status, Func<Task> action)
    {
        SetBusy(true, status);
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            _status.Text = "Operation failed";
            ShowError(exception);
        }
        finally
        {
            SetBusy(false, _status.Text);
        }
    }

    private void SetBusy(bool busy, string status)
    {
        _status.Text = status;
        _progress.Visible = busy;
        UseWaitCursor = busy;
        _chooseSave.Enabled = !busy;
        _chooseProfile.Enabled = !busy;
        _refreshProfiles.Enabled = !busy;
        _profilePicker.Enabled = !busy;
        _destinationPicker.Enabled = !busy;
        if (busy)
        {
            _exportPayload.Enabled = false;
            _import.Enabled = false;
        }
        else
        {
            UpdateActions();
        }
    }

    private void UpdateActions()
    {
        _exportPayload.Enabled = _loadedSave is { Kind: SaveKind.World };
        _import.Enabled = _loadedSave is not null
            && _profilePicker.SelectedItem is ProfileChoice
            && _destinationPicker.SelectedItem is DestinationChoice destination
            && destination.Kind == _loadedSave.Kind;
    }

    private void HandleDragEnter(object? sender, DragEventArgs args)
    {
        args.Effect = GetDroppedFile(args) is null ? DragDropEffects.None : DragDropEffects.Copy;
    }

    private async Task HandleDropAsync(DragEventArgs args)
    {
        var path = GetDroppedFile(args);
        if (path is not null)
        {
            await LoadSaveAsync(path);
        }
    }

    private static string? GetDroppedFile(DragEventArgs args)
    {
        if (args.Data?.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths)
        {
            return null;
        }

        var path = paths[0];
        return File.Exists(path) && new[] { ".sav", ".xav", ".json" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
            ? path
            : null;
    }

    private static string FormatByteSize(int length) => length >= 1024 * 1024
        ? $"{length / 1024d / 1024d:N1} MB"
        : $"{length / 1024d:N1} KB";

    private void ShowError(Exception exception) => MessageBox.Show(
        this,
        exception.Message,
        "RSDW Save Converter",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);

    private static Label CreateLabel(
        string text,
        float size,
        FontStyle style,
        Color? color = null,
        DockStyle dock = DockStyle.None) => new()
        {
            Text = text,
            Font = new Font("Segoe UI", size, style),
            ForeColor = color ?? PrimaryText,
            AutoSize = dock == DockStyle.None,
            Dock = dock,
            BackColor = Color.Transparent
        };

    private static ComboBox CreateComboBox() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        BackColor = Surface,
        ForeColor = PrimaryText,
        FlatStyle = FlatStyle.Flat,
        IntegralHeight = false,
        DropDownHeight = 240
    };

    private static Button CreateButton(string text, bool emphasized = false) => new()
    {
        Text = text,
        FlatStyle = FlatStyle.Flat,
        BackColor = emphasized ? Gold : Surface,
        ForeColor = emphasized ? Color.FromArgb(25, 23, 16) : PrimaryText,
        Font = new Font("Segoe UI", 9F, emphasized ? FontStyle.Bold : FontStyle.Regular),
        Cursor = Cursors.Hand,
        UseVisualStyleBackColor = false,
        FlatAppearance = { BorderColor = emphasized ? Gold : Border, BorderSize = 1 }
    };

    private sealed record ProfileChoice(WgsProfile Profile)
    {
        public override string ToString() =>
            $"{Profile.WorldSlots.Count} world(s), {Profile.CharacterSlots.Count} character(s)  |  {new DirectoryInfo(Profile.Path).Name}";
    }

    private enum SaveKind
    {
        World,
        Character
    }

    private sealed record LoadedSave(
        SaveKind Kind,
        string DisplayName,
        string FileName,
        byte[] Data);

    private sealed record DestinationChoice(
        SaveKind Kind,
        string SlotName,
        string DisplayName,
        string FileName)
    {
        public static DestinationChoice FromWorld(WgsWorldSlot slot) => new(
            SaveKind.World,
            slot.SlotName,
            slot.DisplayName,
            slot.ActiveEntry.FileName);

        public static DestinationChoice FromCharacter(WgsCharacterSlot slot) => new(
            SaveKind.Character,
            slot.SlotName,
            slot.DisplayName,
            slot.ActiveEntry.FileName);

        public override string ToString() => DisplayName.Equals(SlotName, StringComparison.OrdinalIgnoreCase)
            ? DisplayName
            : $"{DisplayName} ({SlotName})";
    }
}
