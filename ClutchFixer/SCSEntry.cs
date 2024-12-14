using System.Runtime.InteropServices;

namespace ClutchFixer;

[StructLayout(LayoutKind.Explicit)]
internal struct SCSEntry
{
  [FieldOffset(0)]
  public ulong Hash;

  [FieldOffset(8)]
  public uint MetadataIdx;

  [FieldOffset(12)]
  public ushort MetadataCount;

  [FieldOffset(14)]
  public ushort Flags;
}
