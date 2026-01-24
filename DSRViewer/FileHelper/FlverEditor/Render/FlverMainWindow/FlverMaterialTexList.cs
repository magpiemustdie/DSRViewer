using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;

namespace DSRViewer.FileHelper.FlverEditor.Render
{
    public class FlverTextureList : ClickableList
    {
        List<FLVER2.Texture> _textures = [];

        public override void Render()
        {
            if (_textures.Count == 0)
            {
                ImGui.Text("No textures for this material");
                return;
            }

            ImGui.Text($"Textures ({_textures.Count}):");
            ImGui.Separator();

            for (int i = 0; i < _textures.Count; i++)
            {
                var texture = _textures[i];

                string textureInfo = $"[{i}] {texture.Type}";
                if (!string.IsNullOrEmpty(texture.Path))
                    textureInfo += $": {texture.Path}";

                if (ImGui.Selectable(textureInfo, this.SelectedItem == i))
                {
                    this.SelectedItem = i;
                    this.SelectedItemName = textureInfo;
                    CurrentClickHandlerMatTexture?.Invoke(texture, i);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text($"Index: {i}");
                    ImGui.Text($"Type: {texture.Type}");
                    if (!string.IsNullOrEmpty(texture.Path))
                        ImGui.Text($"Path: {texture.Path}");
                    ImGui.Text($"Scale: X={texture.Scale.X}, Y={texture.Scale.Y}");
                    ImGui.EndTooltip();
                }
            }
        }

        public void UpdateList(List<FLVER2.Texture> newList)
        {
            _textures = newList ?? new List<FLVER2.Texture>();
        }

        public void ClearList()
        {
            _textures.Clear();
        }

        public FLVER2.Texture GetTexture(int index)
        {
            if (index >= 0 && index < _textures.Count)
                return _textures[index];
            return null;
        }

        public void UpdateTexture(int index, string newPath, string newType)
        {
            if (index >= 0 && index < _textures.Count)
            {
                if (!string.IsNullOrEmpty(newPath))
                    _textures[index].Path = newPath;
                if (!string.IsNullOrEmpty(newType))
                    _textures[index].Type = newType;
            }
        }

        public int GetSelectedIndex() => SelectedItem;
        public string GetSelectedName() => SelectedItemName;
    }
}