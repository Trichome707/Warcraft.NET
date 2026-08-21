using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Warcraft.NET.Files.ADT.Terrain.MCNK;

namespace Warcraft.NET.Tests.Files.ADT.Chunks
{
    [TestClass]
    public class MCRFTests
    {
        [TestMethod]
        public void MCNKReader_ReadsMCRFFromItsOwnOffsetWhenMCLYIsAbsent()
        {
            // MCRF used to be sought at ofsLayer rather than ofsRefs. An MCNK carrying model
            // references but no texture layers therefore seeked to -8, because ofsLayer is 0.
            var mcnk = new Warcraft.NET.Files.ADT.Terrain.Wotlk.MCNK(
                BuildMCNK(includeTextureLayers: false));

            Assert.IsNull(mcnk.TextureLayers);
            Assert.IsNotNull(mcnk.ModelReferences);
            CollectionAssert.AreEqual(new uint[] { 11, 22 }, mcnk.ModelReferences.ModelReferences);
            CollectionAssert.AreEqual(new uint[] { 33 }, mcnk.ModelReferences.WorldObjectReferences);
        }

        [TestMethod]
        public void MCNKReader_ReadsMCRFIdenticallyWhenMCLYIsPresent()
        {
            // The ordinary layout, which the old seek handled only because ReadIFFChunk scans
            // forward from MCLY and lands on MCRF. It must read the same either way.
            var mcnk = new Warcraft.NET.Files.ADT.Terrain.Wotlk.MCNK(
                BuildMCNK(includeTextureLayers: true));

            Assert.IsNotNull(mcnk.TextureLayers);
            Assert.AreEqual(1, mcnk.TextureLayers.Layers.Count);
            Assert.AreEqual((uint)7, mcnk.TextureLayers.Layers[0].TextureID);

            Assert.IsNotNull(mcnk.ModelReferences);
            CollectionAssert.AreEqual(new uint[] { 11, 22 }, mcnk.ModelReferences.ModelReferences);
            CollectionAssert.AreEqual(new uint[] { 33 }, mcnk.ModelReferences.WorldObjectReferences);
        }

        /// <summary>
        /// Builds a minimal MCNK payload holding an MCRF of two model references and one world
        /// model object reference, optionally preceded by a single-entry MCLY.
        /// </summary>
        private static byte[] BuildMCNK(bool includeTextureLayers)
        {
            // Header offsets are relative to the start of the MCNK including its 8-byte IFF
            // envelope, while the payload handed to the reader begins after it.
            const int MCLYEntrySize = 16;
            var header = new Header
            {
                ModelReferenceCount = 2,
                WorldModelObjectReferenceCount = 1
            };

            var mclySize = includeTextureLayers ? 8 + MCLYEntrySize : 0;

            if (includeTextureLayers)
            {
                header.TextureLayersOffset = (uint)(Header.GetSize() + 8);
                header.TextureLayerCount = 1;
            }

            header.ModelReferencesOffset = (uint)(Header.GetSize() + 8 + mclySize);

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(header.Serialize());

                if (includeTextureLayers)
                {
                    writer.Write(ReversedSignature("MCLY"));
                    writer.Write((uint)MCLYEntrySize);
                    writer.Write(7u); // TextureID
                    writer.Write(0u); // Flags
                    writer.Write(0u); // AlphaMapOffset
                    writer.Write(0u); // EffectID
                }

                writer.Write(ReversedSignature("MCRF"));
                writer.Write((uint)12);
                writer.Write(11u);
                writer.Write(22u);
                writer.Write(33u);

                return stream.ToArray();
            }
        }

        /// <summary>Signatures are stored reversed on disk.</summary>
        private static byte[] ReversedSignature(string signature)
        {
            return new[]
            {
                (byte)signature[3], (byte)signature[2], (byte)signature[1], (byte)signature[0]
            };
        }
    }
}
