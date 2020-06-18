using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace CsharpVoxReader.Chunks
{
    public class TransformNode : Chunk
    {
        public const string ID = "nTRN";

        internal override string Id
        {
            get { return ID; }
        }

        internal override int Read(BinaryReader br, IVoxLoader loader)
        {
            int readSize = base.Read(br, loader);

            Int32 id = br.ReadInt32();
            var sd = GenericsReader.ReadDict(br, ref readSize);
            var attributes = new Dictionary<string, object>();
            if (sd.TryGetValue("_name", out var v))
            {
                attributes.Add("_name", Encoding.UTF8.GetString(v));
            }

            if (sd.TryGetValue("_hidden", out v))
            {
                attributes.Add("_hidden", v[0] == '1');
            }

            Int32 childNodeId = br.ReadInt32();
            Int32 reservedId = br.ReadInt32();
            Int32 layerId = br.ReadInt32();
            Int32 numOfFrames = br.ReadInt32();

            readSize += sizeof(Int32) * 5;

            var framesAttributes = new Dictionary<string, object>[numOfFrames];

            for (int fnum=0; fnum < numOfFrames; fnum++) {
              sd = GenericsReader.ReadDict(br, ref readSize);
              var gd = new Dictionary<string, object>();
              if (sd.TryGetValue("_t", out v))
              {
                  gd.Add("_t", GenericsReader.ReadTranslation(v));
              }

              if (sd.TryGetValue("_r", out v))
              {
                  gd.Add("_r", GenericsReader.ReadRotation(v));
              }

              framesAttributes[fnum] = gd;
            }

            loader.NewTransformNode(id, childNodeId, layerId, attributes, framesAttributes);
            return readSize;
        }
    }
}
