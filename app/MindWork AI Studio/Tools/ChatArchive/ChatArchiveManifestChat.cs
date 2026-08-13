namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Describes one exported chat inside a chat archive.
/// </summary>
public sealed record ChatArchiveManifestChat
{
    /// <summary>
    /// The unique identifier of the chat.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// The name of the chat at the time of the export.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The time of the last edit at the time of the export.
    /// </summary>
    public DateTimeOffset LastEditTime { get; init; }

    /// <summary>
    /// The number of attachments which were stored outside of the chat directory and
    /// were copied into the archive.
    /// </summary>
    public int IncludedExternalAttachments { get; init; }

    /// <summary>
    /// The names of attachments which could not be included, because the files were
    /// no longer present on the file system.
    /// </summary>
    public List<string> MissingAttachments { get; init; } = [];
}