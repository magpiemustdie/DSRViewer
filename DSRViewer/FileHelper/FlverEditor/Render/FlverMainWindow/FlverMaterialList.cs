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
    public class FlverMaterialList : ImGuiClickableList
    {
        List<FLVER2.Material> _flverMaterials = [];

        public event Action<FLVER2.Material> OnMaterialSelected;

        public override void Render()
        {
            if (_flverMaterials.Count == 0)
            {
                ImGui.Text("No materials available");
                return;
            }

            for (int i = 0; i < _flverMaterials.Count; i++)
            {
                var material = _flverMaterials[i];
                string displayName = GetDisplayName(material, i);

                if (ImGui.Selectable(displayName, this.SelectedItem == i))
                {
                    this.SelectedItem = i;
                    this.SelectedItemName = displayName;

                    ClickHandlerMaterial?.Invoke(material, i);
                    OnMaterialSelected?.Invoke(material);
                }
            }
        }

        public void UpdateList(List<FLVER2.Material> newList)
        {
            _flverMaterials = newList ?? [];
        }

        public void ClearList()
        {
            _flverMaterials.Clear();
        }

        private string GetDisplayName(FLVER2.Material material, int index)
        {
            string mtd = material.MTD ?? "No MTD";
            return $"[{index}] {mtd}";
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