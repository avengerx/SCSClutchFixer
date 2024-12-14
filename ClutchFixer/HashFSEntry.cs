namespace ClutchFixer;

/// <summary>
/// Represents an entry header which contains metadata about a file in a HashFS archive.
/// </summary>
public struct HashFSEntry
{
  /// <summary>
  /// Return type for HashFsReader.EntryExists().
  /// </summary>
  public enum EntryType
  {
    NotFound = 0,
    File = 1,
    Directory = 2
  }

  /// <summary>
  /// Hash of the full path of the file.
  /// </summary>
  public ulong Hash { get; internal set; }

  /// <summary>
  /// Start of the file contents in the archive.
  /// </summary>
  public ulong Offset { get; internal set; }

  /// <summary>
  /// Size of the file when uncompressed.
  /// </summary>
  public uint Size { get; internal set; }

  /// <summary>
  /// Size of the file in the archive.
  /// </summary>
  public uint CompressedSize { get; internal set; }

  /// <summary>
  /// If true, the entry is a directory listing.
  /// </summary>
  public bool IsDirectory { get; set; }

  public bool IsCompressed { get; set; }
}
