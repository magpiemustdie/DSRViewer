using DSRViewer.UI.Base;
using DSRViewer.FileProcess;
using DSRViewer.Editors.MTDEditor;
using ImGuiNET;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DSRViewer.Editors.FlverEditor
{
    public class FlverMaterialList : ImGuiClickableList
    {
        public FlverMaterialList()
        {
            _childSize = new(0, -1);
        }

        List<FLVER2.Material> _flverMaterials = [];
        List<MTDShortDetails> _mtdList = [];

        /// <summary>Вызывается при выборе материала в списке.</summary>
        public Action<FLVER2.Material> OnMaterialSelected { get; set; }

        /// <summary>Устанавливает список MTD для отображения MW рядом с именем материала.</summary>
        public void SetMtdList(List<MTDShortDetails> mtdList) =>
            _mtdList = mtdList ?? [];

        public override void Render()
        {
            if (_flverMaterials.Count == 0)
            {
                ImGui.TextDisabled("No materials");
                return;
            }

            for (int i = 0; i < _flverMaterials.Count; i++)
            {
                var material = _flverMaterials[i];
                string mtdName = material.MTD?.Split('\\').Last() ?? "No MTD";

                // MW из списка MTD
                string mwTag = "";
                if (_mtdList.Count > 0 && !string.IsNullOrEmpty(material.MTD))
                {
                    var info = _mtdList.FirstOrDefault(m =>
                        m.Name.Equals(mtdName, StringComparison.OrdinalIgnoreCase));
                    if (info != null)
                        mwTag = $" [MW{info.MW}]";
                }

                string display = $"[{i}]{mwTag} {mtdName}";

                if (ImGui.Selectable(display, SelectedItem == i))
                {
                    SelectedItem     = i;
                    SelectedItemName = display;
                    ClickHandlerMaterial?.Invoke(material, i);
                    OnMaterialSelected?.Invoke(material);
                }

                if (ImGui.IsItemHovered() && material.MTD != null)
                    ImGui.SetTooltip(material.MTD);
            }
        }

        public void UpdateList(List<FLVER2.Material> newList)
        {
            _flverMaterials = newList ?? [];
            SelectedItem = -1;
            SelectedItemName = "";
        }

        public void ClearList()
        {
            _flverMaterials.Clear();
            SelectedItem = -1;
            SelectedItemName = "";
        }

        public FLVER2.Material GetMaterial(int index)
        {
            if (index >= 0 && index < _flverMaterials.Count)
                return _flverMaterials[index];
            return null;
        }

        public void UpdateMaterialMTD(int index, string newMTD)
        {
            if (index >= 0 && index < _flverMaterials.Count)
            {
                _flverMaterials[index].MTD = newMTD;
            }
        }

        public int GetSelectedIndex() => SelectedItem;
        public string GetSelectedName() => SelectedItemName;
        public int GetItemCount() => _flverMaterials.Count;
    }
}