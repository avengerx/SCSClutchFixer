using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ClutchFixer;
public class ClutchFix
{
  public const string ModName = "Clutch Fix";
    
  public string ModPath { get; private set; }
  public string GameName { get; private set; }
  public bool HasEngines { get; private set; } = false;

  private string version = string.Empty;
  private string exactVer = string.Empty;

  private uint engineCount;
  private bool dirsMade = false;

  // https://modding.scssoft.com/wiki/Documentation/Engine/Units/accessory_engine_data
  // Gives the 300rpm a torque value of 0.4 to drastically improve
  // the clutch functionality. This means truck would have 40% strength
  // until it gets its "cut rpm" to stall, or just recover ahead.
  private const string defaultTorqueCurve = @"
$2torque_curve[]: (300, 0)
$2torque_curve[]: (310, 0.45)
$2torque_curve[]: (440, 0.6)
$2torque_curve[]: (1000, 1)
$2torque_curve[]: (1300, 1)
$2torque_curve[]: (1900, 0.77)
$2torque_curve[]: (2400, 0.4)
$2torque_curve[]: (2600, 0)";

  private HashSet<string> dlcs = new();

  private Regex torque_re = new Regex(@"((\n[\t ]*torque_curve\[[\t ]*\]:[\t ]*\()[0-9]+ *, *0(?:.0)? *\))", RegexOptions.Compiled | RegexOptions.Singleline);

  public ClutchFix(string gameName, string gameInstallPath)
  {
    if (gameName == null) throw new ArgumentNullException("Game name must be specified.");
    if (gameInstallPath == null) throw new ArgumentNullException("Game installation path must be specified.");

    GameName = gameName;

    getVersion(gameInstallPath);

    ModPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
      gameName,
      "mod",
      ModName.Replace(" ", "")
    );
  }

  public void AddEngine(string targetPath, string contents, string dlc)
  {
    if (string.IsNullOrEmpty(targetPath)) throw new Exception("Engine definition must have a target directory.");
    if (string.IsNullOrEmpty(contents)) throw new Exception("No content specified for the engine definion file.");
    if (!contents.StartsWith("SiiNunit"))
      throw new Exception(".sii contents don't seem to be in the right format for: " + targetPath);

    var destDir = Path.GetDirectoryName(targetPath);

    if (destDir == null) destDir = ModPath;
    else destDir = Path.Combine(ModPath, destDir);

    var destFile = Path.GetFileName(targetPath);

    // We don't fiddle with the secondary torque (if any defined) because
    // it doesn't play a role in the engine in low gears/rpm.
    if (!contents.Contains("torque_curve[]:"))
    {
      contents = Regex.Replace(contents, @"(\n([\t ]*)volume:[\t ]*[0-9]+(\.[0-9]+){0,1}\n)", "${1}" + defaultTorqueCurve + "\n\n");
    }
    else
    {
      // Gives the first torque entry a strength of 0.4 (40%), see
      // defaultTorqueCurve above.
      contents = torque_re.Replace(contents, "${1}${2}310, 0.45)", 1);
    }

    if (!dirsMade) makeDirs();
    if (!Directory.Exists(destDir))
    {
      Directory.CreateDirectory(destDir);
    }

    if (!string.IsNullOrWhiteSpace(dlc) && !dlcs.Contains(dlc))
      dlcs.Add(dlc);

    File.WriteAllText(Path.Combine(destDir, destFile), contents);

    if (!HasEngines)
    {
      HasEngines = true;
    }

    engineCount++; // TODO: track also truck make/model + engine name/desc to add to mod description.

    Console.WriteLine("- Engine added: " + targetPath);
  }

  public void MakeManifest()
  {
    var todayISO = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt");
    if (!HasEngines)
      throw new Exception("No engine files loaded into mod; there's no sense in generating the mod manifest at all.");
    string desc = @"[white]Clutch Improvements mod[normal]

This mod is based in ""Engine Idle Torque 'Fix'"" mod for ATS, but applied to all engines (and no sound changes). It simply increases all engines' torque in low RPM to allow more realistic clutch interaction like holding on ramps and start moving without the need to smash the gas pedal in low gears / lightly loaded.

Clutch deadzone-range should be reduced in options for additional realism (smaller actuation/""bite"" interval in the pedal).

The process to generate this mod is automated by a console app (.NET 9) that is hosted at https://github.com/avengerx/SCSClutchFixer.

Generated for game version: [white]" + exactVer + @"[normal]
Truck engines fixed: [white]" + engineCount + @"[normal]
Generation Date: [white]" + todayISO + "[normal]";

    string contents = @"SiiNunit
{
mod_package : .package_name
{
  package_version: ""v1.0.0""
  display_name: ""Clutch Fix""
  author: ""Heavenly Avenger""
  category[]: ""truck""
  description_file: ""desc.txt""
  compatible_versions[]: """ + version + @"""
  mp_mod_optional: true
";
    if (dlcs.Count > 0)
    {
      foreach (var dlc in dlcs)
      {
        contents += "  dlc_dependencies[]: \"" + dlc + @"""
";
      }
    }

    contents += @"}
}";
    File.WriteAllText(Path.Combine(ModPath, "manifest.sii"), contents);
    File.WriteAllText(Path.Combine(ModPath, "desc.txt"), desc);
    Console.WriteLine("- Mod manifest written: " + Path.Combine(ModPath, "manifest.sii"));
  }

  private void getVersion(string instPath)
  {
    var dir = Path.Combine(instPath, "bin", "win_x64");

    if (Directory.Exists(dir))
    {
      var exePath = Path.Combine(dir, "amtrucks.exe");
      if (!File.Exists(exePath))
      {
        exePath = Path.Combine(dir, "eurotrucks2.exe");
        if (!File.Exists(exePath))
          throw new Exception("Unable to locate game binaries to extract version info from: neither amtrucks.exe nor eurotrucks2.exe were found.");
      }

      var vi = FileVersionInfo.GetVersionInfo(exePath);

      if (string.IsNullOrEmpty(vi.ProductVersion))
        throw new Exception("Unable to fetch version string from " + GameName + "'s executable.");

      version = vi.ProductMajorPart + "." + vi.ProductMinorPart + ".*";
      exactVer = vi.ProductVersion;
    }
    else
      throw new Exception("Unable to locate game binaries to extract version info from: " + dir);

    Console.WriteLine("- Game version read: " + version);
  }

  private void makeDirs()
  {
    string dir;
    if (!Directory.Exists(ModPath))
    {
      dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
      if (!Directory.Exists(dir))
        throw new Exception("Unable to locate base Documents directory at: " + dir);

      dir = Path.Combine(dir, GameName);
      if (!Directory.Exists(dir))
        throw new Exception("Unable to locate " + GameName + " config directory at: " + dir);

      foreach (var thisDir in new[] { Path.Combine(dir, "mod"), ModPath })
      {
        if (!Directory.Exists(thisDir))
        {
          try
          {
            Directory.CreateDirectory(thisDir);
          }
          catch (Exception e)
          {
            throw new Exception("Unable to create directory: " + thisDir + Environment.NewLine +
              "Exception: " + e.ToString() + Environment.NewLine +
              "Description: " + e.Message);
          }
        }
      }
      Console.WriteLine("- Mod directory structure created successfully: " + ModPath);
    }
    else Console.WriteLine("- Mod directory already exists. Using it: " + ModPath);

    dirsMade = true;
  }
}
