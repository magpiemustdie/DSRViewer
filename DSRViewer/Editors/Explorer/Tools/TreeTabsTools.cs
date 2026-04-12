using DSRViewer.Editors.Explorer.DDSHelper;
using DSRViewer.FileProcess;
using ImGuiNET;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSRViewer.Editors.Explorer.Tools
{
    /// <summary>Инструменты анализа дерева: поиск дублей текстур и ошибок формата.</summary>
    public class TreeTabsTools
    {
        /// <summary>Возвращает все FLVER-узлы из дерева.</summary>
        public List<FileNode> NodeFlverFinder(FileNode root) =>
            root.FindAll(n => n.IsFlver || n.IsNestedFlver);

        /// <summary>Возвращает все DDS-узлы из дерева.</summary>
        public List<FileNode> NodeTexFinder(FileNode root) =>
            root.FindAll(n => n.IsNestedDDS);

        /// <summary>Отображает кнопки анализа дерева (дубли, ошибки формата).</summary>
        public void RenderAnalysisButtons(FileNode root)
        {
            if (root == null) return;
            if (!ImGui.CollapsingHeader("Analysis")) return;

            if (ImGui.Button("Find texture doubles"))
                FindTextureDoubles(root);

            ImGui.SameLine();

            if (ImGui.Button("Find format errors"))
                FindFormatErrors(root);
        }

        private static void FindTextureDoubles(FileNode root)
        {
            var all = root.FindAll(n => n.IsNestedDDS);
            int total = all.Count;

            var grouped = all
                .GroupBy(n => n.Name)
                .Where(g => g.Count() > 1)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join("; ", g.Select(n => $"{n.ShortVirtualPath}[{n.DDSFormatFlag}][{n.DDSFormat}]"))
                );

            string path = Path.Combine(AppContext.BaseDirectory, "Test_Doubles.txt");
            File.WriteAllLines(path, grouped.Select(kvp => $"{kvp.Key}; {kvp.Value}"));
            System.Console.WriteLine($"Total DDS: {total}, unique: {all.Select(n => n.Name).Distinct().Count()}, doubles saved to Test_Doubles.txt");
        }

        private static void FindFormatErrors(FileNode root)
        {
            var errors = new Dictionary<string, string>();

            foreach (var file in root.FindAll(n => n.IsNestedDDS))
            {
                string name = file.Name.ToLower();
                string info = $"{file.ShortVirtualPath}; {file.DDSFormatFlag}; {file.DDSFormat}";

                // Проверка несоответствия флага и формата
                if (DDS_FlagFormatList.DDSFlagList.TryGetValue(file.DDSFormatFlag, out string expectedFmt)
                    && file.DDSFormat != expectedFmt)
                {
                    errors[file.Name] = info;
                    continue;
                }

                bool err = file.DDSFormatFlag switch
                {
                    0 or 1       => name.EndsWith("_n"),
                    5            => name.EndsWith("_s") || name.EndsWith("_n") || name.EndsWith("_t") || name.EndsWith("_h"),
                    24 or 35     => true,
                    36           => !name.EndsWith("_n"),
                    37           => name.EndsWith("_s") || name.EndsWith("_n") || name.EndsWith("_t") || name.EndsWith("_h"),
                    38           => name.EndsWith("_n") || name.EndsWith("_t") || name.EndsWith("_h"),
                    _            => false
                };

                if (err) errors[file.Name] = info;
            }

            string path = Path.Combine(AppContext.BaseDirectory, "Format_Err.txt");
            File.WriteAllLines(path, errors.Select(kvp => $"{kvp.Key}; {kvp.Value}"));
            System.Console.WriteLine($"Format errors: {errors.Count}, saved to Format_Err.txt");
        }
    }
}
