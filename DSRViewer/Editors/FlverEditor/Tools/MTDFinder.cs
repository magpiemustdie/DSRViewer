using DSRViewer.FileProcess;
using DSRViewer.Editors.MTDEditor;
using DSRViewer.UI.Base;
using ImGuiNET;
using SoulsFormats;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace DSRViewer.Editors.FlverEditor.Tools
{
    /// <summary>Окно поиска MTD и текстур в загруженных FLVER-файлах с экспортом в CSV.</summary>
    public class FlverMTDFinder : ImGuiWindow
    {
        string _mtdNameFinder = string.Empty;
        FlverTools _flverTools = new();
        // Кэш для кнопок "mtd & tex" и "tex & mtd" — вычисляется один раз
        Dictionary<string, HashSet<string>> _cachedMatToTex = null;
        List<FileNode> _cachedFileList = null;

        public FlverMTDFinder(string windowName, bool showWindow) : base(windowName, showWindow)
        {
            _windowFlags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        }

        private static List<string> GetVirtualPaths(List<FileNode> nodes) =>
            nodes.Where(n => n.VirtualPath != null).Select(n => n.VirtualPath).ToList();

        private static Dictionary<string, HashSet<string>> BuildMaterialToTextures(List<FileNode> flverFileList)
        {
            var result = new Dictionary<string, HashSet<string>>();
            var binder = new FileBinders();
            var op = new FileOperation
            {
                UseFlverDelegate = true,
                AdditionalFlverProcessing = (flver, _, __, ___) =>
                {
                    foreach (var mat in flver.Materials)
                    {
                        if (string.IsNullOrEmpty(mat.MTD)) continue;
                        string matName = mat.MTD.Split('\\').Last().ToLower();
                        if (!result.ContainsKey(matName)) result[matName] = [];
                        foreach (var tex in mat.Textures)
                            if (!string.IsNullOrEmpty(tex.Path))
                                result[matName].Add(tex.Path.Split('\\').Last().ToLower());
                    }
                }
            };
            binder.ProcessPaths(GetVirtualPaths(flverFileList), op);
            return result;
        }

        private static void WriteCsv(string path, string header, IEnumerable<string> rows)
        {
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            writer.WriteLine(header);
            foreach (var row in rows) writer.WriteLine(row);
            Console.WriteLine($"Done, saved to {path}");
        }

        private void WriteTexMwCsv(List<FileNode> flverFileList, List<MTDShortDetails> mtds,
            string csvPath, string? diffuseFilter)
        {
            var matToMw = mtds
                .GroupBy(m => m.Name.Split('\\').Last().ToLower())
                .ToDictionary(g => g.Key, g => g.First().MW);

            var texToMw = new Dictionary<string, HashSet<int>>();
            var binder = new FileBinders();
            var op = new FileOperation
            {
                UseFlverDelegate = true,
                AdditionalFlverProcessing = (flver, _, __, ___) =>
                {
                    foreach (var mat in flver.Materials)
                    {
                        if (string.IsNullOrEmpty(mat.MTD)) continue;
                        string matName = mat.MTD.Split('\\').Last().ToLower();
                        if (!matToMw.TryGetValue(matName, out int mw)) continue;
                        foreach (var tex in mat.Textures)
                        {
                            if (string.IsNullOrEmpty(tex.Path)) continue;
                            if (diffuseFilter != null &&
                                tex.ParamName != diffuseFilter &&
                                tex.ParamName != diffuseFilter + "_2") continue;
                            string texName = tex.Path.Split('\\').Last().ToLower();
                            if (!texToMw.ContainsKey(texName)) texToMw[texName] = [];
                            texToMw[texName].Add(mw);
                        }
                    }
                }
            };
            binder.ProcessPaths(GetVirtualPaths(flverFileList), op);
            WriteCsv(csvPath, "Texture,MW_Type",
                texToMw.Select(kvp => $"{kvp.Key},{string.Join(",", kvp.Value.OrderBy(v => v))}"));
        }

        /// <summary>Отображает окно поиска MTD и выполняет операции поиска/экспорта.</summary>
        public void Render(List<FileNode> flverFileList, List<MTDShortDetails> mtds)
        {
            if (!_showWindow) return;

            ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
            // Без BeginChild — контент напрямую в окне, нет двойного скроллбара
            ImGui.SetNextItemWidth(300);
            ImGui.InputText("mtd_finder", ref _mtdNameFinder, 255);

            if (ImGui.Button("Find MTD"))
            {
                var modelList = new List<string>();
                var binder = new FileBinders();
                var op = new FileOperation
                {
                    UseFlverDelegate = true,
                    AdditionalFlverProcessing = (flver, virtualPath, _, __) =>
                    {
                        if (_flverTools.MTDFinder(flver.Materials, _mtdNameFinder))
                        {
                            modelList.Add(virtualPath);
                            Console.WriteLine($"MTD Found -> : {virtualPath}");
                        }
                    }
                };
                binder.ProcessPaths(GetVirtualPaths(flverFileList), op);
                File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "MTDs.txt"), modelList);
            }

            if (ImGui.Button("Find All MTD"))
            {
                var items = new List<string>();
                var binder = new FileBinders();
                var op = new FileOperation
                {
                    UseFlverDelegate = true,
                    AdditionalFlverProcessing = (flver, _, __, ___) => _flverTools.MTDFinderAll(flver.Materials, items)
                };
                binder.ProcessPaths(GetVirtualPaths(flverFileList), op);
                var counts = items.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
                File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "MTDCount.txt"), counts.Select(kvp => $"{kvp.Key}; {kvp.Value}"));
                Console.WriteLine("Done, Saved in MTDCount.txt");
            }

            if (ImGui.Button("Find all textures"))
            {
                var items = new List<string>();
                var binder = new FileBinders();
                var op = new FileOperation
                {
                    UseFlverDelegate = true,
                    AdditionalFlverProcessing = (flver, _, __, ___) => _flverTools.TexFinderAll(flver.Materials, items)
                };
                binder.ProcessPaths(GetVirtualPaths(flverFileList), op);
                var counts = items.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
                File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "FlverTexCount.txt"), counts.Select(kvp => $"{kvp.Key}; {kvp.Value}"));
                Console.WriteLine("Done, Saved in FlverTexCount.txt");
            }

            if (ImGui.Button("Find all mtd & tex"))
            {
                _cachedMatToTex = BuildMaterialToTextures(flverFileList);
                _cachedFileList = flverFileList;
                WriteCsv(Path.Combine(AppContext.BaseDirectory, "MaterialTextures.csv"), "Material,Textures",
                    _cachedMatToTex.Select(kvp => $"{kvp.Key},{string.Join(", ", kvp.Value)}"));
            }

            if (ImGui.Button("Find all tex & mtd"))
            {
                // Переиспользуем кэш если список не изменился
                var matToTex = (_cachedFileList == flverFileList && _cachedMatToTex != null)
                    ? _cachedMatToTex
                    : BuildMaterialToTextures(flverFileList);

                var texToMat = new Dictionary<string, HashSet<string>>();
                foreach (var kvp in matToTex)
                    foreach (var tex in kvp.Value)
                    {
                        if (!texToMat.ContainsKey(tex)) texToMat[tex] = [];
                        texToMat[tex].Add(kvp.Key);
                    }
                WriteCsv(Path.Combine(AppContext.BaseDirectory, "MaterialTextures.csv"), "Texture,Materials",
                    texToMat.Select(kvp => $"{kvp.Key},{string.Join(", ", kvp.Value)}"));
            }

            if (ImGui.Button("Find all tex & mw"))
                WriteTexMwCsv(flverFileList, mtds, Path.Combine(AppContext.BaseDirectory, "MaterialTexturesMW.csv"), null);

            if (ImGui.Button("Find all diffuse tex & mw"))
                WriteTexMwCsv(flverFileList, mtds, Path.Combine(AppContext.BaseDirectory, "MaterialTexturesDiffMW.csv"), "g_Diffuse");

            ImGui.End();
        }
    }
}
