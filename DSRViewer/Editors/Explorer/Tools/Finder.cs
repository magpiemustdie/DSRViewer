using DSRViewer.FileProcess;
using ImGuiNET;
using System.Collections.Generic;

namespace DSRViewer.Editors.Explorer.Tools
{
    /// <summary>Инструмент поиска узлов в дереве файлов по имени.</summary>
    public class Finder
    {
        private string _input = "";
        private readonly List<string> _results = new();

        public void Render(FileNode root)
        {
            if (!ImGui.CollapsingHeader("Finder")) return;

            ImGui.InputText("##search", ref _input, 256);
            ImGui.SameLine();

            if (ImGui.Button("Full name"))
                Search(root, n => n.Name.Equals(_input, System.StringComparison.OrdinalIgnoreCase));

            ImGui.SameLine();

            if (ImGui.Button("Contains"))
                Search(root, n => n.Name.Contains(_input, System.StringComparison.OrdinalIgnoreCase));

            if (_results.Count > 0)
            {
                ImGui.Separator();
                ImGui.Text($"Results: {_results.Count}");
                foreach (var r in _results)
                    ImGui.TextDisabled(r);
            }
        }

        private void Search(FileNode root, System.Func<FileNode, bool> predicate)
        {
            _results.Clear();
            foreach (var node in root.FindAll(predicate))
                _results.Add(node.VirtualPath);

            if (_results.Count == 0)
                _results.Add("(no results)");
        }
    }
}
