using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Warcraft.NET.Extensions;
using Warcraft.NET.Files.ADT.Chunks;
using Warcraft.NET.Files.ADT.Conversion;
using Warcraft.NET.Files.ADT.Terrain.MCNK;
using Warcraft.NET.Files.ADT.Terrain.MCNK.Flags;
using Warcraft.NET.Files.ADT.Terrain.MCNK.SubChunks;

namespace Warcraft.NET.Tests.Files.ADT.Chunks
{
    [TestClass]
    public class MCLQTests
    {
        [TestMethod]
        public void LoadBinaryData_ReadsDocumentedMCLQPayload()
        {
            var expected = CreateKnownMCLQPayload();
            var mclq = new MCLQ(expected, MCNKFlags.IsOcean);

            Assert.AreEqual(MCLQ.MinimumSize, expected.Length);
            Assert.AreEqual(10.0f, mclq.MinimumHeight);
            Assert.AreEqual(90.0f, mclq.MaximumHeight);
            Assert.AreEqual(81, mclq.Vertices.Length);
            Assert.AreEqual(64, mclq.TileFlags.Length);
            Assert.AreEqual(MCLQLiquidType.Ocean, mclq.LiquidType);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, mclq.Vertices[0].AuxiliaryData);
            Assert.AreEqual(100.25f, mclq.Vertices[0].Height);
            Assert.AreEqual(180.25f, mclq.Vertices[80].Height);
            CollectionAssert.AreEqual(expected, mclq.Serialize());
        }

        [TestMethod]
        public void LoadBinaryData_RoundTripsRealTbcMCLQPayload()
        {
            // Extracted from C:\Program Files (x86)\Smolderforge\Data\common.MPQ,
            // World\Maps\Azeroth\Azeroth_31_48.adt, MCNK (0, 10). The source MCNK
            // declares LiquidSize = 812; the MCLQ IFF size is zero, so this is its 804-byte
            // payload. SHA-256: 4451c42f934b410748289675bfca458d3695d2f2fdeab17e0f368a56fe9238b0.
            var payload = System.Convert.FromBase64String(RealTbcMclqPayloadFixture);
            var mclq = new MCLQ(payload, MCNKFlags.IsRiver);

            Assert.AreEqual(804, payload.Length);
            Assert.AreEqual(71.62354f, mclq.MinimumHeight);
            Assert.AreEqual(71.62354f, mclq.MaximumHeight);
            Assert.AreEqual(64, mclq.TileFlags.Length);
            Assert.AreEqual(84, mclq.AdditionalData.Length);
            Assert.AreEqual(MCLQLiquidType.Water, mclq.LiquidType);
            CollectionAssert.AreEqual(payload, mclq.Serialize());

            var water = MCLQToMH2O.Convert(new Dictionary<int, MCLQ> { [10] = mclq }, (_, _) => 77);
#pragma warning disable CS0618 // Exercise Warcraft.NET's existing structured MH2O reader.
            var output = new MH2OOld(water.Serialize());
#pragma warning restore CS0618
            var instance = output.MH2OHeaders[10].Instances[0];
            Assert.AreEqual((uint)1, output.MH2OHeaders[10].LayerCount);
            Assert.AreEqual(71.62354f, instance.VertexData.HeightMap[0, 0]);
            Assert.AreEqual(71.62354f, instance.VertexData.HeightMap[8, 8]);
        }

        [TestMethod]
        public void MCNKReader_LoadsRealTbcMCLQWhenItsIffLengthIsZero()
        {
            // This models the original MCNK envelope for the real fixture above: its MCNK
            // header says LiquidOffset=5872 and LiquidSize=812 while its MCLQ IFF length is 0.
            // The relative offset is compacted here, but its zero-length envelope and complete
            // payload are unchanged from Azeroth_31_48.adt, MCNK (0, 10).
            var payload = System.Convert.FromBase64String(RealTbcMclqPayloadFixture);
            var header = new Header
            {
                Flags = MCNKFlags.IsRiver,
                LiquidOffset = (uint)(Header.GetSize() + 8),
                LiquidSize = (uint)(payload.Length + 8)
            };

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(header.Serialize());
                writer.Write(new byte[] { (byte)'Q', (byte)'L', (byte)'C', (byte)'M' });
                writer.Write(0u);
                writer.Write(payload);

                var mcnk = new Warcraft.NET.Files.ADT.Terrain.Wotlk.MCNK(stream.ToArray());
                Assert.IsNotNull(mcnk.LegacyWater);
                Assert.AreEqual(MCLQLiquidType.Water, mcnk.LegacyWater.LiquidType);
                Assert.AreEqual(71.62354f, mcnk.LegacyWater.MinimumHeight);
                Assert.AreEqual(84, mcnk.LegacyWater.AdditionalData.Length);
                CollectionAssert.AreEqual(payload, mcnk.LegacyWater.Serialize());

                var water = MCLQToMH2O.Convert(new Dictionary<int, MCLQ> { [10] = mcnk.LegacyWater }, (_, _) => 77);
#pragma warning disable CS0618 // Exercise Warcraft.NET's existing structured MH2O reader.
                var output = new MH2OOld(water.Serialize());
#pragma warning restore CS0618
                var instance = output.MH2OHeaders[10].Instances[0];
                Assert.AreEqual((uint)1, output.MH2OHeaders[10].LayerCount);
                Assert.AreEqual((ushort)77, instance.LiquidTypeId);
                Assert.AreEqual((byte)8, instance.Width);
                Assert.AreEqual((byte)8, instance.Height);
                Assert.AreEqual(71.62354f, instance.MinHeightLevel);
                Assert.AreEqual(71.62354f, instance.MaxHeightLevel);
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00, 0xC0, 0xC0, 0xE0, 0xF0, 0xF0 }, instance.RenderBitmapBytes);
                Assert.AreEqual((byte)0, instance.VertexData.DepthMap[0, 0]);
                Assert.AreEqual((byte)226, instance.VertexData.DepthMap[8, 8]);
            }
        }

        [TestMethod]
        public void Convert_ProducesAFullChunkMH2OLayerWithTheOriginalHeights()
        {
            var mclq = new MCLQ(CreateKnownMCLQPayload(), MCNKFlags.IsRiver);
            var water = MCLQToMH2O.Convert(new Dictionary<int, MCLQ> { [18] = mclq }, (_, _) => 77);

#pragma warning disable CS0618 // Exercise Warcraft.NET's existing structured MH2O reader.
            var output = new MH2OOld(water.Serialize());
#pragma warning restore CS0618
            var header = output.MH2OHeaders[18];
            Assert.AreEqual((uint)1, header.LayerCount);
            Assert.IsNotNull(header.Attributes);

            var instance = header.Instances[0];
            Assert.AreEqual((ushort)77, instance.LiquidTypeId);
            Assert.AreEqual((ushort)0, instance.LiquidObjectOrVertexFormat);
            Assert.AreEqual(10.0f, instance.MinHeightLevel);
            Assert.AreEqual(90.0f, instance.MaxHeightLevel);
            Assert.AreEqual((byte)0, instance.OffsetX);
            Assert.AreEqual((byte)0, instance.OffsetY);
            Assert.AreEqual((byte)8, instance.Width);
            Assert.AreEqual((byte)8, instance.Height);
            CollectionAssert.AreEqual(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, instance.RenderBitmapBytes);
            Assert.AreEqual((byte)0x02, header.Attributes.Deep[0]);
            Assert.AreEqual(100.25f, instance.VertexData.HeightMap[0, 0]);
            Assert.AreEqual(180.25f, instance.VertexData.HeightMap[8, 8]);
            Assert.AreEqual((byte)0, instance.VertexData.DepthMap[0, 0]);
            Assert.AreEqual((byte)80, instance.VertexData.DepthMap[8, 8]);
        }

        [TestMethod]
        public void MCNKReader_LoadsLegacyMCLQUsingTheContainingHeaderFlags()
        {
            var payload = CreateKnownMCLQPayload();
            var header = new Header
            {
                Flags = MCNKFlags.IsSlime,
                LiquidOffset = (uint)(Header.GetSize() + 8),
                LiquidSize = (uint)(payload.Length + 8)
            };

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(header.Serialize());
                writer.WriteIFFChunk(new MCLQ(payload, header.Flags));

                var mcnk = new Warcraft.NET.Files.ADT.Terrain.Wotlk.MCNK(stream.ToArray());
                Assert.IsNotNull(mcnk.LegacyWater);
                Assert.AreEqual(MCLQLiquidType.Slime, mcnk.LegacyWater.LiquidType);
                Assert.AreEqual(100.25f, mcnk.LegacyWater.Vertices[0].Height);
            }
        }

        private static byte[] CreateKnownMCLQPayload()
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
                tileFlags[0] = 0x0F; // Hidden.
                tileFlags[1] = 0x80; // Visible and deep.
                writer.Write(tileFlags);
                return stream.ToArray();
            }
        }

        private const string RealTbcMclqPayloadFixture =
            "QT+PQkE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAA" +
            "AEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/" +
            "j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IA" +
            "AAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAA" +
            "QT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgcAAABBP49CFgAAAEE/j0IAAAAAQT+P" +
            "QgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQhQAAABBP49CMgAAAEE/j0IAAAAAQT+PQgAA" +
            "AABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IKAAAAQT+PQiYAAABBP49CVwAAAEE/j0IAAAAAQT+PQgAAAABB" +
            "P49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CBwAAAEE/j0IqAAAAQT+PQmEAAABBP49CowAAAEE/j0IAAAAAQT+PQgAAAABBP49C" +
            "AAAAAEE/j0IAAAAAQT+PQgAAAABBP49CHQAAAEE/j0JRAAAAQT+PQpQAAABBP49C4gAAAEE/j0IPDw8PDw8PDw8PDw8PDw8PDw8P" +
            "Dw8PDw8PDw8PDw8EBA8PDw8PDwQEDw8PDw8EBEQPDw8PBARERA8PDw8EBEREAAAAAP//f3///39///9/fwAAAAAAAAAAAAAAAAAA" +
            "AAD//wAA38ABAEAAAAD//39///9/f///f38AAAAAAAAAAAAAAAAAAAAAQAxFADQBAAAAAAAA";

        private const string RealTbcMclqPayload =
            "QT+PQkE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgcAAABBP49CFgAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQhQAAABBP49CMgAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IKAAAAQT+PQiYAAABBP49CVwAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAABBP49CBwAAAEE/j0IqAAAAQT+PQmEAAABBP49CowAAAEE/j0IAAAAAQT+PQgAAAABBP49CAAAAAEE/j0IAAAAAQT+PQgAAAAABP49CHQAAAEE/j0JRAAAAQT+PQpQAAABBP49C4gAAAEE/j0IPDw8PDw8PDw8PDw8PDw8PDw8PDw8PDw8PDw8PDw8EBA8PDw8PDwQEDw8PDw8EBEQPDw8PBARERA8PDw8EBEREAAAAAP//f3///39///9/fwAAAAAAAAAAAAAAAAAAAAD//wAA38ABAEAAAAD//39///9/f///f38AAAAAAAAAAAAAAAAAAAAAQAxFADQBAAAAAAAA";
    }
}
