using System;
using System.IO;
using Warcraft.NET.Files.ADT.Entries;
using Warcraft.NET.Files.Interfaces;

namespace Warcraft.NET.Files.ADT.Chunks
{
    /// <summary>MH2O liquid layers for the 16 by 16 terrain chunks in an ADT.</summary>
    public class MH2O : IIFFChunk, IBinarySerializable
    {
        public const string Signature = "MH2O";

        /// <summary>One header per MCNK, ordered row-major within the ADT.</summary>
        public MH2OHeader[] MH2OHeaders { get; set; } = new MH2OHeader[256];

        public MH2O() { }

        public MH2O(byte[] inData)
        {
            LoadBinaryData(inData);
        }

        public string GetSignature() => Signature;

        public uint GetSize() => (uint)Serialize().Length;

        /// <summary>
        /// Parses the MH2O fixed header table and its offset-addressed layer data. All
        /// offsets are relative to this chunk payload, not to the enclosing ADT file.
        /// </summary>
        public void LoadBinaryData(byte[] inData)
        {
            if (inData == null) throw new ArgumentNullException(nameof(inData));
            const int headerTableSize = 256 * 12;
            if (inData.Length < headerTableSize)
                throw new InvalidDataException($"MH2O is {inData.Length} bytes; its fixed header table requires {headerTableSize} bytes.");

            using var ms = new MemoryStream(inData, writable: false);
            using var br = new BinaryReader(ms);
            for (var i = 0; i < MH2OHeaders.Length; i++)
                MH2OHeaders[i] = new MH2OHeader(br.ReadBytes(MH2OHeader.GetSize()));

            foreach (var header in MH2OHeaders)
            {
                if (header.LayerCount == 0) continue;
                EnsureRange(inData, header.OffsetInstances, checked((int)header.LayerCount * MH2OInstance.GetSize()), "instance table");
                header.Instances = new MH2OInstance[header.LayerCount];
                ms.Position = header.OffsetInstances;
                for (var i = 0; i < header.Instances.Length; i++)
                    header.Instances[i] = new MH2OInstance(br.ReadBytes(MH2OInstance.GetSize()));

                if (header.OffsetAttributes != 0)
                {
                    EnsureRange(inData, header.OffsetAttributes, MH2OAttribute.GetSize(), "attributes");
                    ms.Position = header.OffsetAttributes;
                    header.Attributes = new MH2OAttribute(br.ReadBytes(MH2OAttribute.GetSize()));
                }

                foreach (var instance in header.Instances)
                {
                    var bitmapLength = (instance.Width * instance.Height + 7) / 8;
                    if (instance.OffsetExistsBitmap != 0)
                    {
                        EnsureRange(inData, instance.OffsetExistsBitmap, bitmapLength, "exists bitmap");
                        ms.Position = instance.OffsetExistsBitmap;
                        instance.RenderBitmapBytes = br.ReadBytes(bitmapLength);
                    }

                    if (instance.OffsetVertexData != 0)
                    {
                        var size = MH2OInstanceVertexData.GetSize(instance);
                        EnsureRange(inData, instance.OffsetVertexData, size, "vertex data");
                        ms.Position = instance.OffsetVertexData;
                        instance.VertexData = new MH2OInstanceVertexData(br.ReadBytes(size), instance);
                    }
                }
            }
        }

        public byte[] Serialize(long offset = 0)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            // Reserve the fixed header table and start the offset-addressed data after it.
            // SetLength alone leaves Position at 0, which placed the first layer's instance
            // table over the header table that is written last.
            ms.SetLength(256 * MH2OHeader.GetSize());
            ms.Position = ms.Length;

            foreach (var header in MH2OHeaders)
            {
                if (header?.Instances == null || header.Instances.Length == 0)
                {
                    if (header != null)
                    {
                        header.LayerCount = 0;
                        header.OffsetInstances = 0;
                        header.OffsetAttributes = 0;
                    }
                    continue;
                }

                header.LayerCount = (uint)header.Instances.Length;
                header.OffsetInstances = checked((uint)ms.Position);
                ms.Position += header.Instances.Length * MH2OInstance.GetSize();
            }

            foreach (var header in MH2OHeaders)
            {
                if (header?.Instances == null || header.Instances.Length == 0) continue;
                if (header.Attributes != null)
                {
                    header.OffsetAttributes = checked((uint)ms.Position);
                    bw.Write(header.Attributes.Serialize());
                }
                else header.OffsetAttributes = 0;

                foreach (var instance in header.Instances)
                {
                    var bitmapLength = (instance.Width * instance.Height + 7) / 8;
                    if (instance.RenderBitmapBytes?.Length == bitmapLength)
                    {
                        instance.OffsetExistsBitmap = checked((uint)ms.Position);
                        bw.Write(instance.RenderBitmapBytes);
                    }
                    else instance.OffsetExistsBitmap = 0;

                    if (instance.VertexData != null)
                    {
                        instance.OffsetVertexData = checked((uint)ms.Position);
                        bw.Write(instance.VertexData.Serialize(instance));
                    }
                    else instance.OffsetVertexData = 0;
                }
            }

            foreach (var header in MH2OHeaders)
            {
                if (header?.Instances == null || header.Instances.Length == 0) continue;
                ms.Position = header.OffsetInstances;
                foreach (var instance in header.Instances) bw.Write(instance.Serialize());
            }

            ms.Position = 0;
            for (var i = 0; i < 256; i++)
            {
                var header = MH2OHeaders[i] ?? new MH2OHeader(new byte[MH2OHeader.GetSize()]);
                bw.Write(header.Serialize());
            }
            return ms.ToArray();
        }

        private static void EnsureRange(byte[] data, uint offset, int length, string description)
        {
            if (offset > data.Length || length < 0 || length > data.Length - offset)
                throw new InvalidDataException($"MH2O {description} points outside its payload (offset {offset}, length {length}, payload {data.Length}).");
        }
    }
}
