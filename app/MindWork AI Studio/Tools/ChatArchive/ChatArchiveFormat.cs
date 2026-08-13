namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Central definition of the chat archive format. A chat archive is a ZIP file which mirrors
/// the chat storage layout, so that exported chats can be restored without any conversion.
/// </summary>
public static class ChatArchiveFormat
{
    /// <summary>
    /// The archive format version written by this app version. The importer refuses archives
    /// with a higher version, because it cannot know what those archives contain.
    /// </summary>
    public const int FORMAT_VERSION = 1;

    /// <summary>
    /// The file extension used for chat archives.
    /// </summary>
    public const string FILE_EXTENSION = ".aistudio-chats";

    /// <summary>
    /// The manifest file at the archive root. It describes what the archive contains.
    /// </summary>
    public const string MANIFEST_FILE_NAME = "manifest.json";

    /// <summary>
    /// The archive directory containing all exported workspaces.
    /// </summary>
    public const string WORKSPACES_DIRECTORY = "workspaces";

    /// <summary>
    /// The archive directory containing all exported temporary chats.
    /// </summary>
    public const string TEMPORARY_CHATS_DIRECTORY = "tempChats";

    /// <summary>
    /// The chat-relative directory holding all attachment files of a chat.
    /// </summary>
    public const string ATTACHMENTS_DIRECTORY = "attachments";

    /// <summary>
    /// The chat-relative directory where attachments are stored which were located outside
    /// of their chat directory. Those files have no place in the mirrored layout, so the
    /// export collects them here and rewrites the attachment paths accordingly. On import,
    /// they end up in this directory of the chat, next to the transcripts.
    /// </summary>
    public const string IMPORTED_ATTACHMENTS_DIRECTORY = $"{ATTACHMENTS_DIRECTORY}/imported";

    /// <summary>
    /// The file storing the workspace or chat name.
    /// </summary>
    public const string NAME_FILE_NAME = "name";

    /// <summary>
    /// The file storing the serialized chat thread.
    /// </summary>
    public const string THREAD_FILE_NAME = "thread.json";

    /// <summary>
    /// The separator used inside ZIP archives. ZIP entries always use forward slashes,
    /// regardless of the operating system.
    /// </summary>
    public const char ENTRY_SEPARATOR = '/';
}