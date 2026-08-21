using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Warcraft.NET.Files.ADT.Terrain.MCNK;
using Warcraft.NET.Files.ADT.Terrain.MCNK.Flags;

namespace Warcraft.NET.Tests.Files.ADT.Chunks
{
    [TestClass]
    public class MCSHTests
    {
        [TestMethod]
        public void MCNKReader_ParsesMCSHAsAbsentWhenTheSubChunkIsMissing()
        {
            // MCSH is optional. Many Outland ADTs bake no shadows and omit the subchunk while
            // ofsShadow still points into the MCNK, so the reader must treat a missing MCSH as
            // absent rather than throwing ChunkSignatureNotFoundException.
            var mcnk = new Warcraft.NET.Files.ADT.Terrain.Wotlk.MCNK(
                BuildMCNK(shadowMap: null, trailingChunk: "MCAL"));

            Assert.IsNull(mcnk.BakedShadows);
            Assert.IsFalse(mcnk.Header.Flags.HasFlag(MCNKFlags.HasBakedShadows));
        }

        [TestMethod]
        public void MCNKReader_StillReadsMCSHWhenTheSubChunkIsPresent()
        {
            var shadowMap = new byte[512];
            for (int i = 0; i < shadowMap.Length; i++)
            {
                shadowMap[i] = (byte)(i & 0xFF);
            }

            var mcnk = new Warcraft.NET.Files.ADT.Terrain.Wotlk.MCNK(
                BuildMCNK(shadowMap, trailingChunk: null));

            Assert.IsNotNull(mcnk.BakedShadows);
            CollectionAssert.AreEqual(shadowMap, mcnk.BakedShadows.ShadowMap);
            Assert.IsTrue(mcnk.Header.Flags.HasFlag(MCNKFlags.HasBakedShadows));
        }

        /// <summary>
        /// Builds a minimal MCNK payload whose header addresses a single subchunk via
        /// ofsShadow: the MCSH itself when <paramref name="shadowMap"/> is supplied, or an
        /// unrelated chunk when it is not.
        /// </summary>
        private static byte[] BuildMCNK(byte[] shadowMap, string trailingChunk)
        {
            // Header offsets are relative to the start of the MCNK including its 8-byte
            // IFF envelope, while the payload handed to the reader begins after it.
            var header = new Header
            {
                BakedShadowsOffset = (uint)(Header.GetSize() + 8)
            };

            if (shadowMap != null)
            {
                header.BakedShadowsSize = (uint)(shadowMap.Length + 8);
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(header.Serialize());

                // Signatures are stored reversed on disk.
                var signature = shadowMap != null ? "MCSH" : trailingChunk;
                writer.Write(new[]
                {
                    (byte)signature[3], (byte)signature[2], (byte)signature[1], (byte)signature[0]
                });

                writer.Write((uint)(shadowMap?.Length ?? 0));

                if (shadowMap != null)
                {
                    writer.Write(shadowMap);
                }

                return stream.ToArray();
            }
        }
    }
}
