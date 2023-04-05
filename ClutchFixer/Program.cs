using ClutchFixer;
using Microsoft.Win32;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;

const string trucksPath = "def/vehicle/truck";
const string engineDir = "engine";
string steamAppsPath = "steamapps" + Path.DirectorySeparatorChar + "common";

uint atsAppId = 270880;
uint ets2AppId = 227300;
if (!OperatingSystem.IsWindows())
{
  Console.Error.WriteLine("Only windows OS is supported.");
  Environment.Exit(1);
}

#pragma warning disable CA1416

var sKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam");
string steamInstallPath = string.Empty;

if (sKey != null)
{
#pragma warning disable CS8600
  steamInstallPath = (string)sKey.GetValue("SteamPath", string.Empty);
#pragma warning restore CS8600

  if (!Directory.Exists(steamInstallPath))
  {
    Console.Error.WriteLine("Steam installation path not found: " + steamInstallPath);
    Environment.Exit(1);
  }
}
else
{
  Console.Error.WriteLine("Unable to locate Steam installation.");
  Environment.Exit(1);
}

var steamAppInstallPaths = new SteamVDF(steamInstallPath);

foreach (var appId in new uint[] { atsAppId, ets2AppId })
{
  sKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\Apps\\" + appId);

  if (sKey != null)
  {
#pragma warning disable CS8605
    bool installed = (int)sKey.GetValue("Installed", 0) == 1;
#pragma warning restore CS8605

#pragma warning disable CS8600
    var name = (string)sKey.GetValue("Name", string.Empty);
#pragma warning restore CS8600

    Console.WriteLine(name + " (#" + appId + ") installed: " + (installed ? "yes" : "no"));

    if (installed)
    {
      if (name is null) throw new Exception("Unable to fetch game name off Steam registry entry.");

      string instPath;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
      if (steamAppInstallPaths.PathByAppId.TryGetValue(appId, out instPath))
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
      {
        instPath = (instPath + Path.DirectorySeparatorChar + steamAppsPath + Path.DirectorySeparatorChar + name).Replace("\\\\", "\\");

        if (!Directory.Exists(instPath))
        {
          Console.Error.WriteLine("Unable to locate " + name + " installation directory: " + instPath);
          Environment.Exit(1);
        }
        else
        {
          Console.Write("Game '" + name + "' found." + Environment.NewLine +
            "Installation Path: " + instPath + Environment.NewLine +
            "Create a mod for it? [yn] ");

          uint tries = 0;
          bool makeMod = false;
          while (tries < 15)
          {
            var key = Console.ReadKey();

            if (key.Key == ConsoleKey.Y)
            {
              Console.WriteLine("es.");
              makeMod = true;
              break;
            }
            else if (key.Key == ConsoleKey.N)
            {
              Console.WriteLine("o.");
              break;
            }
          }

          if (!makeMod) continue;

          var mod = new ClutchFix(name, instPath);
          string siiPath, dlcName;
          byte[] siiData;

          foreach (var file in Directory.EnumerateFiles(instPath, "*.scs"))
          {
            var baseFile = Path.GetFileName(file).ToLowerInvariant();

            if (baseFile == "base.scs" || baseFile == "def.scs" || baseFile.StartsWith("dlc_"))
            {
              var scspak = SCSHashFS.Open(file); // fixme

              Console.WriteLine("SCS Pack file: " + file + Environment.NewLine + 
                "  Contained files: " + scspak.EntryCount);

              var truckEngineFiles = new List<string>();

              if (baseFile.StartsWith("dlc_")) dlcName = Path.GetFileNameWithoutExtension(baseFile);
              else dlcName = string.Empty;

              if (scspak.EntryExists(trucksPath) == HashFSEntry.EntryType.Directory)
              {
                foreach (var truckDir in scspak.EnumerateDirectories(trucksPath))
                {
                  if (scspak.EntryExists(trucksPath + "/" + truckDir + engineDir) == HashFSEntry.EntryType.Directory)
                  {
                    foreach (var engineFile in scspak.EnumerateFiles(trucksPath + "/" + truckDir + engineDir))
                    {
                      if (engineFile.EndsWith(".sii"))
                      {
                        siiPath = trucksPath + "/" + truckDir + engineDir + "/" + engineFile;
                        siiData = scspak.Extract(siiPath);

                        if (siiData.Length > 0)
                        {
                          truckEngineFiles.Add(siiPath);
                          mod.AddEngine(siiPath.Replace('/', Path.DirectorySeparatorChar), Encoding.UTF8.GetString(siiData), dlcName);
                        }
                      }
                    }
                  }
                }
              }

              scspak.Dispose();

              if (truckEngineFiles.Count > 0)
              {
                Console.WriteLine("  Found " + truckEngineFiles.Count + " engine definition files.");
                //foreach (var efrp in truckEngineFiles) Console.WriteLine("  " + trucksPath + "/" + efrp);
                //Console.WriteLine("  End of list.");
              }
              else Console.WriteLine("  SCS pack has no engine files.");
            }
          }

          if (mod.HasEngines) mod.MakeManifest();
        }
      }
      else
      {
        Console.Error.WriteLine("Unable to locate " + name + " installation directory within Steam.");
        Environment.Exit(1);
      }
    }
  }
  else Console.WriteLine("App #" + appId + " installed: no (absent)");
}
#pragma warning restore CA1416
