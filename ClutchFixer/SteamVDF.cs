using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClutchFixer;
public class SteamVDF
{
  private string vdfPath;

  public IReadOnlyDictionary<uint, string> PathByAppId { get; private set; }

  public SteamVDF(string path)
  {
    var pbai = new Dictionary<uint, string>();
    PathByAppId = pbai;

    if (path == null) throw new ArgumentNullException("Path to Steam VDF file should not be null.");

    // If a directory is passed, assume it is steam installation directory and try
    // its steamapps/libraryfolders.vdf
    if (Directory.Exists(path) && File.Exists(path + Path.DirectorySeparatorChar + "steamapps" + Path.DirectorySeparatorChar + "libraryfolders.vdf"))
    {
      path = path + Path.DirectorySeparatorChar + "steamapps" + Path.DirectorySeparatorChar + "libraryfolders.vdf";
    }
    else if (!File.Exists(path) || !path.EndsWith(".vdf"))
      throw new ArgumentException("File does not exist or is not a .vdf file: " + path);

    vdfPath = path;

    var vdfHandle = File.OpenRead(vdfPath);

    var curPath = string.Empty;

    var buf = new char[8192];

    string readVdfLine()
    {
      int buflen = 0;
      int bufr;
      char bufc = '\0';

#pragma warning disable CS8602
      while ((bufr = vdfHandle.ReadByte()) >= 0)
      {
        bufc = (char)bufr;

        if (bufc == '\r') continue;
        if (bufc == '\n')
        {
          break;
        }
        else buf[buflen++] = bufc;
      }

      if (buflen == 0) return string.Empty;

      return new string(buf[0..(buflen)]);
#pragma warning restore CS8602
    }

    bool readingApps = false;
    uint readAppId;
    while (vdfHandle.Position < vdfHandle.Length)
    {
      var line = readVdfLine();

      var match = Regex.Match(line, "^[ \t]*\"path\"[ \t]+\"([^\"]+)\"");

      if (match.Success) curPath = match.Groups[1].Value;

      if (curPath.Length > 0)
      {
        if (readingApps)
        {
          if (Regex.Match(line, "^[ \t]*}[ \t]*$").Success) readingApps = false;
          else
          {
            match = Regex.Match(line, "^[ \t]*\"([0-9]+)\"[ \t]+\"[0-9]+\"");
            if (match.Success && uint.TryParse(match.Groups[1].Value, out readAppId))
              pbai.Add(readAppId, curPath);
          }
        }
        else if (Regex.Match(line, "^[ \t]*\"apps\"").Success)
          readingApps = true;
      }
    }

    vdfHandle.Close();
  }
}
