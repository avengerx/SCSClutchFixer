﻿namespace ClutchFixer;

public struct HashFSFlagField
{
  private uint bits;
  public uint Bits
  {
    get => bits;
    set => bits = value;
  }

  private const int byteSize = 8;
  private const uint byteMask = 0xFFU;

  public HashFSFlagField(uint bits)
  {
    this.bits = bits;
  }

  public bool this[int index]
  {
    get
    {
      AssertInRange(index, 0, 31);
      var mask = 1U << index;
      return (bits & mask) == mask;
    }
    set
    {
      AssertInRange(index, 0, 31);
      var mask = 1U << index;
      if (value)
        bits |= mask;
      else
        bits &= ~mask;
    }
  }

  public byte GetByte(int index)
  {
    AssertInRange(index, 0, 3);
    var mask = byteMask << index * byteSize;
    return (byte)((bits & mask) >> index * byteSize);
  }

  public void SetByte(int index, byte value)
  {
    AssertInRange(index, 0, 3);
    var mask = byteMask << index * byteSize;
    bits &= ~mask; // clear
    bits |= (uint)value << index * byteSize; // set
  }

  public bool[] ToBoolArray()
  {
    var arr = new bool[32];
    var mask = 1U;
    for (int i = 0; i < 32; i++)
    {
      arr[i] = (bits & mask) == mask;
      mask <<= 1;
    }
    return arr;
  }

  public uint GetBitString(int start, int length)
  {
    if (length == 0) return 0;
    AssertInRange(start, 0, 31);
    AssertInRange(length, 0, 31);

    if ((start + length) > 32)
      throw new IndexOutOfRangeException();

    var mask = (uint)((1UL << length) - 1) << start;
    return (bits & mask) >> start;
  }

  public void SetBitString(int start, int length, uint value)
  {
    if (length == 0)
      return;

    AssertInRange(start, 0, 31);
    AssertInRange(length, 0, 31);

    if ((start + length) > 32)
      throw new IndexOutOfRangeException();

    var mask = (uint)((1UL << length) - 1);

    // trim value first
    value &= mask;

    bits &= ~(mask << start); // clear
    bits |= value << start;
  }

  public override string ToString() =>
      Convert.ToString(bits, 2).PadLeft(32, '0');

  public override int GetHashCode() =>
      bits.GetHashCode();

  private void AssertInRange(int i, int min, int max)
  {
    if (i > max || i < min)
      throw new IndexOutOfRangeException();
  }

  public override bool Equals(object? obj)
  {
    return obj is not null && obj is HashFSFlagField flagField && flagField.Bits == this.Bits;
  }

  public static bool operator ==(HashFSFlagField left, HashFSFlagField right) =>
      left.Equals(right);

  public static bool operator !=(HashFSFlagField left, HashFSFlagField right) =>
      !(left == right);
}