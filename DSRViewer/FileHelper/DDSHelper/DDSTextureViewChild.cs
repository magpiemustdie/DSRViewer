using System.Numerics;
using ImGuiNET;
using Veldrid;
using SoulsFormats;
using DSRViewer.FileHelper.DDSHelper;
using DSRViewer.ImGuiHelper;
using DSRViewer.FileHelper;
using DSRViewer.FileHelper.FileExplorer.TreeBuilder;

namespace DSRViewer.DDSHelper
{
    public class DDSTextureViewChild : ImGuiChild
    {
        private Texture _texture;
        private TextureView _textureView;
        private nint _textureId;
        float _scale = 0;
        FileNode _prevFileNode = new();
        FileNode _selected = new();

        float _window_scale_w = 0;
        float _window_scale_h = 0;


        public void Render(GraphicsDevice gd, ImGuiController cl, FileTreeViewer viewer)
        {
            if (viewer.GetSelectedFile() == null)
                return;

            _selected = viewer.GetSelectedFile();

            if (!_selected.IsNestedDDS)
            {
                _prevFileNode = new();
                _textureView?.Dispose();
                _texture?.Dispose();
                _scale = 0;
                return;
            }

            if (_selected.IsNestedDDS & _prevFileNode != _selected)
            {
                _prevFileNode = _selected;
                FileBinders binders = new();
                binders.SetGetObjectOnly();
                binders.Read(_selected.VirtualPath);
                TPF.Texture dds = (TPF.Texture)binders.GetObject();

                _textureView?.Dispose();
                _texture?.Dispose();

                DDSTools textureLoader = new();
                textureLoader.LoadDDSImage(dds.Bytes, gd, out _texture, out _textureView);
                _textureId = cl.GetOrCreateImGuiBinding(gd.ResourceFactory, _textureView);
                _scale = Math.Min((float)512 / Convert.ToInt32(_texture.Width), (float)512 / Convert.ToInt32(_texture.Height));
                _window_scale_w = _scale * _texture.Width;
                _window_scale_h = _scale * _texture.Height;
            }

            ImGui.BeginChild("Cld_TextureViewWin", new Vector2(0, 0), ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY);
            {
                if (_selected.IsNestedDDS)
                {
                    ImGui.BeginChild("Cld_TextureInfo", new Vector2(0, 0), ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY);
                    {
                        ImGui.Text($"Name = {_selected.Name}");
                        ImGui.Text($"Format = {_selected.DDSFormat}");
                        ImGui.Text($"Format flag = {_selected.DDSFormatFlag}");
                        ImGui.Text($"Size (px) = {_texture.Width}, {_texture.Height}");
                        ImGui.Text($"Size (byte) = {_selected.Size}");
                        ImGui.Text($"Path = {_selected.VirtualPath}");
                    }
                    ImGui.EndChild();

                    ImGui.BeginChild("Cld_TextureView", new Vector2(0, 0), ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY);
                    {
                        if (_textureId != nint.Zero)
                        {
                            ImGui.Image(_textureId, new Vector2(_window_scale_w, _window_scale_h));
                        }
                    }
                    ImGui.EndChild();
                }
            }
            ImGui.EndChild();
        }


        public void Render(GraphicsDevice gd, ImGuiController cl, FileNode selected)
        {
            _selected = selected;

            if (!_selected.IsNestedDDS | _selected == null)
            {
                _prevFileNode = new();
                _textureView?.Dispose();
                _texture?.Dispose();
                _scale = 0;
                return;
            }

            if (_prevFileNode != _selected)
            {
                _prevFileNode = _selected;
                FileBinders binders = new();
                binders.SetGetObjectOnly();
                binders.Read(_selected.VirtualPath);

                TPF.Texture dds = (TPF.Texture)binders.GetObject();

                _textureView?.Dispose();
                _texture?.Dispose();

                DDSTools textureLoader = new();
                textureLoader.LoadDDSImage(dds.Bytes, gd, out _texture, out _textureView);
                _textureId = cl.GetOrCreateImGuiBinding(gd.ResourceFactory, _textureView);
                _scale = Math.Min((float)512 / Convert.ToInt32(_texture.Width), (float)512 / Convert.ToInt32(_texture.Height));
                _window_scale_w = _scale * _texture.Width;
                _window_scale_h = _scale * _texture.Height;
            }

            ImGui.BeginChild("Cld_TextureViewWin", _childSize, _childFlags);
            {
                ImGui.BeginChild("Cld_TextureInfo", _childSize, _childFlags);
                {
                    ImGui.Text($"Name = {_selected.Name}");
                    ImGui.Text($"Format = {_selected.DDSFormat}");
                    ImGui.Text($"Format flag = {_selected.DDSFormatFlag}");
                    ImGui.Text($"Size (px) = {_texture.Width}, {_texture.Height}");
                    ImGui.Text($"Size (byte) = {_selected.Size}");
                    ImGui.Text($"Path = {_selected.VirtualPath}");
                }
                ImGui.EndChild();

                ImGui.BeginChild("Cld_TextureView", _childSize, _childFlags);
                {
                    if (_textureId != nint.Zero)
                    {
                        ImGui.Image(_textureId, new Vector2(_window_scale_w, _window_scale_h));
                    }
                }
                ImGui.EndChild();
            }
            ImGui.EndChild();
        }

        public void UpdateInfo(GraphicsDevice gd, ImGuiController cl)
        {
            FileBinders binders = new();
            binders.SetGetObjectOnly();
            binders.Read(_selected.VirtualPath);
            TPF.Texture dds = (TPF.Texture)binders.GetObject();

            _textureView?.Dispose();
            _texture?.Dispose();

            DDSTools textureLoader = new();
            textureLoader.LoadDDSImage(dds.Bytes, gd, out _texture, out _textureView);
            _textureId = cl.GetOrCreateImGuiBinding(gd.ResourceFactory, _textureView);
            _scale = Math.Min((float)512 / Convert.ToInt32(_texture.Width), (float)512 / Convert.ToInt32(_texture.Height));
            _window_scale_w = _scale * _texture.Width;
            _window_scale_h = _scale * _texture.Height;
            _selected = new();
            _prevFileNode = new();
        }
    }
}
