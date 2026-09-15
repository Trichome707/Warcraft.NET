using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Warcraft.NET.Files.ADT.Chunks;
using Warcraft.NET.Files.ADT.Conversion;
using Warcraft.NET.Files.ADT.Terrain.MCNK.Flags;
using Warcraft.NET.Files.ADT.Terrain.MCNK.SubChunks;

namespace Warcraft.NET.Tests.Files.ADT.Chunks
{
    [TestClass]
    public class MH2OTests
    {
        private const int HeaderTableSize = 256 * 12;

        [TestMethod]
        public void Serialize_PlacesLayerDataAfterTheFixedHeaderTable()
        {
            // Regression: Serialize reserved the header table with SetLength but left Position at 0,
            // so the first layer's instance table was written at offset 0 and then overwritten by the
            // header table. Every MCLQ bridged through MCLQToMH2O read back as LiquidTypeId 0 with
            // no vertex data.
            var water = MCLQToMH2O.Convert(new Dictionary<int, MCLQ> { [0] = CreateBridgeableMCLQ() }, (_, _) => 77);
            var bytes = water.Serialize();

            var offsetInstances = BitConverter.ToUInt32(bytes, 0);
            var layerCount = BitConverter.ToUInt32(bytes, 4);
            Assert.AreEqual((uint)1, layerCount);
            Assert.IsTrue(offsetInstances >= HeaderTableSize, $"instance table at {offsetInstances}, inside the {HeaderTableSize}-byte header table");

            var instance = new MH2O(bytes).MH2OHeaders[0].Instances[0];
            Assert.AreEqual((ushort)77, instance.LiquidTypeId);
            Assert.IsTrue(instance.OffsetVertexData >= HeaderTableSize);
            Assert.AreEqual(100.25f, instance.VertexData.HeightMap[0, 0]);
            Assert.AreEqual(180.25f, instance.VertexData.HeightMap[8, 8]);
        }

        [TestMethod]
        public void Serialize_WritesTheSameBytesAsTheMH2OOldWriterItReplacedInTheBridge()
        {
            // Before MH2O parsed its payload, MCLQToMH2O.Convert returned MH2OOld's serialized bytes
            // verbatim. Callers that re-read the bridge through MH2OOld depend on that staying true.
            var bytes = MCLQToMH2O.Convert(new Dictionary<int, MCLQ> { [18] = CreateBridgeableMCLQ() }, (_, _) => 77).Serialize();

#pragma warning disable CS0618 // Compare against Warcraft.NET's existing structured MH2O writer.
            CollectionAssert.AreEqual(new MH2OOld(bytes).Serialize(), bytes);
#pragma warning restore CS0618
        }

        private static MCLQ CreateBridgeableMCLQ()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(10.0f);
                writer.Write(90.0f);
                for (int i = 0; i < 81; i++)
                {
                    writer.Write((byte)i);
                    writer.Write((byte)(i + 1));
                    writer.Write((byte)(i + 2));
                    writer.Write((byte)(i + 3));
                    writer.Write(100.25f + i);
                }

                var tileFlags = new byte[64];
                tileFlags[0] = 0x0F;
                tileFlags[1] = 0x80;
                writer.Write(tileFlags);
                return new MCLQ(stream.ToArray(), MCNKFlags.IsRiver);
            }
        }
    }
}
