using System;
using System.IO;
using System.Numerics;
using Warcraft.NET.Files.WMO.WorldMapObject.MOGP;

namespace Warcraft.NET.Files.WMO.WorldMapObject.Wotlk
{
    /// <summary>Collision-relevant geometry from one pre-Cataclysm numbered WMO group file.</summary>
    public sealed class WorldMapObjectGroupGeometry
    {
        private const uint Mogp = 0b_01001101_01001111_01000111_01010000;
        private const uint Mopy = 0b_01001101_01001111_01010000_01011001;
        private const uint Movi = 0b_01001101_01001111_01010110_01001001;
        private const uint Movt = 0b_01001101_01001111_01010110_01010100;

        /// <summary>The fixed header carried by the enclosing MOGP chunk.</summary>
        public Header Header { get; private set; }
        /// <summary>The declared byte count of the enclosing MOGP chunk payload.</summary>
        public uint MogpChunkSize { get; private set; }
        public Vector3[] Vertices { get; private set; } = Array.Empty<Vector3>();
        public ushort[] Indices { get; private set; } = Array.Empty<ushort>();
        /// <summary>One raw MOPY flag/material word per triangle.</summary>
        public ushort[] TriangleMaterials { get; private set; } = Array.Empty<ushort>();

        public WorldMapObjectGroupGeometry(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using var stream = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(stream);
            bool foundMogp = false;
            while (stream.Position + 8 <= stream.Length)
            {
                uint tag = reader.ReadUInt32();
                uint size = reader.ReadUInt32();
                if (size > stream.Length - stream.Position)
                    throw new InvalidDataException("WMO group chunk extends beyond the input data.");
                long payloadStart = stream.Position;
                if (tag != Mogp)
                {
                    stream.Position = payloadStart + size;
                    continue;
                }

                if (foundMogp)
                    throw new InvalidDataException("WMO group contains more than one MOGP wrapper.");
                if (size < Header.GetSize())
                    throw new InvalidDataException("WMO MOGP chunk is smaller than its fixed header.");

                foundMogp = true;
                MogpChunkSize = size;
                Header = new Header(reader.ReadBytes(Header.GetSize()));
                ReadNestedChunks(reader, payloadStart + size);
                stream.Position = payloadStart + size;
            }

            if (!foundMogp)
                throw new InvalidDataException("WMO group does not contain an MOGP wrapper.");
            if (Indices.Length % 3 != 0 || TriangleMaterials.Length != Indices.Length / 3)
                throw new InvalidDataException("WMO MOVI/MOPY triangle counts disagree.");
        }

        private void ReadNestedChunks(BinaryReader reader, long end)
        {
            while (reader.BaseStream.Position + 8 <= end)
            {
                uint tag = reader.ReadUInt32();
                uint size = reader.ReadUInt32();
                if (size > end - reader.BaseStream.Position)
                    throw new InvalidDataException("Nested WMO group chunk extends beyond MOGP.");

                long next = reader.BaseStream.Position + size;
                if (tag == Movt)
                {
                    if (size % 12 != 0) throw new InvalidDataException("MOVT size is not a multiple of 12.");
                    Vertices = new Vector3[size / 12];
                    for (int i = 0; i < Vertices.Length; i++) Vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
                else if (tag == Movi)
                {
                    if (size % 2 != 0) throw new InvalidDataException("MOVI size is not a multiple of 2.");
                    Indices = new ushort[size / 2];
                    for (int i = 0; i < Indices.Length; i++) Indices[i] = reader.ReadUInt16();
                }
                else if (tag == Mopy)
                {
                    if (size % 2 != 0) throw new InvalidDataException("MOPY size is not a multiple of 2.");
                    TriangleMaterials = new ushort[size / 2];
                    for (int i = 0; i < TriangleMaterials.Length; i++) TriangleMaterials[i] = reader.ReadUInt16();
                }
                reader.BaseStream.Position = next;
            }

            if (reader.BaseStream.Position != end)
                throw new InvalidDataException("MOGP nested chunks do not end on a chunk boundary.");
        }
    }
}
