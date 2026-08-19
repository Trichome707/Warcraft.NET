using System;
using System.Collections.Generic;
using Warcraft.NET.Files.ADT.Chunks;
using Warcraft.NET.Files.ADT.Entries;
using Warcraft.NET.Files.ADT.Terrain.MCNK.SubChunks;

namespace Warcraft.NET.Files.ADT.Conversion
{
    /// <summary>
    /// Materializes legacy, full-MCNK MCLQ liquid geometry as an MH2O chunk.
    /// </summary>
    public static class MCLQToMH2O
    {
        /// <summary>
        /// Converts the supplied legacy liquid chunks to one MH2O chunk.
        /// </summary>
        /// <param name="mclqByChunkIndex">MCLQ data keyed by MCNK index (0 through 255).</param>
        /// <param name="targetLiquidTypeId">Resolves a target-client LiquidType.dbc ID for each legacy MCLQ.</param>
        /// <returns>An MH2O raw chunk suitable for an ADT terrain root.</returns>
        public static MH2O Convert(IReadOnlyDictionary<int, MCLQ> mclqByChunkIndex, Func<int, MCLQ, ushort> targetLiquidTypeId)
        {
            if (mclqByChunkIndex == null)
                throw new ArgumentNullException(nameof(mclqByChunkIndex));

            if (targetLiquidTypeId == null)
                throw new ArgumentNullException(nameof(targetLiquidTypeId));

#pragma warning disable CS0618 // Warcraft.NET's existing structured MH2O serializer.
            var mh2o = new MH2OOld
            {
                MH2OHeaders = CreateHeaders()
            };
#pragma warning restore CS0618

            foreach (var entry in mclqByChunkIndex)
            {
                if (entry.Key < 0 || entry.Key >= mh2o.MH2OHeaders.Length)
                    throw new ArgumentOutOfRangeException(nameof(mclqByChunkIndex), "MCNK indices must be between 0 and 255.");

                if (entry.Value == null)
                    throw new ArgumentException("MCLQ values cannot be null.", nameof(mclqByChunkIndex));

                var converted = CreateLayer(entry.Value, targetLiquidTypeId(entry.Key, entry.Value));
                mh2o.MH2OHeaders[entry.Key].Instances = new[] { converted.Instance };
                mh2o.MH2OHeaders[entry.Key].Attributes = converted.Attributes.HasOnlyZeroes ? null : converted.Attributes;
            }

            return new MH2O(mh2o.Serialize());
        }

        private static MH2OHeader[] CreateHeaders()
        {
            var headers = new MH2OHeader[256];
            for (int i = 0; i < headers.Length; i++)
                headers[i] = new MH2OHeader(new byte[MH2OHeader.GetSize()]);

            return headers;
        }

        private static MCLQToMH2OLayer CreateLayer(MCLQ mclq, ushort liquidTypeId)
        {
            if (mclq.Vertices == null || mclq.Vertices.Length != MCLQ.VertexGridWidth * MCLQ.VertexGridHeight)
                throw new ArgumentException("MCLQ must contain a complete 9 by 9 vertex grid.", nameof(mclq));

            if (mclq.TileFlags == null || mclq.TileFlags.Length != MCLQ.TileGridWidth * MCLQ.TileGridHeight)
                throw new ArgumentException("MCLQ must contain a complete 8 by 8 tile grid.", nameof(mclq));

            var heightMap = new float[MCLQ.VertexGridHeight, MCLQ.VertexGridWidth];
            var depthMap = new byte[MCLQ.VertexGridHeight, MCLQ.VertexGridWidth];
            for (int y = 0; y < MCLQ.VertexGridHeight; y++)
            {
                for (int x = 0; x < MCLQ.VertexGridWidth; x++)
                {
                    var vertex = mclq.Vertices[(y * MCLQ.VertexGridWidth) + x];
                    if (vertex == null || vertex.AuxiliaryData == null || vertex.AuxiliaryData.Length != sizeof(uint))
                        throw new ArgumentException("MCLQ contains an invalid vertex.", nameof(mclq));

                    heightMap[y, x] = vertex.Height;
                    depthMap[y, x] = vertex.AuxiliaryData[0];
                }
            }

            var renderBitmap = new byte[8];
            var attributes = new MH2OAttribute(new byte[MH2OAttribute.GetSize()]);
            for (int y = 0; y < MCLQ.TileGridHeight; y++)
            {
                for (int x = 0; x < MCLQ.TileGridWidth; x++)
                {
                    var flag = mclq.TileFlags[(y * MCLQ.TileGridWidth) + x];
                    var bitIndex = (y * MCLQ.TileGridWidth) + x;
                    if (flag != 0x0F)
                        SetBit(renderBitmap, bitIndex);

                    if ((flag & 0x80) != 0)
                        SetBit(attributes.Deep, bitIndex);
                }
            }

            return new MCLQToMH2OLayer
            {
                Instance = new MH2OInstance(new byte[MH2OInstance.GetSize()])
                {
                    LiquidTypeId = liquidTypeId,
                    LiquidObjectOrVertexFormat = 0,
                    MinHeightLevel = mclq.MinimumHeight,
                    MaxHeightLevel = mclq.MaximumHeight,
                    OffsetX = 0,
                    OffsetY = 0,
                    Width = MCLQ.TileGridWidth,
                    Height = MCLQ.TileGridHeight,
                    RenderBitmapBytes = renderBitmap,
                    VertexData = CreateVertexData(heightMap, depthMap)
                },
                Attributes = attributes
            };
        }

        private static void SetBit(byte[] bitmap, int bitIndex)
        {
            bitmap[bitIndex / 8] |= (byte)(1 << (bitIndex % 8));
        }

        private static MH2OInstanceVertexData CreateVertexData(float[,] heightMap, byte[,] depthMap)
        {
            // Use a LiquidObject/VertexFormat value the existing reader treats as an external
            // liquid object so its constructor does not consume data before we assign the maps.
            var descriptor = new byte[MH2OInstance.GetSize()];
            descriptor[2] = 42;
            return new MH2OInstanceVertexData(Array.Empty<byte>(), new MH2OInstance(descriptor))
            {
                HeightMap = heightMap,
                DepthMap = depthMap
            };
        }

        private class MCLQToMH2OLayer
        {
            public MH2OInstance Instance { get; set; }

            public MH2OAttribute Attributes { get; set; }
        }
    }
}
