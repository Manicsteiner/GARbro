using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameRes.Formats.Kid
{
    [Export(typeof(ImageFormat))]
    public class CpsFormat : ImageFormat
    {
        public override string Tag { get { return "CPS"; } }
        public override string Description { get { return "KID & MAGES PS2 compressed BMP"; } }
        public override uint Signature { get { return 0x535043; } } // 'CPS'
        public CpsFormat()
        {
            Extensions = new string[] { "cps" };
        }
        public override ImageMetaData ReadMetaData(IBinaryStream stream)
        {
            var header = stream.ReadHeader(0x1C);

            bool need_key;
            if (header[0x08] == 0x66)
                need_key = false;
            else if (header[0x08] == 0x68)
                need_key = true;
            else
                return null;

            uint unpacked_size = header.ToUInt32(0x0C);
            if (unpacked_size <= 0x20 || unpacked_size > 0x5000000) // ~83MB
                return null;

            byte bpp = header[0x18];
            if (bpp == 24)
                bpp = 32;
            else if (bpp != 8)
                return null;

            return new CpsMetaData
            {
                Width = header.ToUInt16(0x14),
                Height = header.ToUInt16(0x16),
                BPP = bpp,
                NeedKey = need_key,
                UnpackedSize = unpacked_size,
            };
        }
        public override ImageData Read(IBinaryStream stream, ImageMetaData info)
        {
            CpsMetaData m_info = (CpsMetaData)info;
            stream.Seek(m_info.NeedKey ? 0x28 : 0x1C, SeekOrigin.Begin);
            using (IBinaryStream input = CpsReader.UnpackCps(stream, m_info.UnpackedSize))
            {
                var reader = new CpsReader(input, (CpsMetaData)info);
                var pixels = reader.Read();
                return ImageData.Create(info, reader.Format, reader.Palette, pixels);
            }
        }
        public override void Write(Stream file, ImageData image)
        {
            throw new System.NotImplementedException("CpsFormat.Write not implemented");
        }

        internal class CpsMetaData : ImageMetaData
        {
            public bool NeedKey;
            public uint UnpackedSize;
        }
        internal class CpsReader
        {
            IBinaryStream m_input;
            CpsMetaData m_info;

            public PixelFormat Format { get; private set; }
            public BitmapPalette Palette { get; private set; }

            public CpsReader(IBinaryStream input, CpsMetaData info)
            {
                m_input = input;
                m_info = info;
                switch (info.BPP)
                {
                    case 4: Format = PixelFormats.Indexed4; break;
                    case 8: Format = PixelFormats.Indexed8; break;
                    case 16: Format = PixelFormats.Bgr555; break;
                    case 24: Format = PixelFormats.Bgr24; break;
                    case 32: Format = PixelFormats.Bgra32; break;
                }
            }
            public byte[] Read()
            {
                int pixel_size = m_info.BPP / 8;
                int image_size = ((int)m_info.Width * (int)m_info.Height * pixel_size);
                if (m_info.BPP == 8)
                {
                    var color_map = ImageFormat.ReadColorMap(m_input.AsStream, 256, PaletteFormat.RgbA7);
                    Palette = new BitmapPalette(color_map);
                }

                var output = m_input.ReadBytes(image_size);

                if (pixel_size == 4)
                {
                    for (int i = 0; i < image_size; i += 4)
                    {
                        byte r = output[i];
                        output[i] = output[i + 2];
                        output[i + 2] = r;
                        if (output[i + 3] >= byte.MaxValue / 2)
                            output[i + 3] = byte.MaxValue;
                        else
                            output[i + 3] = (byte)(output[i + 3] << 1);
                    }
                }
                return output;
            }

            public static IBinaryStream UnpackCps(IBinaryStream input, long unpacked_size)
            {
                var output = new byte[unpacked_size];

                int out_pos = 0;
                int i;

                while (out_pos < unpacked_size)
                {
                    var ctrl = input.ReadUInt8();
                    if ((ctrl & 0x80) != 0)
                    {
                        if ((ctrl & 0x40) != 0)
                        {
                            i = (ctrl & 0x1F) + 2;
                            if ((ctrl & 0x20) != 0)
                            {
                                i += (input.ReadUInt8() << 5);
                            }
                            var temp = input.ReadUInt8();
                            while (i > 0)
                            {
                                output[out_pos++] = temp;
                                i--;
                            }
                        }
                        else
                        {
                            var mod = ((ctrl & 3) << 8) | input.ReadUInt8();
                            i = ((ctrl >> 2) & 0xF) + 2;
                            while (i > 0)
                            {
                                if (out_pos - mod - 1 < 0)
                                    output[out_pos] = 0;
                                else
                                    output[out_pos] = output[out_pos - mod - 1];
                                out_pos++;
                                i--;
                            }
                        }
                    }
                    else
                    {
                        if ((ctrl & 0x40) != 0)
                        {
                            var num_bytes = input.ReadUInt8() + 1;
                            var copy_len = (ctrl & 0x3F) + 2;
                            var block = new byte[copy_len];
                            input.Read(block, 0, copy_len);
                            for (int rep = 0; rep < num_bytes; rep++)
                            {
                                for (int copy_pos = 0; copy_pos < copy_len; copy_pos++)
                                {
                                    output[out_pos++] = block[copy_pos];
                                }
                            }
                        }
                        else
                        {
                            i = (ctrl & 0x1F) + 1;
                            if ((ctrl & 0x20) != 0)
                            {
                                i += (input.ReadUInt8() << 5);
                            }
                            while (i > 0)
                            {
                                output[out_pos++] = input.ReadUInt8();
                                i--;
                            }
                        }
                    }
                }
                /*var headerblock = new byte[8];
                input.Seek(0x14, SeekOrigin.Begin);
                input.Read(headerblock, 0, 8);*/

                /*List<byte> f_out_bytes = new List<byte>();
                f_out_bytes.AddRange(headerblock);
                f_out_bytes.AddRange(output);*/

                return new BinMemoryStream(output);
            }
        }
    }
}
