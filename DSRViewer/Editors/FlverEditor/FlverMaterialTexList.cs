using DSRViewer.UI.Base;
using DSRViewer.FileProcess;
using ImGuiNET;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DSRViewer.Editors.FlverEditor
{
    public class FlverTextureList : ImGuiClickableList
    {
        public FlverTextureList()
        {
            _childSize = new(0, -1);
        }

        List<FLVER2.Texture> _textures = [];

        public override void Render()
        {
            if (_textures.Count == 0)
            {
                ImGui.TextDisabled("No textures for this material");
                return;
            }

            ImGui.TextDisabled($"Textures ({_textures.Count}):");
            ImGui.Separator();

            for (int i = 0; i < _textures.Count; i++)
            {
                var texture = _textures[i];

                string textureInfo = $"[{i}] {texture.ParamName ?? "(no type)"}";
                if (!string.IsNullOrEmpty(texture.Path))
                    textureInfo += $": {texture.Path}";

                if (ImGui.Selectable(textureInfo, this.SelectedItem == i))
                {
                    this.SelectedItem = i;
                    this.SelectedItemName = textureInfo;
                    ClickHandlerMatTexture?.Invoke(texture, i);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextDisabled($"Index: {i}");
                    ImGui.Text($"Type:  {texture.ParamName ?? "(none)"}");
                    if (!string.IsNullOrEmpty(texture.Path))
                        ImGui.Text($"Path:  {texture.Path}");
                    ImGui.TextDisabled($"Scale: {texture.TilingScale.X:F2} x {texture.TilingScale.Y:F2}");
                    ImGui.EndTooltip();
                }
            }
        }

        public void UpdateList(List<FLVER2.Texture> newList)
        {
            _textures = newList ?? [];
            SelectedItem = -1;
            SelectedItemName = "";
        }

        public void ClearList()
        {
            _textures.Clear();
            SelectedItem = -1;
            SelectedItemName = "";
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
                    _textures[index].ParamName = newType;
            }
        }

        public void AddTexture(string newType)
        {
            _textures.Add(new FLVER2.Texture(newType, "", new Vector2(1, 1), FLVER2.Texture.TilingType.Repeat, FLVER2.Texture.TilingType.Repeat, 0, 0, 0));
        }

        public int GetSelectedIndex() => SelectedItem;
        public string GetSelectedName() => SelectedItemName;
    }
}