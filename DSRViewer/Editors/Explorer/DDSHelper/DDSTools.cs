using DirectXTexNet;
using SoulsFormats;
using System;
using System.Runtime.InteropServices;
using Veldrid;

namespace DSRViewer.Editors.Explorer.DDSHelper
{
    /// <summary>Утилиты для работы с DDS-текстурами: чтение формата и загрузка в GPU.</summary>
    public class DDSTools
    {
        // Минимальный валидный DDS 1x1 BC1 — используется как заглушка при добавлении новой текстуры
        public static readonly byte[] fatcat =
        [
            0x44, 0x44, 0x53, 0x20, 0x7C, 0x00, 0x00, 0x00, 0x07, 0x10, 0x0A, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00,
            0x44, 0x58, 0x54, 0x31, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0xA1, 0xF0, 0x81, 0xF0, 0xAA, 0xAA, 0xAA, 0xAA
        ];

        /// <summary>Читает формат DDS из байтов без декомпрессии.</summary>
        public static string ReadDDSImageFormat(byte[] ddsBytes)
        {
            var handle = GCHandle.Alloc(ddsBytes, GCHandleType.Pinned);
            try
            {
                var img = TexHelper.Instance.LoadFromDDSMemory(handle.AddrOfPinnedObject(), ddsBytes.Length, DDS_FLAGS.NONE);
                string fmt = img.GetMetadata().Format.ToString();
                img.Dispose();
                return fmt;
            }
            finally { handle.Free(); }
        }

        /// <summary>
        /// Читает все метаданные DDS нужные для TPF.Texture:
        /// формат, количество mip-уровней, является ли кубмапом.
        /// </summary>
        public static DDSMeta ReadDDSMeta(byte[] ddsBytes)
        {
            var handle = GCHandle.Alloc(ddsBytes, GCHandleType.Pinned);
            try
            {
                var img  = TexHelper.Instance.LoadFromDDSMemory(handle.AddrOfPinnedObject(), ddsBytes.Length, DDS_FLAGS.NONE);
                var meta = img.GetMetadata();
                var result = new DDSMeta(
                    Format:    meta.Format.ToString(),
                    Mipmaps:   (int)meta.MipLevels,
                    IsCubemap: meta.IsCubemap()
                );
                img.Dispose();
                return result;
            }
            finally { handle.Free(); }
        }

        /// <summary>
        /// Читает метаданные DDS из файла (только заголовок, без загрузки всех байтов).
        /// Оптимизация: читает только первые 256 байт для заголовка.
        /// Если заголовка недостаточно — читает весь файл.
        /// </summary>
        public static DDSMeta ReadDDSMetaFromFile(string ddsPath)
        {
            const int headerSize = 256; // с запасом для DX10

            using var fs = new FileStream(ddsPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            int bytesToRead = (int)Math.Min(headerSize, fs.Length);
            byte[] header = new byte[bytesToRead];
            fs.Read(header, 0, bytesToRead);

            try
            {
                return ReadDDSMeta(header);
            }
            catch
            {
                // Заголовка недостаточно — читаем весь файл
                fs.Seek(0, SeekOrigin.Begin);
                byte[] full = new byte[fs.Length];
                fs.Read(full, 0, full.Length);
                return ReadDDSMeta(full);
            }
        }

        public record DDSMeta(string Format, int Mipmaps, bool IsCubemap);

        /// <summary>Загружает DDS в GPU-текстуру Veldrid для предпросмотра.</summary>
        public void LoadDDSImage(byte[] ddsBytes, GraphicsDevice gd, out Texture texture, out TextureView textureView)
        {
            var handle = GCHandle.Alloc(ddsBytes, GCHandleType.Pinned);
            ScratchImage scratch;
            try
            {
                scratch = TexHelper.Instance.LoadFromDDSMemory(handle.AddrOfPinnedObject(), ddsBytes.Length, DDS_FLAGS.NONE);
            }
            finally { handle.Free(); }

            ScratchImage decompressed = null;
            try
            {
                if (!IsRgba(scratch.GetMetadata().Format))
                {
                    try
                    {
                        decompressed = scratch.Decompress(DXGI_FORMAT.R8G8B8A8_UNORM);
                        scratch.Dispose();
                        scratch = decompressed;
                        decompressed = null;
                    }
                    catch { Console.WriteLine($"[DDSTools] Cannot decompress: {scratch.GetMetadata().Format}"); }
                }

                if (scratch.GetMetadata().IsCubemap())
                {
                    var cross = TextureImageProcessor.CubeMapToCross(scratch);
                    var old = scratch;
                    scratch = cross;
                    old.Dispose();
                }

                var image = scratch.GetImage(0, 0, 0);

                texture = gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
                    (uint)image.Width, (uint)image.Height, 1, 1,
                    PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled));

                gd.UpdateTexture(texture, image.Pixels,
                    (uint)(image.RowPitch * image.Height),
                    0, 0, 0, (uint)image.Width, (uint)image.Height, 1, 0, 0);

                textureView = gd.ResourceFactory.CreateTextureView(texture);
            }
            finally
            {
                scratch.Dispose();
                decompressed?.Dispose();
            }
        }

        internal static bool IsRgba(DXGI_FORMAT format) =>
            format == DXGI_FORMAT.R8G8B8A8_UNORM;
    }

    /// <summary>
    /// Применяет DDS-байты к TPF.Texture обновляя Format, Mipmaps и Type.
    /// </summary>
    public static class DDSTextureApplier
    {
        public static void Apply(SoulsFormats.TPF.Texture texture, byte[] ddsBytes)
        {
            texture.Bytes = ddsBytes;
            try
            {
                var meta = DDSTools.ReadDDSMeta(ddsBytes);
                if (DDS_FlagFormatList.DDSFlagListSet.TryGetValue(meta.Format, out int flag))
                    texture.Format = Convert.ToByte(flag);
                texture.Mipmaps = (byte)(meta.Mipmaps > 1 ? 1 : 0);
                if (meta.IsCubemap)
                    texture.Type = SoulsFormats.TPF.TexType.Cubemap;
                else if (texture.Type == SoulsFormats.TPF.TexType.Cubemap)
                    texture.Type = SoulsFormats.TPF.TexType.Texture;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DDSTextureApplier] {texture.Name}: {ex.Message}");
            }
        }
    }
}
