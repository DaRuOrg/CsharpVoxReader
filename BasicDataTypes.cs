using System.Diagnostics;

namespace CsharpVoxReader
{
    [DebuggerDisplay("X:{X}, Y:{Y}, Z:{Z}")]
    public struct Vector3
    {
        public Vector3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X;
        public int Y;
        public int Z;
        
        public static Vector3 Zero => new Vector3(0, 0, 0);
    }

    [DebuggerDisplay("RowX:{RowX}, RowY:{RowY}, RowZ:{RowZ}")]
    public struct Matrix3x3
    {
        public Matrix3x3(Vector3 x, Vector3 y, Vector3 z)
        {
            RowX = x;
            RowY = y;
            RowZ = z;
        }

        public Vector3 RowX;
        public Vector3 RowY;
        public Vector3 RowZ;
        
        public Vector3 ColX => new Vector3(RowX.X, RowY.X, RowZ.X);
        public Vector3 ColY => new Vector3(RowX.Y, RowY.Y, RowZ.Y);
        public Vector3 ColZ => new Vector3(RowX.Z, RowY.Z, RowZ.Z);
        
        public static Matrix3x3 Identity => new Matrix3x3(new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1));
    }
}