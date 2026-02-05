using System.Numerics;
using ImGuiNET;
using Veldrid;
using SoulsFormats;
using DSRViewer.FileHelper.FileExplorer.DDSHelper;
using DSRViewer.ImGuiHelper;
using DSRViewer.FileHelper;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;

namespace DSRViewer.FileHelper.FileExplorer.DDSHelper
{
    public class DDSTextureViewChild : ImGuiChild
    {
        private Texture _texture;
        private TextureView _textureView;
        private nint _textureId;

        private float _previewScale = 0f;
        private Vector2 _previewSize;

        private FileNode _previousFile;
        private FileNode _currentFile;

        private bool _isTextureLoaded = false;

        public DDSTextureViewChild(string childName, bool showChild)
        {
            _childName = childName;
            _showChild = showChild;
        }

        public void Render(GraphicsDevice gd, ImGuiController cl, FileNode selected)
        {
            _currentFile = selected;

            if (!IsValidTextureFile(_currentFile))
            {
                ResetTextureState();
                return;
            }

            if (_previousFile != _currentFile)
            {
                LoadTexture(gd, cl, _currentFile);
                _previousFile = _currentFile;
            }

            RenderTextureWindow();
        }

        private bool IsValidTextureFile(FileNode file)
        {
            return file != null && file.IsNestedDDS;
        }

        private void LoadTexture(GraphicsDevice gd, ImGuiController cl, FileNode file)
        {
            DisposeTextureResources();
            _isTextureLoaded = false;

            try
            {
                var binders = new FileBinders();
                binders.SetGetObjectOnly();
                binders.Read(file.VirtualPath);

                if (binders.GetObject() is TPF.Texture textureData)
                {
                    var textureLoader = new DDSTools();
                    textureLoader.LoadDDSImage(textureData.Bytes, gd, out _texture, out _textureView);

                    _textureId = cl.GetOrCreateImGuiBinding(gd.ResourceFactory, _textureView);

                    CalculatePreviewScale();

                    _isTextureLoaded = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading texture: {ex.Message}");
                DisposeTextureResources();
            }
        }

        private void CalculatePreviewScale()
        {
            if (_texture == null) return;

            float width = _texture.Width;
            float height = _texture.Height;

            // Оригинальная логика масштабирования
            float scaleX = 512f / width;
            float scaleY = 512f / height;

            _previewScale = Math.Min(scaleX, scaleY);
            _previewSize = new Vector2(width * _previewScale, height * _previewScale);
        }

        private void RenderTextureWindow()
        {
            ImGui.BeginChild("Cld_TextureViewWin", _childSize, _childFlags);
            {
                RenderTextureInfoPanel();
                RenderTexturePreviewPanel();
            }
            ImGui.EndChild();
        }

        private void RenderTextureInfoPanel()
        {
            ImGui.BeginChild("Cld_TextureInfo", _childSize, _childFlags);
            {
                ImGui.Text($"Name = {_currentFile.Name}");
                ImGui.Text($"Format = {_currentFile.DDSFormat}");
                ImGui.Text($"Format flag = {_currentFile.DDSFormatFlag}");

                if (_texture != null)
                {
                    ImGui.Text($"Size (px) = {_texture.Width}, {_texture.Height}");
                }
                else
                {
                    ImGui.Text($"Size (px) = N/A");
                }

                ImGui.Text($"Size (byte) = {_currentFile.Size}");
                ImGui.Text($"Path = {_currentFile.VirtualPath}");
            }
            ImGui.EndChild();
        }

        private void RenderTexturePreviewPanel()
        {
            ImGui.BeginChild("Cld_TextureView", _childSize, _childFlags);
            {
                if (_isTextureLoaded && _textureId != nint.Zero)
                {
                    ImGui.Image(_textureId, _previewSize);
                }
                else
                {
                    var center = ImGui.GetContentRegionAvail() * 0.5f;
                    var textSize = ImGui.CalcTextSize("No texture loaded");
                    ImGui.SetCursorPos(center - textSize * 0.5f);
                    ImGui.TextDisabled("No texture loaded");
                }
            }
            ImGui.EndChild();
        }

        public void UpdateInfo(GraphicsDevice gd, ImGuiController cl)
        {
            if (_currentFile?.IsNestedDDS != true) return;

            LoadTexture(gd, cl, _currentFile);

            _currentFile = new FileNode();
            _previousFile = new FileNode();
        }

        private void ResetTextureState()
        {
            DisposeTextureResources();
            _previewScale = 0f;
            _previewSize = Vector2.Zero;
            _isTextureLoaded = false;
            _previousFile = null;
        }

        private void DisposeTextureResources()
        {
            _textureView?.Dispose();
            _texture?.Dispose();

            _textureView = null;
            _texture = null;
            _textureId = nint.Zero;
        }

        public void Cleanup()
        {
            DisposeTextureResources();
        }
    }
}