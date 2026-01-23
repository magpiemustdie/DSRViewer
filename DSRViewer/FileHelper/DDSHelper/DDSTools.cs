using System.IO;
using Veldrid;
using DirectXTexNet;
using System.Runtime.InteropServices;

namespace DSRViewer.FileHelper.DDSHelper
{
    public class DDSTools
    {
        public static byte[] fatcat = [0x44, 0x44, 0x53, 0x20, 0x7C, 0x00, 0x00, 0x00, 0x07, 0x10, 0x0A, 0x00, 0x04, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x08, 0x00,
                        0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x44, 0x58, 0x54, 0x31, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF1, 0xBD, 0xE5, 0x81, 0xDD, 0x8D, 0x25, 0xAD];

        public static string ReadDDSImageFormat(byte[] tex_bytes)
        {
            // Load DDS file into a MemoryStream
            byte[] ddsData = tex_bytes;
            GCHandle array = GCHandle.Alloc(ddsData, GCHandleType.Pinned);

            // Decode the DDS image using DirectXTex
            var scratchImage = TexHelper.Instance.LoadFromDDSMemory(array.AddrOfPinnedObject(), ddsData.Length, DDS_FLAGS.NONE);
            array.Free();
            return scratchImage.GetMetadata().Format.ToString();
        }

        public void LoadDDSImage(byte[] tex_bytes, GraphicsDevice graphicsDevice, out Texture texture, out TextureView textureView)
        {

            GCHandle array = GCHandle.Alloc(tex_bytes, GCHandleType.Pinned);

            // Decode the DDS image using DirectXTex
            var scratchImage = TexHelper.Instance.LoadFromDDSMemory(array.AddrOfPinnedObject(), tex_bytes.Length, DDS_FLAGS.NONE);
            Console.WriteLine(scratchImage.GetMetadata().Format);
            array.Free();

            if (!IsUncompressed(scratchImage.GetMetadata().Format))
            {
                try
                {
                    scratchImage = scratchImage.Decompress(DXGI_FORMAT.R8G8B8A8_UNORM);
                }
                catch
                {
                    Console.WriteLine($"Strange format -> {scratchImage.GetMetadata().Format}");
                }
            }

            if (scratchImage.GetMetadata().IsCubemap())
            {
                CubeMapConvertor(ref scratchImage);
            }
            //var stream = scratchImage.SaveToDDSMemory(DDS_FLAGS.FORCE_DX10_EXT);
            //byte[] decompressedBytes = new byte[stream.Length];
            //stream.Read(decompressedBytes);

            //stream.Close();
            //File.WriteAllBytes($"texture.dds", decompressedBytes);
            var image = scratchImage.GetImage(0, 0, 0); // Get the first image

            // Create Veldrid texture
            TextureDescription textureDesc = TextureDescription.Texture2D(
                (uint)image.Width,
                (uint)image.Height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm, // Assumes RGBA 32-bit format
                TextureUsage.Sampled);
            texture = graphicsDevice.ResourceFactory.CreateTexture(textureDesc);

            // Update texture with DDS image data
            graphicsDevice.UpdateTexture(
                texture,
                image.Pixels,
                (uint)(image.RowPitch * image.Height),
                0, 0, 0,
                (uint)image.Width,
                (uint)image.Height,
                1,
                0,
                0);

            // Create texture view
            textureView = graphicsDevice.ResourceFactory.CreateTextureView(texture);
        }

        private bool IsUncompressed(DXGI_FORMAT format)
        {
            return format switch
            {
                DXGI_FORMAT.R8G8B8A8_UNORM => true,
                _ => false,
            };
        }

        private void CubeMapConvertor(ref ScratchImage cubeTex)
        {
            int faceWidth = cubeTex.GetMetadata().Width;
            int faceHeight = cubeTex.GetMetadata().Height;
            using var crossImage = TexHelper.Instance.Initialize2D(
                DXGI_FORMAT.R8G8B8A8_UNORM,
                faceWidth * 4,
                faceHeight * 3,
                1,
                0,
                CP_FLAGS.NONE);

            for (int i = 0; i < 6; i++)
            {
                var face = cubeTex.GetImage(0, i, 0);

                int x = 0, y = 0;
                switch (i)
                {
                    case 0: x = faceWidth * 2; y = faceHeight * 1; break; // +x
                    case 1: x = faceWidth * 0; y = faceHeight * 1; break; // -x
                    case 2: x = faceWidth * 1; y = faceHeight * 0; break; // +y
                    case 3: x = faceWidth * 1; y = faceHeight * 2; break; // -y
                    case 4: x = faceWidth * 1; y = faceHeight * 1; break; // +z
                    case 5: x = faceWidth * 3; y = faceHeight * 1; break; // -z
                }

                TexHelper.Instance.CopyRectangle(face, 0, 0, faceWidth, faceHeight, crossImage.GetImage(0), 0, x, y);

            }
            var stream = crossImage.SaveToDDSMemory(DDS_FLAGS.FORCE_DX10_EXT);
            byte[] decompressedBytes = new byte[stream.Length];
            stream.Read(decompressedBytes);
            stream.Close();

            GCHandle new_array = GCHandle.Alloc(decompressedBytes, GCHandleType.Pinned);
            cubeTex = TexHelper.Instance.LoadFromDDSMemory(new_array.AddrOfPinnedObject(), decompressedBytes.Length, DDS_FLAGS.NONE);
            new_array.Free();
        }
    }
}
