using System;
using System.IO;
using Warcraft.NET.Files.ADT.Terrain.MCNK.Flags;
using Warcraft.NET.Files.Interfaces;

namespace Warcraft.NET.Files.ADT.Terrain.MCNK.SubChunks
{
    /// <summary>
    /// The legacy, pre-WotLK liquid data contained by an MCNK chunk.
    /// </summary>
    public class MCLQ : IIFFChunk, IBinarySerializable
    {
        /// <summary>
        /// Holds the binary chunk signature.
        /// </summary>
        public const string Signature = "MCLQ";

        /// <summary>
        /// The fixed width of the liquid vertex grid.
        /// </summary>
        public const int VertexGridWidth = 9;

        /// <summary>
        /// The fixed height of the liquid vertex grid.
        /// </summary>
        public const int VertexGridHeight = 9;

        /// <summary>
        /// The fixed width of the liquid tile grid.
        /// </summary>
        public const int TileGridWidth = 8;

        /// <summary>
        /// The fixed height of the liquid tile grid.
        /// </summary>
        public const int TileGridHeight = 8;

        /// <summary>
        /// The number of bytes required by the documented MCLQ payload.
        /// </summary>
        public const int MinimumSize = (sizeof(float) * 2) + (VertexGridWidth * VertexGridHeight * MCLQVertex.Size) + (TileGridWidth * TileGridHeight);

        /// <summary>
        /// Gets or sets the minimum liquid surface height.
        /// </summary>
        public float MinimumHeight { get; set; }

        /// <summary>
        /// Gets or sets the maximum liquid surface height.
        /// </summary>
        public float MaximumHeight { get; set; }

        /// <summary>
        /// Gets the 81 liquid vertices in row-major order.
        /// </summary>
        public MCLQVertex[] Vertices { get; set; } = new MCLQVertex[VertexGridWidth * VertexGridHeight];

        /// <summary>
        /// Gets the 64 liquid tile flags in row-major order.
        /// </summary>
        public byte[] TileFlags { get; set; } = new byte[TileGridWidth * TileGridHeight];

        /// <summary>
        /// Gets the liquid type selected by the containing MCNK's flags.
        /// </summary>
        public MCLQLiquidType LiquidType { get; private set; }

        /// <summary>
        /// Gets or sets bytes following the documented MCLQ payload.
        /// </summary>
        public byte[] AdditionalData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Initializes a new empty MCLQ chunk.
        /// </summary>
        public MCLQ()
        {
        }

        /// <summary>
        /// Initializes a new MCLQ chunk using the supplied MCNK flags to select the liquid type.
        /// </summary>
        /// <param name="inData">The MCLQ payload, without its IFF signature and size.</param>
        /// <param name="mcnkFlags">Flags from the containing MCNK header.</param>
        public MCLQ(byte[] inData, MCNKFlags mcnkFlags)
        {
            LoadBinaryData(inData, mcnkFlags);
        }

        /// <inheritdoc/>
        public string GetSignature()
        {
            return Signature;
        }

        /// <inheritdoc/>
        public uint GetSize()
        {
            return (uint)Serialize().Length;
        }

        /// <inheritdoc/>
        public void LoadBinaryData(byte[] inData)
        {
            LoadBinaryData(inData, (MCNKFlags)0);
        }

        /// <summary>
        /// Loads MCLQ data using the flags from its containing MCNK header.
        /// </summary>
        /// <param name="inData">The MCLQ payload, without its IFF signature and size.</param>
        /// <param name="mcnkFlags">Flags from the containing MCNK header.</param>
        public void LoadBinaryData(byte[] inData, MCNKFlags mcnkFlags)
        {
            if (inData == null)
                throw new ArgumentNullException(nameof(inData));

            if (inData.Length < MinimumSize)
                throw new ArgumentException($"MCLQ data must be at least {MinimumSize} bytes.", nameof(inData));

            using (var ms = new MemoryStream(inData, false))
            using (var br = new BinaryReader(ms))
            {
                MinimumHeight = br.ReadSingle();
                MaximumHeight = br.ReadSingle();

                Vertices = new MCLQVertex[VertexGridWidth * VertexGridHeight];
                for (int i = 0; i < Vertices.Length; i++)
                    Vertices[i] = new MCLQVertex(br.ReadBytes(MCLQVertex.Size));

                TileFlags = br.ReadBytes(TileGridWidth * TileGridHeight);
                AdditionalData = br.ReadBytes((int)(ms.Length - ms.Position));
            }

            LiquidType = GetLiquidType(mcnkFlags);
        }

        /// <inheritdoc/>
        public byte[] Serialize(long offset = 0)
        {
            if (Vertices == null || Vertices.Length != VertexGridWidth * VertexGridHeight)
                throw new InvalidOperationException($"MCLQ must contain exactly {VertexGridWidth * VertexGridHeight} vertices.");

            if (TileFlags == null || TileFlags.Length != TileGridWidth * TileGridHeight)
                throw new InvalidOperationException($"MCLQ must contain exactly {TileGridWidth * TileGridHeight} tile flags.");

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(MinimumHeight);
                bw.Write(MaximumHeight);

                foreach (var vertex in Vertices)
                {
                    if (vertex == null)
                        throw new InvalidOperationException("MCLQ vertices cannot contain null values.");

                    bw.Write(vertex.Serialize());
                }

                bw.Write(TileFlags);
                if (AdditionalData != null)
                    bw.Write(AdditionalData);

                return ms.ToArray();
            }
        }

        private static MCLQLiquidType GetLiquidType(MCNKFlags flags)
        {
            if (flags.HasFlag(MCNKFlags.IsOcean))
                return MCLQLiquidType.Ocean;

            if (flags.HasFlag(MCNKFlags.IsMagma))
                return MCLQLiquidType.Magma;

            if (flags.HasFlag(MCNKFlags.IsSlime))
                return MCLQLiquidType.Slime;

            return MCLQLiquidType.Water;
        }
    }

    /// <summary>
    /// Legacy liquid categories selected by MCNK flags.
    /// </summary>
    public enum MCLQLiquidType
    {
        /// <summary>Water or river.</summary>
        Water,

        /// <summary>Ocean.</summary>
        Ocean,

        /// <summary>Magma or lava.</summary>
        Magma,

        /// <summary>Slime.</summary>
        Slime
    }

    /// <summary>
    /// An eight-byte MCLQ vertex.
    /// </summary>
    public class MCLQVertex
    {
        /// <summary>
        /// The size of a serialized MCLQ vertex.
        /// </summary>
        public const int Size = 8;

        /// <summary>
        /// Gets or sets the type-dependent four-byte prefix. For water, ocean, and slime this is
        /// depth/flow/filler; for magma it is two little-endian UV values.
        /// </summary>
        public byte[] AuxiliaryData { get; set; } = new byte[sizeof(uint)];

        /// <summary>
        /// Gets or sets the absolute liquid-surface height.
        /// </summary>
        public float Height { get; set; }

        /// <summary>
        /// Initializes a new empty MCLQ vertex.
        /// </summary>
        public MCLQVertex()
        {
        }

        /// <summary>
        /// Initializes an MCLQ vertex from its eight-byte binary representation.
        /// </summary>
        /// <param name="inData">Vertex data.</param>
        public MCLQVertex(byte[] inData)
        {
            if (inData == null)
                throw new ArgumentNullException(nameof(inData));

            if (inData.Length != Size)
                throw new ArgumentException($"MCLQ vertices must be exactly {Size} bytes.", nameof(inData));

            using (var ms = new MemoryStream(inData, false))
            using (var br = new BinaryReader(ms))
            {
                AuxiliaryData = br.ReadBytes(sizeof(uint));
                Height = br.ReadSingle();
            }
        }

        /// <summary>
        /// Serializes the vertex.
        /// </summary>
        /// <returns>The eight-byte binary representation.</returns>
        public byte[] Serialize()
        {
            if (AuxiliaryData == null || AuxiliaryData.Length != sizeof(uint))
                throw new InvalidOperationException("MCLQ vertex auxiliary data must be exactly four bytes.");

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(AuxiliaryData);
                bw.Write(Height);
                return ms.ToArray();
            }
        }
    }
}
