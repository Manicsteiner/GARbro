using GameRes.Compression;
using GameRes.Formats.Primel;
using GameRes.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

namespace GameRes.Formats.Kid
{
    [Export(typeof(ImageFormat))]
    public class KlzFormat: DigitalWorks.Tim2Format
    {
        public override string Tag { get { return "KLZ/KID PS2 compressed TIM2"; } }
        public override string Description { get { return "KID PS2 compressed TIM2 image format"; } }
        public override uint Signature { get { return 0; } } //KLZ
        public KlzFormat()
        {
            Extensions = new string[] { "klz" };
        }

        public override ImageMetaData ReadMetaData(IBinaryStream stream)
        {
            uint unpacked_size = Binary.BigEndian(stream.Signature);
            if (unpacked_size <= 0x20 || unpacked_size > 0x5000000) // ~83MB
                return null;
            stream.Position = 0;
            //Stream streamdec = LzsStreamDecode(stream);
            //using (var lzss = new LzssStream(stream.AsStream, LzssMode.Decompress, true))
            using (var input = new SeekableStream(LzsStreamDecode(stream)))
            using (var tm2 = new BinaryStream(input, stream.Name))
                return base.ReadMetaData(tm2);
        }
        public override ImageData Read(IBinaryStream stream, ImageMetaData info)
        {
            //stream.Position = 4;
            //using (var lzss = new LzssStream(stream.AsStream, LzssMode.Decompress, true))
            using (var input = new SeekableStream(LzsStreamDecode(stream)))
            using (var tm2 = new BinaryStream(input, stream.Name))
                return base.Read(tm2, info);
        }
        public override void Write(Stream file, ImageData image)
        {
            throw new System.NotImplementedException("KlzFormat.Write not implemented");
        }

        public static Stream LzsStreamDecode(IBinaryStream input) {
            List<byte> out_bytes = new List<byte>();
            List<byte> f_out_bytes = new List<byte>();
            uint output_size = Binary.BigEndian(input.ReadUInt32());
            ushort fill_count = Binary.BigEndian(input.ReadUInt16());
            int at = 0, v0, v1, a0, s0 = 0, s1 = 0, s2 = 0, s3 = 0;
            int OO40_sp = 0, OO42_sp = 0, OO44_sp, OO48_sp, OO50_sp = 0, OO60_sp = 0, OO70_sp = fill_count;
            int next_read_pos = 0;
            int count = 0;
            int num_passes = 0;
            byte[] decode_table = new byte[] { 
                0x01, 0x02, 0x04, 0x08,
                0x10, 0x20, 0x40, 0x80,
                // Only first 8 are used
                0x81, 0x75, 0x81, 0x69,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x02, 0x00, 0x00
            };
            // out.resize(0x4000)
            int temp = 0x4000;
            while (temp > 0)
            {
                out_bytes.Add(0);
                temp--;
            }
            // out.resize(0x4000) end
            if (fill_count > 0x4000)
            {
                next_read_pos = 4;
                while (OO70_sp > 0x4000)
                {
                    int cnt = 0;
                    int copy_off = next_read_pos + 2;
                    while (cnt < 0x4000)
                    {
                        input.Position = copy_off;
                        f_out_bytes.Add(input.ReadUInt8());
                        cnt++;
                        copy_off++;
                    }
                    count += 0x4000;
                    next_read_pos += cnt + 2;
                    num_passes++;
                    input.Position = next_read_pos;
                    OO70_sp = Binary.BigEndian(input.ReadUInt16());
                    if (count >= output_size || next_read_pos >= input.Length)
                    {
                        Stream stream = new MemoryStream(f_out_bytes.ToArray());
                        return stream;
                    }
                    OO60_sp = next_read_pos + 2;
                }
            }
            else
            {
                OO60_sp = 6;
            }

            OO44_sp = OO60_sp;
            v0 = OO60_sp + 1;
            OO48_sp = v0;
            while (true){
                input.Position = OO44_sp;
                //input.Seek(OO44_sp, SeekOrigin.Begin);
                v0 = input.ReadUInt8();
                a0 = v0 & 0xFF;
                v0 = OO40_sp;
                v1 = v0 & 0xFF;
                v0 = decode_table[v1] & 0xFF;
                v0 &= a0;
                // #001BA8AC
                if (v0 == 0)
                {
                    input.Position = OO48_sp;
                    //input.Seek(OO48_sp, SeekOrigin.Begin);
                    v1 = input.ReadUInt8();
                    v0 = OO50_sp + s0;
                    out_bytes.RemoveAt(v0);
                    out_bytes.Insert(v0, Convert.ToByte(v1));
                    OO48_sp++;
                    OO42_sp++;
                    s0++;
                }
                else if (v0 != 0) {
                    // # 001BA8F0
                    OO42_sp += 2;
                    input.Position = OO48_sp;
                    // input.Seek(OO48_sp, SeekOrigin.Begin);
                    v0 = input.ReadUInt8() & 0xFF;
                    v1 = v0 << 8;
                    v0 = input.ReadUInt8() & 0xFF;
                    v0 = v1 | v0;
                    v0 &= 0xFFFF;
                    s2 = v0 & 0xFFFF;
                    v0 = s2 & 0xFFFF;
                    v0 &= 0x1F;
                    v0 += 2;
                    v0 &= 0xFFFF;
                    s3 = v0 & 0xFFFF;
                    v0 = s2 & 0xFFFF;
                    v0 >>= 5;
                    v0 &= 0xFFFF;
                    s1 = v0 & 0xFFFF;
                    v0 = s1 & 0xFFFF;
                    v0 = s0 - v0;
                    v0 -= 1;
                    v0 &= 0xFFFF;
                    s1 = v0 & 0xFFFF;
                    OO48_sp += 1;
                    v0 = 1;
                    while (v0 != 0)
                    {
                        at = s0 < 0x0800 ? 1 : 0;
                        // # 001BA96C
                        if (at != 0)
                        {
                            v0 = s1 & 0xFFFF;
                            at = s0 < v0 ? 1 : 0;
                            if (at != 0)
                            {
                                v1 = out_bytes[OO50_sp];
                                v0 = OO50_sp + s0;
                                out_bytes.RemoveAt(v0);
                                out_bytes.Insert(v0, Convert.ToByte(v1));
                                s0 += 1;
                                v0 = s1 + 1;
                                s1 = v0 & 0xFFFF;
                                // # 001BA9D8
                                v1 = s3;
                                v0 = v1 - 1;
                                s3 = v0 & 0xFFFF;
                                v0 = v1 & 0xFFFF;
                                continue;
                            }
                        }
                        // # 001BA9B0
                        v1 = s1 & 0xFFFF;
                        v0 = OO50_sp;
                        v0 += v1;
                        v1 = out_bytes[v0];
                        v0 = OO50_sp;
                        v0 += s0;
                        out_bytes.RemoveAt(v0);
                        out_bytes.Insert(v0, Convert.ToByte(v1));
                        s0 += 1;
                        v0 = s1 + 1;
                        s1 = v0 & 0xFFFF;
                        // # 001BA9D8
                        v1 = s3;
                        v0 = v1 - 1;
                        s3 = v0 & 0xFFFF;
                        v0 = v1 & 0xFFFF;
                    }
                    OO48_sp += 1;
                }
                // # 001BAA00
                OO40_sp += 1;
                v1 = OO40_sp & 0xFF;
                v0 = 8;
                if (v1 == v0)
                {
                    OO40_sp = 0;
                    OO44_sp = OO48_sp;
                    OO48_sp += 1;
                    OO42_sp += 1;
                }
                v0 = OO42_sp;
                v1 = v0 & 0xFFFF;
                v0 = OO70_sp;
                v0 -= 1;
                v0 = v1 < v0 ? 1 : 0;
                if (v0 == 0)
                {
                    count += s0;
                    if (count >= output_size || next_read_pos >= input.Length)
                    {
                        f_out_bytes.AddRange(out_bytes);
                        Stream stream = new MemoryStream(f_out_bytes.ToArray());
                        return stream;
                    }
                    num_passes += 1;
                    if (num_passes == 1)
                    {
                        next_read_pos += OO70_sp + 6;
                    }
                    else
                    {
                        next_read_pos += OO70_sp + 2;
                    }

                    f_out_bytes.AddRange(out_bytes);
                    // # out.fill(0);
                    input.Position = next_read_pos;
                    OO70_sp = Binary.BigEndian(input.ReadUInt16());
                    if (OO70_sp > 0x4000)
                    {
                        while (OO70_sp > 0x4000)
                        {
                            int cnt = 0;
                            int copy_off = next_read_pos + 2;
                            while (cnt < 0x4000)
                            {
                                input.Position = copy_off;
                                f_out_bytes.Add(input.ReadUInt8());
                                cnt += 1;
                                copy_off += 1;
                            }
                            count += 0x4000;
                            next_read_pos += cnt + 2;
                            input.Position = next_read_pos;
                            OO70_sp = Binary.BigEndian(input.ReadUInt16());
                            if (count >= output_size || next_read_pos >= input.Length)
                            {
                                Stream stream = new MemoryStream(f_out_bytes.ToArray());
                                return stream;
                            }
                        }
                    }
                    s0 = 0;
                    s1 = 0;
                    OO50_sp = 0;
                    OO42_sp = 0;
                    OO48_sp = next_read_pos + 2;
                    if (OO48_sp > input.Length) {
                        Stream stream = new MemoryStream(f_out_bytes.ToArray());
                        return stream;
                    }
                    OO40_sp = 0;
                    OO60_sp = OO48_sp;
                    OO44_sp = OO60_sp;
                    v0 = OO60_sp + 1;
                    OO48_sp = v0;
                }
            }
            Stream stream_out = new MemoryStream(f_out_bytes.ToArray());
            return stream_out;
        }
    }
}
