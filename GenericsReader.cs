using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CsharpVoxReader
{
  public class GenericsReader {

    public static byte[] ReadByteArray(BinaryReader br, ref int readsize) {
      Int32 numChars = br.ReadInt32();
      readsize += sizeof(Int32) + numChars;

      return br.ReadBytes(numChars);
    }

    public static Dictionary<string, byte[]> ReadDict(BinaryReader br, ref int readsize) {
      Dictionary<string, byte[]> result = new Dictionary<string, byte[]>();

      Int32 numElements = br.ReadInt32();
      readsize += sizeof(Int32);

      for (int i=0; i < numElements; i++) {
        string key = Encoding.UTF8.GetString(ReadByteArray(br, ref readsize));
        byte[] value = ReadByteArray(br, ref readsize);

        result[key] = value;
      }

      return result;
    }

    public static Vector3 ReadTranslation(byte[] v)
    {
      var m = Regex.Match(Encoding.ASCII.GetString(v), @"(-?\d+)\s(-?\d+)\s(-?\d+)");
      if (m.Success && m.Groups.Count == 4)
      {
        return new Vector3 {X = int.Parse(m.Groups[1].Value), Y = int.Parse(m.Groups[2].Value), Z = int.Parse(m.Groups[3].Value)};
      }

      return default;
    }
    
    public static Matrix3x3 ReadRotation(byte[] v)
    {

      var rot = int.Parse(Encoding.ASCII.GetString(v));
       int r0v = ((rot & 16) == 0)?1:-1;
       int r1v = ((rot & 32) == 0)?1:-1;
       int r2v = ((rot & 64) == 0)?1:-1;

       int r0i = rot & 3;
       int r1i = (rot & 12) >> 2;
       /*
       Truth table for the third index
         r0| 0 | 1 | 2 |
       r1--+---+---+---+
        0  | X | 2 | 1 |
       ----+---+---+---+
        1  | 2 | X | 0 |
       ----+---+---+---+
        2  | 1 | 0 | X |
       ----+---+---+---+

       Derived function
       f(r0, r1) = 3 - r0 - r1
       */
       int r2i = 3 - r0i - r1i;

       int[] result = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };

       result[r0i] = r0v;
       result[r1i + 3] = r1v;
       result[r2i + 6] = r2v;

       return new Matrix3x3
       {
         RowX = new Vector3(result[0], result[1], result[2]),
         RowY = new Vector3(result[3], result[4], result[5]),
         RowZ = new Vector3(result[6], result[7], result[8])
       };
    }
  }
}
