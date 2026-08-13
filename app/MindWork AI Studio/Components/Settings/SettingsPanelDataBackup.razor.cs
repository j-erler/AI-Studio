using AIStudio.Tools.ChatArchive;
using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelDataBackup : SettingsPanelBase
{
    /// <summary>
    /// The shortest time between two progress renders. Without this, a large archive would
    /// re-render the whole page once per chat.
    /// </summary>
    private static readonly TimeSpan PROGRESS_RENDER_INTERVAL = TimeSpan.FromMilliseconds(100);

    [Inject]
    private ILogger<SettingsPanelDataBackup> Logger { get; init; } = null!;

    private readonly List<ChatArchiveWorkspaceSelection> workspaceSelections = [];

    private ChatArchiveCollisionBehavior collisionBehavior = ChatArchiveCollisionBehavior.SKIP;
    private CancellationTokenSource? cancellationTokenSource;
    private ChatArchiveProgress progress;
    private ChatArchiveExportResult? exportResult;
    private ChatArchiveImportPreview? importPreview;
    private ChatArchiveImportResult? importResult;
    private DateTimeOffset lastProgressRender;
    private string workspaceLoadError = string.Empty;
    private string selectedArchivePath = string.Empty;
    private int temporaryChatCount;
    private bool includeTemporaryChats;

    /// <summary>
    /// Whether the attachment files travel with the chats. Enabled by default, so an
    /// archive is complete on another computer without the user having to think about it.
    /// </summary>
    private bool includeAttachments = true;
    private bool isLoadingWorkspaces;
    private bool wereWorkspacesLoaded;

    /// <summary>
    /// Blocks any further action, starting with the click that opens a file dialog. Without
    /// this, a second click during the open dialog would start a second run.
    /// </summary>
    private bool isBusy;

    /// <summary>
    /// Indicates that an export or import is running, which shows the progress.
    /// </summary>
    private bool isProcessing;

    private bool HasWorkspacesToExport => this.workspaceSelections.Count > 0 || this.temporaryChatCount > 0;

    private bool IsAnythingSelected => this.workspaceSelections.Any(selection => selection.IsSelected) || (this.includeTemporaryChats && this.temporaryChatCount > 0);

    private int SelectedChatCount => this.workspaceSelections.Where(selection => selection.IsSelected).Sum(selection => selection.ChatCount) + (this.includeTemporaryChats ? this.temporaryChatCount : 0);

    private bool CanImport => this.importPreview is { Success: true, Manifest.TotalChatCount: > 0 };

    private double ProgressPercentage => this.progress.TotalChats is 0 ? 0d : this.progress.ProcessedChats * 100d / this.progress.TotalChats;

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        this.cancellationTokenSource?.Cancel();
        this.DisposeCancellation();
    }

    #endregion

    /// <summary>
    /// Reads the workspaces when the user opens this panel for the first time. Doing it
    /// earlier would scan the chat storage every time the settings are opened.
    /// </summary>
    private async Task PanelExpandedChanged(bool isExpanded)
    {
        if (!isExpanded || this.wereWorkspacesLoaded)
            return;

        // Only a successful run counts as loaded. Otherwise, a single failure would leave
        // the panel empty for the rest of the session:
        this.wereWorkspacesLoaded = await this.LoadWorkspacesAsync();
    }

    /// <summary>
    /// Reads all workspaces with their chat counts, keeping the current selection.
    /// </summary>
    /// <returns>Whether the workspaces could be read.</returns>
    private async Task<bool> LoadWorkspacesAsync()
    {
        this.isLoadingWorkspaces = true;
        this.workspaceLoadError = string.Empty;
        try
        {
            var previousSelection = this.workspaceSelections
                .Where(selection => selection.IsSelected)
                .Select(selection => selection.WorkspaceId)
                .ToHashSet();

            this.workspaceSelections.Clear();

            var tree = await Tools.WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
            foreach (var workspace in tree.Workspaces)
            {
                var chats = await Tools.WorkspaceBehaviour.GetWorkspaceChatsAsync(workspace.WorkspaceId);
                this.workspaceSelections.Add(new()
                {
                    WorkspaceId = workspace.WorkspaceId,
                    Name = workspace.Name,
                    ChatCount = chats.Count,
                    IsSelected = previousSelection.Contains(workspace.WorkspaceId),
                });
            }

            this.workspaceSelections.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            this.temporaryChatCount = tree.TemporaryChats.Count;
            return true;
        }
        catch (Exception exception)
        {
            // Reading the chat storage can fail, e.g. when the data directory sits on a
            // network drive. Report it instead of showing an empty panel:
            this.Logger.LogError(exception, "Failed to read the workspaces for the data backup.");
            this.workspaceLoadError = string.Format(T("Your chats could not be read: {0}"), exception.Message);
            this.temporaryChatCount = 0;
            return false;
        }
        finally
        {
            this.isLoadingWorkspaces = false;
        }
    }

    /// <summary>
    /// Refreshes the workspaces when returning to the export, because an import in the
    /// meantime might have changed them.
    /// </summary>
    private async Task ActivePanelChanged(int panelIndex)
    {
        if (this.isBusy || panelIndex is not 0)
            return;

        await this.LoadWorkspacesAsync();
    }

    private void WorkspaceSelectionChanged(ChatArchiveWorkspaceSelection selection, bool isSelected)
    {
        selection.IsSelected = isSelected;
        this.exportResult = null;
    }

    private void SelectAllWorkspaces(bool isSelected)
    {
        foreach (var selection in this.workspaceSelections)
            selection.IsSelected = isSelected;

        this.includeTemporaryChats = isSelected && this.temporaryChatCount > 0;
        this.exportResult = null;
    }

    private async Task StartExportAsync()
    {
        if (this.isBusy || !this.IsAnythingSelected)
            return;

        this.isBusy = true;
        try
        {
            var suggestedFileName = $"ai-studio-chats-{DateTime.Now:yyyy-MM-dd}{ChatArchiveFormat.FILE_EXTENSION}";
            var saveResponse = await this.RustService.SaveFile(T("Export chats"), [FileTypes.CHAT_ARCHIVE], suggestedFileName);
            if (saveResponse.UserCancelled)
                return;

            this.exportResult = null;
            this.isProcessing = true;
            this.progress = new(0, this.SelectedChatCount);
            this.cancellationTokenSource = new();

            var workspaceIds = this.workspaceSelections
                .Where(selection => selection.IsSelected)
                .Select(selection => selection.WorkspaceId)
                .ToList();

            // Compressing on the UI thread would keep the progress bar and the cancel
            // button from updating for the whole run:
            var token = this.cancellationTokenSource.Token;
            var reporter = this.CreateProgressReporter();
            this.exportResult = await Task.Run(() => ChatArchiveExporter.ExportAsync(workspaceIds, this.includeTemporaryChats, this.includeAttachments, saveResponse.SaveFilePath, reporter, token), token);
        }
        catch (OperationCanceledException)
        {
            this.Logger.LogInformation("The user cancelled the chat export.");
        }
        finally
        {
            this.isProcessing = false;
            this.DisposeCancellation();
            this.isBusy = false;
        }
    }

    private async Task ChooseArchiveAsync()
    {
        if (this.isBusy)
            return;

        this.isBusy = true;
        try
        {
            var selectionResponse = await this.RustService.SelectFile(T("Select a chat archive"), [FileTypes.CHAT_ARCHIVE]);
            if (selectionResponse.UserCancelled)
                return;

            this.selectedArchivePath = selectionResponse.SelectedFilePath;
            this.importResult = null;
            this.importPreview = await ChatArchiveImporter.ReadPreviewAsync(this.selectedArchivePath, CancellationToken.None);
        }
        finally
        {
            this.isBusy = false;
        }
    }

    private async Task StartImportAsync()
    {
        if (this.isBusy || !this.CanImport)
            return;

        this.isBusy = true;
        try
        {
            this.importResult = null;
            this.isProcessing = true;
            this.progress = new(0, this.importPreview!.Manifest.TotalChatCount);
            this.cancellationTokenSource = new();

            var archivePath = this.selectedArchivePath;
            var token = this.cancellationTokenSource.Token;
            var reporter = this.CreateProgressReporter();
            this.importResult = await Task.Run(() => ChatArchiveImporter.ImportAsync(archivePath, this.collisionBehavior, reporter, token), token);

            //
            // Drop the preview once everything was imported. Otherwise, another click on the
            // import button would silently import the whole archive a second time:
            //
            if (this.importResult is { Success: true, Cancelled: false })
            {
                this.importPreview = null;
                this.selectedArchivePath = string.Empty;
            }
        }
        catch (OperationCanceledException)
        {
            this.Logger.LogInformation("The user cancelled the chat import.");
        }
        finally
        {
            this.isProcessing = false;
            this.DisposeCancellation();
            this.isBusy = false;

            await this.LoadWorkspacesAsync();
        }
    }

    private void CancelProcessing() => this.cancellationTokenSource?.Cancel();

    /// <summary>
    /// Detaches the cancellation source before disposing it, so that a late click on the
    /// cancel button cannot reach a disposed source.
    /// </summary>
    private void DisposeCancellation()
    {
        var cancellation = this.cancellationTokenSource;
        this.cancellationTokenSource = null;

        cancellation?.Dispose();
    }

    private IProgress<ChatArchiveProgress> CreateProgressReporter()
    {
        this.lastProgressRender = DateTimeOffset.MinValue;
        return new Progress<ChatArchiveProgress>(value =>
        {
            this.progress = value;

            // Render at most every PROGRESS_RENDER_INTERVAL, but never skip the final state:
            var now = DateTimeOffset.UtcNow;
            if (value.ProcessedChats < value.TotalChats && now - this.lastProgressRender < PROGRESS_RENDER_INTERVAL)
                return;

            this.lastProgressRender = now;
            this.InvokeAsync(this.StateHasChanged);
        });
    }
}