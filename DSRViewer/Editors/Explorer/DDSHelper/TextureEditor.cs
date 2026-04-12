using DirectXTexNet;
using DSRViewer.FileProcess;
using ImageMagick;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Veldrid;

namespace DSRViewer.Editors.Explorer.DDSHelper
{
    /// <summary>
    /// Обрабатывает DDS-текстуры через ImageMagick + DirectXTex.
    /// Содержит всю логику канальной обработки, компрессии и MW-маппинга.
    /// </summary>
    public class TextureEditor
    {
        private readonly GraphicsDevice _gd;

        public TextureEditor(GraphicsDevice gd)
        {
            _gd = gd;
        }

        // ── Публичный API ────────────────────────────────────────────────

        /// <summary>Обрабатывает DDS-байты через ImageMagick (канальная обработка + перекомпрессия).</summary>
        public byte[] EditImage(byte[] ddsBytes, string textureName)
        {
            LoadMwMapping();
            if (_mwMap == null) return ddsBytes;

            _mwMap.TryGetValue(NormalizeName(textureName), out var mwValues);
            bool hasBoth = mwValues != null && mwValues.Contains(0) && mwValues.Contains(1);
            bool isMw1   = mwValues != null && mwValues.Count == 1 && mwValues.Contains(1);

            Console.WriteLine($"[TextureEditor] {textureName}: mw={string.Join(",", mwValues ?? new HashSet<int>())}");

            var handle = GCHandle.Alloc(ddsBytes, GCHandleType.Pinned);
            ScratchImage scratch;
            try
            {
                scratch = TexHelper.Instance.LoadFromDDSMemory(handle.AddrOfPinnedObject(), ddsBytes.Length, DDS_FLAGS.NONE);
            }
            finally { handle.Free(); }

            var originalMeta = scratch.GetMetadata();

            if (!DDSTools.IsRgba(originalMeta.Format))
            {
                ScratchImage decompressed;
                try { decompressed = scratch.Decompress(DXGI_FORMAT.R8G8B8A8_UNORM); }
                catch
                {
                    Console.WriteLine($"[TextureEditor] Cannot decompress: {originalMeta.Format}");
                    scratch.Dispose();
                    return ddsBytes;
                }
                scratch.Dispose();
                scratch = decompressed;
            }

            if (originalMeta.IsCubemap())
            {
                var cross = TextureImageProcessor.CubeMapToCross(scratch);
                var old = scratch;
                scratch = cross;
                old.Dispose();
            }

            byte[] tgaBytes;
            using (var stream = scratch.SaveToTGAMemory(0))
            {
                tgaBytes = new byte[stream.Length];
                stream.Read(tgaBytes);
            }
            scratch.Dispose();

            byte[] processedTga = ProcessChannels(tgaBytes, isMw1 || hasBoth);

            var outHandle = GCHandle.Alloc(processedTga, GCHandleType.Pinned);
            ScratchImage result;
            try
            {
                result = TexHelper.Instance.LoadFromTGAMemory(outHandle.AddrOfPinnedObject(), processedTga.Length);
            }
            finally { outHandle.Free(); }

            try
            {
                if (originalMeta.IsCubemap())
                {
                    var cube = TextureImageProcessor.CrossToCubeMap(result);
                    result.Dispose();
                    result = cube;
                }

                var withMips = result.GenerateMipMaps(TEX_FILTER_FLAGS.FANT, 0);
                result.Dispose();
                result = null;

                ScratchImage compressed;
                try { compressed = Compress(withMips, originalMeta.Format); }
                finally { withMips.Dispose(); }

                try
                {
                    using var outStream = compressed.SaveToDDSMemory(DDS_FLAGS.FORCE_DX10_EXT);
                    byte[] output = new byte[outStream.Length];
                    outStream.Read(output);
                    return output;
                }
                finally { compressed.Dispose(); }
            }
            finally { result?.Dispose(); }
        }

        /// <summary>Обрабатывает TPF.Texture на месте: меняет Bytes, Format и Type.</summary>
        public void EditTexture(TPF.Texture texture)
        {
            var newBytes = EditImage(texture.Bytes, texture.Name);
            DDSTextureApplier.Apply(texture, newBytes);
        }

        /// <summary>Обрабатывает все текстуры из списка FileNode через FileBinders.</summary>
        public void EditBatch(IEnumerable<FileNode> texNodes, Func<FileNode, bool> filter = null,
            Action<string> onComplete = null)
        {
            var paths = new List<string>();
            foreach (var n in texNodes)
                if (filter == null || filter(n))
                    paths.Add(n.VirtualPath);

            if (paths.Count == 0) return;

            new FileBinders().ProcessPaths(paths, new FileOperation
            {
                WriteObject = true,
                UseTexDelegate = true,
                AdditionalTextureProcessing = (texture, virtualPath) =>
                {
                    Console.WriteLine($"[TextureEditor] Processing: {virtualPath} / {texture.Name}");
                    EditTexture(texture);
                    onComplete?.Invoke(virtualPath);
                }
            });
        }

        /// <summary>Обрабатывает все текстуры внутри узла (рекурсивно находит IsNestedDDS).</summary>
        public void EditAllInNode(FileNode root, Func<FileNode, bool> filter = null,
            Action<string> onComplete = null)
        {
            EditBatch(root.FindAll(n => n.IsNestedDDS), filter, onComplete);
        }

        // ── MW-маппинг ───────────────────────────────────────────────────

        private static Dictionary<string, HashSet<int>> _mwMap;
        private const string MwCsvPath = "MaterialTexturesDiffMW.csv";

        private static void LoadMwMapping()
        {
            if (_mwMap != null) return;
            _mwMap = new Dictionary<string, HashSet<int>>();
            if (!File.Exists(MwCsvPath)) return;

            foreach (var line in File.ReadLines(MwCsvPath, Encoding.UTF8).Skip(1))
            {
                int comma = line.IndexOf(',');
                if (comma < 0) continue;
                string tex = NormalizeName(line[..comma].Trim());
                var mwSet = new HashSet<int>();
                foreach (var token in line[(comma + 1)..].Split(','))
                    if (int.TryParse(token.Trim(), out int v))
                        mwSet.Add(v);
                _mwMap[tex] = mwSet;
            }
        }

        public static void ResetMwMapping() => _mwMap = null;

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int dot = name.LastIndexOf('.');
            return (dot > 0 ? name[..dot] : name).ToLower();
        }

        // ── Приватные методы ─────────────────────────────────────────────

        private static byte[] ProcessChannels(byte[] tgaBytes, bool processAlpha)
        {
            using var image = new MagickImage(tgaBytes);
            using var red   = (MagickImage)image.Separate(Channels.Red).First();
            using var green = (MagickImage)image.Separate(Channels.Green).First();
            using var blue  = (MagickImage)image.Separate(Channels.Blue).First();
            using var alpha = (MagickImage)image.Separate(Channels.Alpha).First();

            if (processAlpha)
            {
                // Заполняем альфа-канал белым (непрозрачным)
                alpha.Evaluate(Channels.Gray, EvaluateOperator.Set, new Percentage(100));
            }

            using var collection = new MagickImageCollection { red, green, blue, alpha };
            using var combined = collection.Combine();
            using var ms = new MemoryStream();
            combined.Write(ms, MagickFormat.Tga);
            return ms.ToArray();
        }

        private ScratchImage Compress(ScratchImage img, DXGI_FORMAT format)
        {
            bool isBC7orBC6 = format == DXGI_FORMAT.BC7_UNORM || format == DXGI_FORMAT.BC6H_UF16;
            if (isBC7orBC6 && _gd.BackendType == GraphicsBackend.Direct3D11)
            {
                IntPtr devicePtr = _gd.GetD3D11Info().Device;
                return img.Compress(devicePtr, format, TEX_COMPRESS_FLAGS.PARALLEL | TEX_COMPRESS_FLAGS.DEFAULT, 1.0f);
            }
            return img.Compress(format, TEX_COMPRESS_FLAGS.PARALLEL | TEX_COMPRESS_FLAGS.DEFAULT, 0.5f);
        }
    }

    /// <summary>Вспомогательные методы для преобразования кубмапов (используются DDSTools и TextureEditor).</summary>
    internal static class TextureImageProcessor
    {
        /// <summary>Разворачивает кубмап в крест. Возвращает новый ScratchImage, входной не освобождает.</summary>
        internal static ScratchImage CubeMapToCross(ScratchImage cubeTex)
        {
            var meta = cubeTex.GetMetadata();
            int fw = meta.Width, fh = meta.Height;

            using var cross = TexHelper.Instance.Initialize2D(
                DXGI_FORMAT.R8G8B8A8_UNORM, fw * 4, fh * 3, 1, 0, CP_FLAGS.NONE);

            (int x, int y)[] offsets = [(fw*2,fh),(fw*0,fh),(fw,0),(fw,fh*2),(fw,fh),(fw*3,fh)];
            for (int i = 0; i < 6; i++)
                TexHelper.Instance.CopyRectangle(cubeTex.GetImage(0, i, 0), 0, 0, fw, fh,
                    cross.GetImage(0), 0, offsets[i].x, offsets[i].y);

            byte[] bytes;
            using (var stream = cross.SaveToDDSMemory(DDS_FLAGS.FORCE_DX10_EXT))
            {
                bytes = new byte[stream.Length];
                stream.Read(bytes);
            }

            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try { return TexHelper.Instance.LoadFromDDSMemory(handle.AddrOfPinnedObject(), bytes.Length, DDS_FLAGS.NONE); }
            finally { handle.Free(); }
        }

        /// <summary>Собирает крест обратно в кубмап.</summary>
        internal static ScratchImage CrossToCubeMap(ScratchImage cross)
        {
            var meta = cross.GetMetadata();
            if (meta.Width % 4 != 0 || meta.Height % 3 != 0)
                throw new ArgumentException("Cross image must be 4:3 ratio");

            int fw = meta.Width / 4, fh = meta.Height / 3;
            var cube = TexHelper.Instance.InitializeCube(meta.Format, fw, fh, 1, 0, CP_FLAGS.NONE);
            var src = cross.GetImage(0);

            (int x, int y)[] offsets = [(fw*2,fh),(fw*0,fh),(fw,0),(fw,fh*2),(fw,fh),(fw*3,fh)];
            for (int i = 0; i < 6; i++)
                TexHelper.Instance.CopyRectangle(src, offsets[i].x, offsets[i].y, fw, fh,
                    cube.GetImage(0, i, 0), 0, 0, 0);

            return cube;
        }
    }
}
