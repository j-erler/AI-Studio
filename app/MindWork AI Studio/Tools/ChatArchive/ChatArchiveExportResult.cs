namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// The outcome of a chat archive export.
/// </summary>
/// <param name="Success">Whether the archive was written completely.</param>
/// <param name="ArchivePath">The path of the written archive.</param>
/// <param name="ExportedWorkspaces">The number of exported workspaces.</param>
/// <param name="ExportedChats">The number of exported chats.</param>
/// <param name="IncludedExternalAttachments">The number of attachments which were stored outside of their chat directory and were copied into the archive.</param>
/// <param name="MissingAttachments">The number of attachments which could not be included, because the files were gone.</param>
/// <param name="Issue">The reason why the export failed, if it did.</param>
/// <param name="Cancelled">Whether the user stopped the export. No archive is left behind then.</param>
public sealed record ChatArchiveExportResult(
    bool Success,
    string ArchivePath,
    int ExportedWorkspaces,
    int ExportedChats,
    int IncludedExternalAttachments,
    int MissingAttachments,
    string Issue,
    bool Cancelled);