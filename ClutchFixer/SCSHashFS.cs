using System.IO.Compression;
using System.Text;

namespace ClutchFixer;

internal class SCSHashFS : IDisposable
{
  public string Path { get; private set; }
  public int EntryCount => entries.Count;

  private const uint Magic = 0x23534353; // as ascii: "SCS#"
  private const ushort SupportedVersion = 1;
  private const string SupportedHashMethod = "CITY";
  private const string RootPath = "/";

  private const string dirMarker = "*";

  private BinaryReader reader;

  private ushort Salt;
  private string HashMethod;
  private uint EntriesCount;
  private uint StartOffset;

  private Dictionary<ulong, HashFSEntry> entries = new();

  public SCSHashFS(string scsPackPath)
  {
    Path = scsPackPath;
    reader = new BinaryReader(new FileStream(scsPackPath, FileMode.Open));

    uint magic = reader.ReadUInt32();
    if (magic != Magic)
      throw new InvalidDataException("Probably not a HashFS file.");

    ushort version = reader.ReadUInt16();
    if (version != SupportedVersion)
      throw new NotSupportedException($"Version {version} is not supported.");

    Salt = reader.ReadUInt16();

    HashMethod = new string(reader.ReadChars(4));
    if (HashMethod != SupportedHashMethod)
      throw new NotSupportedException($"Hash method \"{HashMethod}\" is not supported.");

    EntriesCount = reader.ReadUInt32();
    StartOffset = reader.ReadUInt32();

    reader.BaseStream.Position = StartOffset;

    for (int i = 0; i < EntriesCount; i++)
    {
      var entry = new HashFSEntry
      {
        Hash = reader.ReadUInt64(),
        Offset = reader.ReadUInt64(),
        Flags = new HashFSFlagField(reader.ReadUInt32()),
        Crc = reader.ReadUInt32(),
        Size = reader.ReadUInt32(),
        CompressedSize = reader.ReadUInt32()
      };
      entries.Add(entry.Hash, entry);
    }
  }

  /// <summary>
  /// Opens a HashFS file.
  /// </summary>
  /// <param name="path"></param>
  /// <returns></returns>
  public static SCSHashFS Open(string scsPackPath)
  {
    var hfr = new SCSHashFS(scsPackPath);
    return hfr;
  }

  /// <summary>
  /// Checks if an entry exists and returns its type if it does.
  /// </summary>
  /// <param name="path"></param>
  /// <returns></returns>
  public HashFSEntry.EntryType EntryExists(string path)
  {
    path = RemoveTrailingSlash(path);
    var hash = HashPath(path);

    if (entries.TryGetValue(hash, out var entry))
    {
      return entry.IsDirectory
          ? HashFSEntry.EntryType.Directory
          : HashFSEntry.EntryType.File;
    }
    return HashFSEntry.EntryType.NotFound;
  }

  public Dictionary<ulong, HashFSEntry> GetEntries()
  {
    return new Dictionary<ulong, HashFSEntry>(entries);
  }

  /// <summary>
  /// Extracts and decompresses an entry to memory.
  /// </summary>
  /// <param name="path"></param>
  /// <returns></returns>
  public byte[] Extract(string path)
  {
    if (EntryExists(path) == HashFSEntry.EntryType.NotFound)
      throw new FileNotFoundException();

    var entry = GetEntryHeader(path);
    return GetEntryContent(entry);
  }

  /// <summary>
  /// Extracts and decompresses an entry to memory.
  /// </summary>
  /// <param name="entry">The entry header of the file to extract.</param>
  /// <returns></returns>
  public byte[] Extract(HashFSEntry entry)
  {
    if (!entries.ContainsValue(entry))
      throw new FileNotFoundException();

    return GetEntryContent(entry);
  }

  /// <summary>
  /// Extracts and decompresses an entry to a file.
  /// </summary>
  /// <param name="path">The path of the file in the archive.</param>
  /// <param name="outputPath">The output path.</param>
  public void ExtractToFile(string path, string outputPath)
  {
    if (EntryExists(path) == HashFSEntry.EntryType.NotFound)
      throw new FileNotFoundException();

    var entry = GetEntryHeader(path);
    ExtractToFile(entry, outputPath);
  }

  /// <summary>
  /// Extracts and decompresses an entry to a file.
  /// </summary>
  /// <param name="entry">The entry header of the file to extract.</param>
  /// <param name="outputPath">The output path.</param>
  public void ExtractToFile(HashFSEntry entry, string outputPath)
  {
    if (entry.Size == 0)
    {
      // create an empty file
      File.Create(outputPath).Dispose();
      return;
    }

    reader.BaseStream.Position = (long)entry.Offset;
    using var fileStream = new FileStream(outputPath, FileMode.Create);
    if (entry.IsCompressed)
    {
      var zlibStream = new ZLibStream(reader.BaseStream, CompressionMode.Decompress);
      zlibStream.CopyTo(fileStream, (int)entry.CompressedSize);
    }
    else
    {
      var buffer = new byte[(int)entry.Size];
      reader.BaseStream.Read(buffer, 0, (int)entry.Size);
      fileStream.Write(buffer, 0, (int)entry.Size);
    }
  }

  public IEnumerable<string> EnumerateDirectory(HashFSEntry rootDirectory, HashFSEntry.EntryType fileType)
  {
    bool isDir;

    var dirEntries = Encoding.ASCII.GetString(GetEntryContent(rootDirectory)).Split("\n");

    var result = new List<string>();

    for (int i = 0; i < dirEntries.Length; i++)
    {
      isDir = dirEntries[i].StartsWith(dirMarker);
      if ((isDir && (fileType & HashFSEntry.EntryType.Directory) != 0) ||
          (!isDir && (fileType & HashFSEntry.EntryType.File) != 0))
        result.Add(isDir ? dirEntries[i][1..] + "/" : dirEntries[i]);
    }

    return result;
  }

  public IEnumerable<string> EnumerateFiles(string path)
  {
    path = RemoveTrailingSlash(path);

    var entryType = EntryExists(path);
    if (entryType == HashFSEntry.EntryType.NotFound)
      throw new DirectoryNotFoundException();
    else if (entryType != HashFSEntry.EntryType.Directory)
      throw new ArgumentException($"\"{path}\" is not a directory.");

    return EnumerateDirectory(GetEntryHeader(path), HashFSEntry.EntryType.File);
  }

  public IEnumerable<string> EnumerateDirectories(string path)
  {
    path = RemoveTrailingSlash(path);

    var entryType = EntryExists(path);
    if (entryType == HashFSEntry.EntryType.NotFound)
      throw new DirectoryNotFoundException();
    else if (entryType != HashFSEntry.EntryType.Directory)
      throw new ArgumentException($"\"{path}\" is not a directory.");

    return EnumerateDirectory(GetEntryHeader(path), HashFSEntry.EntryType.Directory);
  }

  private byte[] GetEntryContent(HashFSEntry entry)
  {
    reader.BaseStream.Position = (long)entry.Offset;
    byte[] file;
    if (entry.IsCompressed)
    {
      var zippedstream = new MemoryStream(reader.ReadBytes((int)entry.CompressedSize));
      var unzip = new ZLibStream(zippedstream, CompressionMode.Decompress);

      var buf = new byte[16384];
      int chunkId;
      var ostream = new MemoryStream();
      while ((chunkId = unzip.Read(buf, 0, buf.Length)) != 0)
        ostream.Write(buf, 0, chunkId);

      file = ostream.ToArray();
    }
    else
    {
      file = reader.ReadBytes((int)entry.Size);
    }
    return file;
  }

  private HashFSEntry GetEntryHeader(string path)
  {
    ulong hash = HashPath(path);
    var entry = entries[hash];
    return entry;
  }

  /// <summary>
  /// Hashes a file path.
  /// </summary>
  /// <param name="path"></param>
  /// <returns></returns>
  private ulong HashPath(string path)
  {
    ulong a, b, c, d, e, f, g, hash, plenLeft, tmp;
    uint plen, pos;

    const ulong prime0 = 0xc3a5c85c97cb3127UL;
    const ulong prime1 = 0xb492b66fbe98f273UL;
    const ulong prime2 = 0x9ae16a3b2f90404fUL;
    const ulong prime3 = 0xc949d7c7509e6557UL;

    if (path[0] == '/')
      path = path[1..];

    if (Salt != 0)
      path = Salt + path;

    var bytePath = Encoding.ASCII.GetBytes(path);
    plen = (uint)bytePath.Length;

    const ulong kMul = 0x9ddfea08eb382d69UL;
    ulong hashPair(ulong first, ulong second) => nshift47((second ^ nshift47((first ^ second) * kMul)) * kMul) * kMul;

    ulong rotate(ulong val, int shift) => (val >> shift) | (val << (64 - shift));

    ulong rotateNZ(ulong val, int shift) => shift == 0 ? val : rotate(val, shift);

    ulong nshift47(ulong val) => val ^ (val >> 47);

    ulong path2ULong(long pos = 0)
    {
      if (bytePath is null) throw new Exception("Call to pathToLong() where bytePath was not defined.");

      if (pos < 0)
      {
        if (plen <= pos) throw new Exception("Reference position in path ASCII string outside boundaries (len:" + plen + ", pos:" + pos + ", path:" + path + ").");
        if (pos > int.MaxValue) throw new Exception("Reference position outside Integer boundaries.");
        return BitConverter.ToUInt64(bytePath, (int)(plen + pos));
      }
      else if (pos == 0)
      {
        return BitConverter.ToUInt64(bytePath);
      }
      else
      {
        if (pos > int.MaxValue) throw new Exception("Reference position outside Integer boundaries.");
        return BitConverter.ToUInt64(bytePath, (int)pos);
      }
    }

    (ulong, ulong) seedHash(uint strlen, ulong a, ulong b)
    {
      ulong c, w, x, y, z;

      w = path2ULong(strlen);
      x = path2ULong(strlen + 8);
      y = path2ULong(strlen + 16);
      z = path2ULong(strlen + 24);

      a += w;
      b = rotateNZ(b + a + z, 21);

      c = a;
      a += x + y;
      b += rotateNZ(a, 44);

      return (a + z, b + c);
    }

    if (plen <= 16)
    {
      if (plen > 8)
      {
        tmp = path2ULong(-8);
        hash = hashPair(path2ULong(), rotate(tmp + plen, bytePath.Length)) ^ tmp;
      }
      else if (plen >= 4)
      {
        hash = hashPair(
            plen + ((ulong)BitConverter.ToUInt32(bytePath) << 3),
            BitConverter.ToUInt32(bytePath, (int)plen - 4)
        );
      }
      else if (plen > 0)
      {
        hash = nshift47(
            (bytePath[0] + ((uint)bytePath[plen >> 1] << 8)) * prime2 ^
            (plen + ((uint)bytePath[plen - 1] << 2)) * prime3
        ) * prime2;
      }
      else
      {
        hash = prime2;
      }
    }
    else if (plen <= 32)
    {
      a = path2ULong() * prime1;
      b = path2ULong(8);
      c = path2ULong(-8) * prime2;
      d = path2ULong(-16) * prime0;
      hash = hashPair(rotateNZ(a - b, 43) + rotateNZ(c, 30) + d, a + rotateNZ(b ^ prime3, 20) - c + plen);
    }
    else if (plen <= 64)
    {
      a = path2ULong(24);
      b = path2ULong() + (plen + path2ULong(-16)) * prime0;
      c = rotateNZ(a + b, 52);
      d = rotateNZ(b, 37);

      b += path2ULong(8);
      d += rotateNZ(b, 7);

      b += path2ULong(16);

      e = a + b;
      f = c + rotateNZ(b, 31) + d;

      a = path2ULong(16) + path2ULong(-32);
      b = path2ULong(-8);
      c = rotateNZ(a + b, 52);
      d = rotateNZ(a, 37);

      a += path2ULong(-24);
      d += rotateNZ(a, 7);
      a += path2ULong(-16);

      b += a;
      d += c + rotateNZ(a, 31);

      e = nshift47((e + d) * prime2 + (b + f) * prime0);
      hash = nshift47(e * prime0 + f) * prime2;
    }
    else // over 64 bytes long ASCII path.
    {
      a = path2ULong(-40);
      b = path2ULong(-16) + path2ULong(-56);
      c = hashPair(path2ULong(-48) + plen, path2ULong(-24));
      (d, e) = seedHash(plen - 64, plen, c);
      (f, g) = seedHash(plen - 32, b + prime1, a);
      a = a * prime1 + path2ULong();

      pos = 0;
      plenLeft = (plen - 1) & ~(uint)63;
      do
      {
        a = (rotateNZ(a + b + d + path2ULong(pos + 8), 37) * prime1) ^ g;
        b = rotateNZ(b + e + path2ULong(pos + 48), 42) * prime1 + d + path2ULong(pos + 40);
        c = rotateNZ(c + f, 33) * prime1;
        (d, e) = seedHash(pos, e * prime1, a + f);
        (f, g) = seedHash(pos + 32, c + g, b + path2ULong(pos + 16));
        tmp = c;
        c = a;
        a = tmp;

        pos += 64;
        plenLeft -= 64;
      } while (plenLeft != 0);

      hash = hashPair(hashPair(d, f) + nshift47(b) * prime1 + c,
                      hashPair(e, g) + a);
    }

    return hash;
  }

  private string RemoveTrailingSlash(string path)
  {
    if (path.EndsWith("/") && path != RootPath)
      path = path[0..^1];
    return path;
  }

  public void Dispose()
  {
    reader.BaseStream.Dispose();
    reader.Dispose();
  }
}
