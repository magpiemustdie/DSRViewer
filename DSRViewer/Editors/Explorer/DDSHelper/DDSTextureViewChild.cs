using System;
using System.IO;
using System.Numerics;
using ImGuiNET;
using Veldrid;
using SoulsFormats;
using DSRViewer.UI.Base;
using DSRViewer.FileProcess;

namespace DSRViewer.Editors.Explorer.DDSHelper
{
    public class DDSTextureViewChild : ImGuiChild
    {
        private Texture     _texture;
        private TextureView _textureView;
        private nint        _textureId;

        private Vector2  _previewSize;
        private FileNode _previousFile;
        private FileNode _currentFile;
        private bool     _isTextureLoaded;

        // ── Отдельное окно просмотра ─────────────────────────────────────
        private bool    _popoutOpen;
        private float   _popoutZoom = 1f;
        private readonly string _popoutId;
        private int     _lastRenderedFrame = -1;

        public DDSTextureViewChild(string childName, bool showChild)
        {
            _childName = childName;
            _showChild = showChild;
            _popoutId  = $"###{Guid.NewGuid().ToString("N")[..8]}";
        }

        public void Render(GraphicsDevice gd, ImGuiController cl, FileNode selected)
        {
            _currentFile = selected;

            if (selected == null || !selected.IsDDS)
            {
                ResetTextureState();
                return;
            }

            if (_previousFile != _currentFile)
            {
                LoadTexture(gd, cl, _currentFile);
                _previousFile = _currentFile;
            }

            RenderInlinePanel();
            RenderPopoutWindow();
        }

        // ── Встроенная панель ────────────────────────────────────────────

        private void RenderInlinePanel()
        {
            ImGui.BeginChild("Cld_TextureViewWin", _childSize, ImGuiChildFlags.Borders);

            // Инфо
            ImGui.Text($"Name:   {_currentFile.Name}");
            ImGui.Text($"Format: {_currentFile.DDSFormat}  (flag {_currentFile.DDSFormatFlag})");
            if (_texture != null)
                ImGui.Text($"Size:   {_texture.Width} x {_texture.Height} px");
            ImGui.Text($"Bytes:  {_currentFile.Size}");
            ImGui.TextDisabled(_currentFile.VirtualPath);

            ImGui.Spacing();

            // Кнопка открыть в отдельном окне
            if (ImGui.Button("Pop out##tex_popout"))
            {
                _popoutOpen = true;
                _popoutZoom = 1f;
            }

            ImGui.Spacing();

            // Превью
            if (_isTextureLoaded && _textureId != nint.Zero)
                ImGui.Image(_textureId, _previewSize);
            else
                ImGui.TextDisabled("No texture loaded");

            ImGui.EndChild();
        }

        // ── Отдельное окно ───────────────────────────────────────────────

        private void RenderPopoutWindow()
        {
            if (!_popoutOpen) return;
            if (!_isTextureLoaded || _textureId == nint.Zero) { _popoutOpen = false; return; }

            // Рендерим не более одного раза за кадр ImGui
            int frame = ImGui.GetFrameCount();
            if (_lastRenderedFrame == frame) return;
            _lastRenderedFrame = frame;

            ImGui.SetNextWindowSizeConstraints(new Vector2(300, 250), new Vector2(1024, 900));
            ImGui.SetNextWindowSize(new Vector2(
                                       Math.Clamp(_texture.Width  * _popoutZoom + 20, 300, 1024),
                                       Math.Clamp(_texture.Height * _popoutZoom + 80, 250, 900)),
                                   ImGuiCond.Appearing);

            string title = $"{_currentFile.Name}  [{_currentFile.DDSFormat}  {_texture.Width}x{_texture.Height}]{_popoutId}";

            if (ImGui.Begin(title, ref _popoutOpen, ImGuiWindowFlags.HorizontalScrollbar))
            {
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Zoom##popout", ref _popoutZoom, 0.1f, 4f, "%.1fx");
                ImGui.SameLine();
                if (ImGui.SmallButton("1:1##popout"))  _popoutZoom = 1f;
                ImGui.SameLine();
                if (ImGui.SmallButton("Fit##popout"))
                {
                    var avail = ImGui.GetContentRegionAvail();
                    _popoutZoom = Math.Min(avail.X / _texture.Width, avail.Y / _texture.Height);
                }

                ImGui.Separator();

                ImGui.Image(_textureId, new Vector2(_texture.Width * _popoutZoom,
                                                    _texture.Height * _popoutZoom));
            }
            ImGui.End();
        }

        // ── Загрузка ─────────────────────────────────────────────────────

        private void LoadTexture(GraphicsDevice gd, ImGuiController cl, FileNode file)
        {
            DisposeTextureResources();
            _isTextureLoaded = false;

            try
            {
                byte[] ddsBytes = null;

                if (file.IsNestedDDS)
                {
                    // Вложенная текстура — читаем через FileBinders
                    var binder = new FileBinders();
                    binder.ProcessPaths([file.VirtualPath], new FileOperation { GetObject = true });
                    if (binder.GetObject() is TPF.Texture texData)
                        ddsBytes = texData.Bytes;
                }
                else if (file.IsDDS && File.Exists(file.VirtualPath))
                {
                    // Корневой DDS — читаем напрямую с диска
                    ddsBytes = File.ReadAllBytes(file.VirtualPath);
                }

                if (ddsBytes == null || ddsBytes.Length == 0) return;

                new DDSTools().LoadDDSImage(ddsBytes, gd, out _texture, out _textureView);
                _textureId = cl.GetOrCreateImGuiBinding(gd.ResourceFactory, _textureView);

                // Превью вписываем в 320x320
                float scale = Math.Min(320f / _texture.Width, 320f / _texture.Height);
                _previewSize = new Vector2(_texture.Width * scale, _texture.Height * scale);

                _isTextureLoaded = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DDSViewer] {ex.Message}");
                DisposeTextureResources();
            }
        }

        // ── Вспомогательные ─────────────────────────────────────────────

        private void ResetTextureState()
        {
            DisposeTextureResources();
            _previewSize     = Vector2.Zero;
            _isTextureLoaded = false;
            _previousFile    = null;
        }

        private void DisposeTextureResources()
        {
            _textureView?.Dispose();
            _texture?.Dispose();
            _textureView = null;
            _texture     = null;
            _textureId   = nint.Zero;
        }

        public void Cleanup() => DisposeTextureResources();

        /// <summary>
        /// Загружает текстуру и показывает только pop-out окно (без инлайн-панели).
        /// Используется из FlverEditor.
        /// </summary>
        public void RenderPopout(GraphicsDevice gd, ImGuiController cl, FileNode node)
        {
            if (node == null || !node.IsNestedDDS) return;

            if (_previousFile != node)
            {
                LoadTexture(gd, cl, node);
                _currentFile  = node;
                _previousFile = node;
                _popoutOpen   = true;
                _popoutZoom   = 1f;
            }

            RenderPopoutWindow();
        }

        /// <summary>
        /// Сбрасывает кэш — следующий Render перезагрузит текстуру с диска.
        /// Вызывать после инжекции/замены текстуры.
        /// </summary>
        public void InvalidateCache() => _previousFile = null;
    }
}
