using Warcraft.NET.Files.Interfaces;
using System.IO;
using Warcraft.NET.Files.ADT.Terrain.MCNK.SubChunks;
using Warcraft.NET.Extensions;
using Warcraft.NET.Exceptions;
using Warcraft.NET.Files.ADT.Terrain.MCNK;

namespace Warcraft.NET.Files.ADT.Terrain
{
    /// <summary>
    /// MCNK
    /// </summary>
    public abstract class MCNKBase : IIFFChunk, IBinarySerializable
    {
        /// <summary>
        /// Holds the binary chunk signature.
        /// </summary>
        public const string Signature = "MCNK";

        /// <summary>
        /// Gets or sets the header, which contains information about the MCNK and its subchunks such as offsets,
        /// position and flags.
        /// </summary>
        public Header Header { get; set; } = new();

        /// <summary>
        /// Gets or sets the heightmap chunk.
        /// </summary>
        public MCVT Heightmap { get; set; }

        /// <summary>
        /// Gets or sets the vertex shading chunk.
        /// </summary>
        public MCCV VertexShading { get; set; }

        /// <summary>
        /// Gets or sets the normal map chunk.
        /// </summary>
        public MCNR VertexNormals { get; set; }

        /// <summary>
        /// Gets or sets the sound emitters chunk.
        /// </summary>
        public MCSE SoundEmitters { get; set; }

        /// <summary>
        /// Gets or sets the legacy pre-WotLK liquid data addressed by this MCNK header.
        /// </summary>
        public MCLQ LegacyWater { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MCNKBase"/> class.
        /// </summary>
        public MCNKBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MCNKBase"/> class.
        /// </summary>
        /// <param name="inData">ExtendedData.</param>
        public MCNKBase(byte[] inData)
        {
            LoadBinaryData(inData);
        }

        /// <inheritdoc/>
        public virtual void LoadBinaryData(byte[] inData)
        {
            using (var ms = new MemoryStream(inData))
            using (var br = new BinaryReader(ms))
            {
                Header = new Header(br.ReadBytes(Header.GetSize()));
                long headerEndPositon = Header.GetSize();

                // Read MCVT
                try
                {
                    ms.Seek(headerEndPositon, SeekOrigin.Begin);
                    Heightmap = br.ReadIFFChunk<MCVT>(false, false);
                }
                catch (ChunkSignatureNotFoundException)
                {
                    // Ignore missing chunks
                }

                // Read MCCV
                try
                {
                    ms.Seek(headerEndPositon, SeekOrigin.Begin);
                    VertexShading = br.ReadIFFChunk<MCCV>(false, false);
                } catch (ChunkSignatureNotFoundException)
                {
                    // Ignore missing chunks
                }

                // Read MCNR
                try
                {
                    ms.Seek(headerEndPositon, SeekOrigin.Begin);
                    VertexNormals = br.ReadIFFChunk<MCNR>(false, false);
                }
                catch (ChunkSignatureNotFoundException)
                {
                    // Ignore missing chunks
                }

                // Read MCSE
                try
                {
                    ms.Seek(headerEndPositon, SeekOrigin.Begin);
                    SoundEmitters = br.ReadIFFChunk<MCSE>(false, false);
                } catch (ChunkSignatureNotFoundException)
                {
                    // Ignore missing chunks
                }

                // MCLQ is a legacy subchunk addressed by the MCNK header rather than a
                // sequential subchunk. Legacy ADTs can write zero (or otherwise unreliable)
                // IFF MCLQ lengths, so Header.LiquidSize bounds the payload. A size of 8
                // denotes the newer top-level MH2O path.
                if (Header.LiquidOffset > 0 && Header.LiquidSize > 8)
                {
                    var mclqOffset = Header.LiquidOffset - 8;
                    var mclqPayloadSize = Header.LiquidSize - 8;
                    if (mclqOffset <= ms.Length - 8 && mclqPayloadSize <= ms.Length - mclqOffset - 8)
                    {
                        ms.Seek(mclqOffset, SeekOrigin.Begin);
                        var signature = br.ReadBinarySignature();
                        _ = br.ReadUInt32(); // Legacy clients may set this IFF length to zero.

                        if (signature == MCLQ.Signature)
                            LegacyWater = new MCLQ(br.ReadBytes((int)mclqPayloadSize), Header.Flags);
                    }
                }
            }
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
        public abstract byte[] Serialize(long offset = 0);
    }
}
