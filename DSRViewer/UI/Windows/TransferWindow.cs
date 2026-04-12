using DSRViewer.Core;
using DSRViewer.Core.Transfer;
using DSRViewer.FileProcess;
using DSRViewer.Editors.Explorer;
using DSRViewer.UI.Base;
using ImGuiNET;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;

namespace DSRViewer.UI.Windows
{
    /// <summary>Окно переноса файлов между двумя проводниками с поддержкой авто-переноса текстур.</summary>
    public class TransferFilesWindow : ImGuiWindow
    {
        public List<ExplorerWindow> ExplorerWindows { get; set; }

        private int _sourceExplorerIndex = 0;
        private int _targetExplorerIndex = 1;

        private List<FileItem> _sourceFiles = new();
        private List<FileItem> _targetFiles = new();

        private FileItem _selectedSourceFile;
        private FileItem _selectedTargetFile;

        private bool _useSourceFileName = false;

        // Auto transfer
        private string _sourceTexListPath = "";
        private string _targetTexListPath = "";
        private bool _autoTransferDryRun = true;
        private bool _autoTransferExpanded = false;
        private bool _useFlverIndex = false;   // FLVER-режим: только для случаев когда имена текстур совпадают между PTDE и DSR
        private string _autoTransferStatus = "";

        private class FileItem
        {
            public string RootPath { get; set; }
            public string VirtualPath { get; set; }
            public string Name { get; set; }
            public NodeType Type { get; set; }
            public FileNode Node { get; set; }
            public ExplorerWindow SourceWindow { get; set; }
            public TreeChild SourceTree { get; set; }
        }

        public TransferFilesWindow(string windowName, bool showWindow) : base(windowName, showWindow)
        {
            _minSize = new Vector2(700, 500);
            _maxSize = new Vector2(float.MaxValue, float.MaxValue);
        }

        public override void Render()
        {
            if (!_showWindow) return;

            ApplySizeConstraints();
            ImGui.Begin(_windowName, ref _showWindow, ImGuiWindowFlags.NoDocking);

            if (ExplorerWindows == null || ExplorerWindows.Count == 0)
            {
                ImGui.Text("No explorer windows open.");
                ImGui.End();
                return;
            }

            _sourceExplorerIndex = Math.Clamp(_sourceExplorerIndex, 0, ExplorerWindows.Count - 1);
            _targetExplorerIndex = Math.Clamp(_targetExplorerIndex, 0, ExplorerWindows.Count - 1);
            if (_sourceExplorerIndex == _targetExplorerIndex && ExplorerWindows.Count > 1)
                _targetExplorerIndex = (_sourceExplorerIndex + 1) % ExplorerWindows.Count;

            _sourceFiles = BuildFileItemsForExplorer(ExplorerWindows[_sourceExplorerIndex]);
            _targetFiles = BuildFileItemsForExplorer(ExplorerWindows[_targetExplorerIndex]);

            var explorerNames = ExplorerWindows.Select(w => w.GetWindowName()).ToArray();

            ImGui.Separator();

            if (_selectedSourceFile != null && _selectedTargetFile != null)
            {
                bool sameType = _selectedSourceFile.Node.Type == _selectedTargetFile.Node.Type;

                if (ImGui.Button("Transfer file (replace target)"))
                {
                    if (sameType) TransferFile(_selectedSourceFile, _selectedTargetFile);
                    else Console.WriteLine("Please select files of the same type");
                }
                ImGui.SameLine();
                if (ImGui.RadioButton("Use source name", _useSourceFileName))
                    _useSourceFileName = !_useSourceFileName;

                ImGui.Text($"Replace '{_selectedTargetFile.VirtualPath}' with '{_selectedSourceFile.VirtualPath}'");
                ImGui.Text($"Type: '{_selectedTargetFile.Node.Type}' <- '{_selectedSourceFile.Node.Type}'");

                if (ImGui.Button("Transfer env"))
                {
                    if (sameType) TransferEnv(_selectedSourceFile, _selectedTargetFile);
                    else Console.WriteLine("Please select files of the same type");
                }
            }
            else
            {
                ImGui.Text("Select source and target files to enable transfer.");
            }

            // Резервируем место под AutoTransfer внизу: свёрнут — 28px, раскрыт — ~260px
            float autoTransferHeight = _autoTransferExpanded ? 260f : 28f;

            if (ImGui.BeginTable("TransferTable", 2, ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableNextColumn();
                ImGui.Text("Source Explorer");
                ImGui.SameLine();
                if (ImGui.Combo("##SourceExplorer", ref _sourceExplorerIndex, explorerNames, explorerNames.Length))
                {
                    _selectedSourceFile = null;
                    _sourceFiles = BuildFileItemsForExplorer(ExplorerWindows[_sourceExplorerIndex]);
                }
                ImGui.Separator();
                float srcH = ImGui.GetContentRegionAvail().Y - autoTransferHeight - 12f;
                if (srcH < 80) srcH = 80;
                if (ImGui.BeginChild("SourceFileList", new Vector2(-1, srcH), ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar))
                    RenderFileList(ref _sourceFiles, ref _selectedSourceFile, isSource: true);
                ImGui.EndChild();

                ImGui.TableNextColumn();
                ImGui.Text("Target Explorer");
                ImGui.SameLine();
                if (ImGui.Combo("##TargetExplorer", ref _targetExplorerIndex, explorerNames, explorerNames.Length))
                {
                    _selectedTargetFile = null;
                    _targetFiles = BuildFileItemsForExplorer(ExplorerWindows[_targetExplorerIndex]);
                }
                ImGui.Separator();
                float dstH = ImGui.GetContentRegionAvail().Y - autoTransferHeight - 12f;
                if (dstH < 80) dstH = 80;
                if (ImGui.BeginChild("TargetFileList", new Vector2(-1, dstH), ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar))
                    RenderFileList(ref _targetFiles, ref _selectedTargetFile, isSource: false);
                ImGui.EndChild();

                ImGui.EndTable();
            }

            ImGui.Separator();
            RenderTools();
            ImGui.Separator();
            RenderAutoTransfer();

            ImGui.End();
        }

        private void RenderFileList(ref List<FileItem> files, ref FileItem selectedItem, bool isSource)
        {
            if (files == null || files.Count == 0)
            {
                ImGui.Text("No selected files in this explorer.");
                return;
            }

            foreach (var group in files.GroupBy(f => f.RootPath).OrderBy(g => g.Key))
            {
                if (!ImGui.TreeNode($"{group.Key}##{group.Key}_{(isSource ? "src" : "dst")}")) continue;

                foreach (var file in group.OrderBy(f => f.VirtualPath))
                {
                    bool isSelected = selectedItem != null
                        && selectedItem.RootPath == file.RootPath
                        && selectedItem.VirtualPath == file.VirtualPath;

                    if (ImGui.Selectable($"{file.VirtualPath}##{file.RootPath}{file.VirtualPath}", isSelected))
                        selectedItem = file;

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"Full name: {file.Name}");
                }
                ImGui.TreePop();
            }
        }

        private List<FileItem> BuildFileItemsForExplorer(ExplorerWindow explorer)
        {
            var items = new List<FileItem>();
            var treeChildren = explorer.GetTreeChildList();
            if (treeChildren == null) return items;

            foreach (var tree in treeChildren)
            {
                var node = tree.Selected;
                if (node == null || string.IsNullOrEmpty(node.VirtualPath)) continue;

                items.Add(new FileItem
                {
                    RootPath = tree.RootFilePath ?? "Unknown",
                    VirtualPath = node.VirtualPath,
                    Name = node.Name,
                    Type = node.Type,
                    Node = node,
                    SourceWindow = explorer,
                    SourceTree = tree
                });
            }
            return items;
        }

        private void TransferFile(FileItem source, FileItem target)
        {
            // Защита от переноса файла в самого себя
            if (string.Equals(source.VirtualPath, target.VirtualPath, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Transfer] Skipped: source == target ({source.VirtualPath})");
                return;
            }

            try
            {
                byte[] data = ExtractFileBytes(source.Node);
                if (data == null)
                {
                    Console.WriteLine($"[Transfer] Failed to extract bytes from: {source.VirtualPath}");
                    return;
                }

                var injector = target.SourceTree.Injector;
                string name = _useSourceFileName ? source.Name : target.Name;
                bool success = injector.InjectBytes(target.SourceTree.Root, target.Node, data, name);

                if (success)
                {
                    injector.OnInjectionComplete?.Invoke(target.Node.VirtualPath);
                    Debug.WriteLine($"Transfer OK: {source.VirtualPath} -> {target.VirtualPath}");
                }
                else
                {
                    Console.WriteLine($"[Transfer] Inject failed: {target.VirtualPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Transfer] Error: {ex.Message}");
            }
        }

        private void TransferEnv(FileItem source, FileItem target)
        {
            // Защита от переноса в себя
            if (string.Equals(source.SourceTree?.RootFilePath, target.SourceTree?.RootFilePath,
                StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[Transfer] Skipped TransferEnv: source and target are the same archive");
                return;
            }

            // Ищем DDS в выбранном узле, а если пусто — в корне вкладки
            // (текстуры могут быть в соседнем архиве, например BD_A_9560_M)
            var ddsFiles = source.Node.FindAll(n => n.IsNestedDDS);
            if (ddsFiles.Count == 0 && source.SourceTree?.Root != null)
            {
                ddsFiles = source.SourceTree.Root.FindAll(n => n.IsNestedDDS);
                if (ddsFiles.Count > 0)
                    Console.WriteLine($"[Transfer] No DDS in selected node, searching root: found {ddsFiles.Count}");
            }

            foreach (var file in ddsFiles)
            {
                // Сначала ищем в выбранном целевом узле
                bool found = false;
                MatchAndTransferWithResult(target.Node, file, source, target, ref found);

                // Если не нашли — ищем в корне целевой вкладки
                if (!found && target.SourceTree?.Root != null)
                    MatchAndTransferInRoot(target.SourceTree.Root, file, source, target);
            }
        }

        private void MatchAndTransferWithResult(FileNode targetNode, FileNode sourceFile, FileItem source, FileItem target, ref bool found, int depth = 0)
        {
            if (depth > 20 || found)
            {
                if (depth > 20) Console.WriteLine($"[Transfer] Max depth reached at {targetNode.VirtualPath}");
                return;
            }

            targetNode.EnsureLoaded();
            foreach (var file in targetNode.Children)
            {
                if (file.Name.Equals(sourceFile.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Found target file: {file.VirtualPath}");
                    TransferFile(
                        new FileItem { VirtualPath = sourceFile.VirtualPath, Name = sourceFile.Name, Type = sourceFile.Type, Node = sourceFile, SourceTree = source.SourceTree },
                        new FileItem { VirtualPath = file.VirtualPath, Name = file.Name, Type = file.Type, Node = file, SourceTree = target.SourceTree }
                    );
                    found = true;
                    return;
                }

                MatchAndTransferWithResult(file, sourceFile, source, target, ref found, depth + 1);
                if (found) return;
            }
        }

        /// <summary>
        /// Ищет текстуру по имени в дереве начиная с targetRoot.
        /// Используется когда текстуры могут быть в соседних архивах (_M и т.д.).
        /// </summary>
        private void MatchAndTransferInRoot(FileNode targetRoot, FileNode sourceFile, FileItem source, FileItem target)
        {
            var found = targetRoot.FindFirst(n =>
                n.IsNestedDDS &&
                n.Name.Equals(sourceFile.Name, StringComparison.OrdinalIgnoreCase));

            if (found != null)
            {
                Console.WriteLine($"Found target file (root search): {found.VirtualPath}");
                TransferFile(
                    new FileItem { VirtualPath = sourceFile.VirtualPath, Name = sourceFile.Name, Type = sourceFile.Type, Node = sourceFile, SourceTree = source.SourceTree },
                    new FileItem { VirtualPath = found.VirtualPath, Name = found.Name, Type = found.Type, Node = found, SourceTree = target.SourceTree }
                );
            }
        }

        private static byte[] ExtractFileBytes(FileNode node)
        {
            var binder = new FileBinders();
            binder.ProcessPaths([node.VirtualPath], new FileOperation { GetObject = true });

            return binder.GetObject() switch
            {
                FlverWithFallback fb  => fb.WriteOrFallback(e => Console.WriteLine($"[Transfer] FLVER write error: {e}")),
                BinderFile file       => file.Bytes,
                TPF.Texture texture   => texture.Bytes,
                FLVER2 flver          => flver.Write(),
                TPF tpf               => tpf.Write(),
                BND bnd               => bnd.Write(),
                byte[] bytes          => bytes,
                _                     => null
            };
        }

        private void RenderTools()
        {
            if (!ImGui.CollapsingHeader("Tools")) return;

            if (ImGui.Button("Compare texture lists..."))
                RunTextureListCompare();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Compare two TexturesList.txt files and save a diff report");

            ImGui.SameLine();

            if (ImGui.Button("Scan FLVER textures..."))
                RunFlverTextureScan();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Scan all FLVERs in open explorer tabs and report where each texture comes from");
        }

        private static void RunTextureListCompare()
        {
            string pathA = DialogHelper.SelectFile("Select first texture list (A)", "Text files|*.txt|All files|*.*");
            if (string.IsNullOrEmpty(pathA)) return;
            string pathB = DialogHelper.SelectFile("Select second texture list (B)", "Text files|*.txt|All files|*.*");
            if (string.IsNullOrEmpty(pathB)) return;

            try
            {
                var result = TextureListComparer.Compare(pathA, pathB);
                string reportPath = Path.Combine(
                    Path.GetDirectoryName(pathA) ?? "",
                    $"TextureCompare_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                TextureListComparer.WriteReport(result, reportPath);
                Process.Start("explorer.exe", $"/select,\"{reportPath}\"");
            }
            catch (Exception ex) { Console.WriteLine($"[TextureListComparer] Error: {ex.Message}"); }
        }

        private void RunFlverTextureScan()
        {
            if (ExplorerWindows == null || ExplorerWindows.Count == 0)
            { Console.WriteLine("[FlverTextureScan] No explorer windows open."); return; }

            string texListPath = DialogHelper.SelectFile(
                "Select TexturesList.txt (texture index)", "Text files|*.txt|All files|*.*");
            if (string.IsNullOrEmpty(texListPath)) return;

            try
            {
                var texturePaths = FlverTextureScanner.LoadTextureList(texListPath);
                var allMatches   = new List<FlverTextureScanner.TextureMatch>();
                var allNotFound  = new List<string>();

                foreach (var explorer in ExplorerWindows)
                    foreach (var tree in explorer.GetTreeChildList())
                    {
                        var r = FlverTextureScanner.Scan(tree.Root, texturePaths);
                        allMatches.AddRange(r.Matches);
                        allNotFound.AddRange(r.NotFound);
                    }

                string reportPath = Path.Combine(
                    Path.GetDirectoryName(texListPath) ?? "",
                    $"FlverTextureScan_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                FlverTextureScanner.WriteReport(
                    new FlverTextureScanner.ScanResult(allMatches, allNotFound), reportPath);
                Process.Start("explorer.exe", $"/select,\"{reportPath}\"");
            }
            catch (Exception ex) { Console.WriteLine($"[FlverTextureScan] Error: {ex.Message}"); }
        }

        private void RenderAutoTransfer()
        {
            _autoTransferExpanded = ImGui.CollapsingHeader("Auto Transfer (texture matching)", ImGuiTreeNodeFlags.DefaultOpen);
            if (!_autoTransferExpanded) return;
            ImGui.TextDisabled("Matches textures from source game to target game by name, archive type and fuzzy rules.");
            ImGui.Spacing();

            // Выбор TexturesList.txt для источника
            ImGui.Text("Source texture list (e.g. PTDE):");
            ImGui.SameLine();
            ImGui.TextDisabled(string.IsNullOrEmpty(_sourceTexListPath)
                ? "not set" : Path.GetFileName(_sourceTexListPath));
            ImGui.SameLine();
            if (ImGui.Button("Browse##src"))
            {
                string p = DialogHelper.SelectFile("Select source TexturesList.txt", "Text files|*.txt|All files|*.*");
                if (!string.IsNullOrEmpty(p)) _sourceTexListPath = p;
            }
            ImGui.SameLine();
            if (ImGui.Button("Export from source##src"))
                _sourceTexListPath = ExportTexList(ExplorerWindows[_sourceExplorerIndex], "source");

            // Выбор TexturesList.txt для цели
            ImGui.Text("Target texture list (e.g. DSR): ");
            ImGui.SameLine();
            ImGui.TextDisabled(string.IsNullOrEmpty(_targetTexListPath)
                ? "not set" : Path.GetFileName(_targetTexListPath));
            ImGui.SameLine();
            if (ImGui.Button("Browse##dst"))
            {
                string p = DialogHelper.SelectFile("Select target TexturesList.txt", "Text files|*.txt|All files|*.*");
                if (!string.IsNullOrEmpty(p)) _targetTexListPath = p;
            }
            ImGui.SameLine();
            if (ImGui.Button("Export from target##dst"))
                _targetTexListPath = ExportTexList(ExplorerWindows[_targetExplorerIndex], "target");

            ImGui.Spacing();
            ImGui.Checkbox("Dry run (report only, no actual transfer)", ref _autoTransferDryRun);
            ImGui.Checkbox("Use FLVER index (experimental, may cause wrong transfers)", ref _useFlverIndex);
            if (_useFlverIndex)
                ImGui.TextDisabled("  Requires open explorer tabs with loaded archives.");
            ImGui.Spacing();

            bool canRun = !string.IsNullOrEmpty(_sourceTexListPath)
                       && !string.IsNullOrEmpty(_targetTexListPath);

            if (!canRun)
            {
                ImGui.TextDisabled("Set both texture lists to enable.");
                return;
            }

            if (ImGui.Button(_autoTransferDryRun ? "Analyse (dry run)" : "Transfer textures!"))
            {
                RunAutoTransfer();
            }

            ImGui.SameLine();
            if (ImGui.Button("Analyze index stats"))
            {
                RunAnalyzer();
            }

            if (!string.IsNullOrEmpty(_autoTransferStatus))
            {
                ImGui.Spacing();
                ImGui.TextWrapped(_autoTransferStatus);
            }
        }

        /// <summary>Экспортирует TexturesList.txt из всех вкладок проводника и возвращает путь к файлу.</summary>
        private static string ExportTexList(ExplorerWindow explorer, string label)
        {
            try
            {
                var lines = new List<string>();
                foreach (var tree in explorer.GetTreeChildList())
                {
                    var texNodes = tree.Root.FindAll(n => n.IsDDS);
                    lines.AddRange(texNodes.Select(n => $"{n.Name}; {n.VirtualPath}"));
                }

                string path = Path.Combine(
                    AppContext.BaseDirectory,
                    $"{explorer.GetWindowName()}_{label}_TexturesList.txt");
                File.WriteAllLines(path, lines);
                Console.WriteLine($"[ExportTexList] {lines.Count} textures → {path}");
                return path;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExportTexList] Error: {ex.Message}");
                return "";
            }
        }

        private void RunAutoTransfer()        {
            try
            {
                _autoTransferStatus = "Building indexes...";

                string baseName = Path.Combine(
                    Path.GetDirectoryName(_targetTexListPath) ?? "",
                    $"AutoTransfer_{DateTime.Now:yyyyMMdd_HHmmss}");

                string reportPath = baseName + ".txt";
                string csvPath    = baseName + ".csv";

                TextureAutoTransfer.TransferResult result;

                if (_useFlverIndex && ExplorerWindows?.Count >= 2)
                {
                    // Точный режим: сканируем FLVER из открытых проводников
                    var srcTexList = FlverTextureScanner.LoadTextureListMulti(_sourceTexListPath);
                    var dstTexList = FlverTextureScanner.LoadTextureListMulti(_targetTexListPath);

                    _autoTransferStatus = "Scanning FLVERs in source explorer...";
                    var srcRoots = ExplorerWindows[_sourceExplorerIndex]
                        .GetTreeChildList().Select(t => t.Root).ToList();
                    var srcIndex = new Dictionary<(string, string), List<FlverTextureScanner.FlverTexEntry>>();
                    foreach (var root in srcRoots)
                        foreach (var kv in FlverTextureScanner.BuildFlverIndex(root, srcTexList))
                        {
                            if (!srcIndex.ContainsKey(kv.Key)) srcIndex[kv.Key] = new();
                            srcIndex[kv.Key].AddRange(kv.Value);
                        }

                    _autoTransferStatus = "Scanning FLVERs in target explorer...";
                    var dstRoots = ExplorerWindows[_targetExplorerIndex]
                        .GetTreeChildList().Select(t => t.Root).ToList();
                    var dstIndex = new Dictionary<(string, string), List<FlverTextureScanner.FlverTexEntry>>();
                    foreach (var root in dstRoots)
                        foreach (var kv in FlverTextureScanner.BuildFlverIndex(root, dstTexList))
                        {
                            if (!dstIndex.ContainsKey(kv.Key)) dstIndex[kv.Key] = new();
                            dstIndex[kv.Key].AddRange(kv.Value);
                        }

                    _autoTransferStatus = $"Source: {srcIndex.Count} slots, Target: {dstIndex.Count} slots. Running...";
                    result = TextureAutoTransfer.RunWithFlverIndex(srcIndex, dstIndex,
                        dryRun: _autoTransferDryRun,
                        onProgress: msg => Console.WriteLine($"[AutoTransfer] {msg}"));
                }
                else
                {
                    // Базовый режим: по именам из TexturesList.txt
                    var sourceIndex = TextureAutoTransfer.BuildIndex(_sourceTexListPath);
                    var targetIndex = TextureAutoTransfer.BuildIndex(_targetTexListPath);
                    _autoTransferStatus = $"Source: {sourceIndex.Count}, Target: {targetIndex.Count}. Running...";
                    result = TextureAutoTransfer.Run(sourceIndex, targetIndex,
                        dryRun: _autoTransferDryRun,
                        onProgress: msg => Console.WriteLine($"[AutoTransfer] {msg}"));
                }

                TextureAutoTransfer.WriteReport(result, reportPath);
                TextureAutoTransfer.WriteCsv(result, csvPath);

                int found = result.Transferred.Count + result.Skipped.Count;
                _autoTransferStatus = _autoTransferDryRun
                    ? $"Dry run: found {found}, not found {result.NotFound.Count}. Report + CSV saved."
                    : $"Done: transferred {result.Transferred.Count}, not found {result.NotFound.Count}. Report + CSV saved.";

                Process.Start("explorer.exe", $"/select,\"{csvPath}\"");
            }
            catch (Exception ex)
            {
                _autoTransferStatus = $"Error: {ex.Message}";
                Console.WriteLine($"[AutoTransfer] {ex.Message}");
            }
        }

        private void RunAnalyzer()
        {
            try
            {
                _autoTransferStatus = "Analyzing...";

                var result = TextureAutoTransferAnalyzer.Run(_sourceTexListPath, _targetTexListPath, verbose: true);

                string csvPath = Path.Combine(
                    Path.GetDirectoryName(_targetTexListPath) ?? "",
                    $"AnalyzerReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                TextureAutoTransferAnalyzer.SaveCsv(result, csvPath);

                _autoTransferStatus =
                    $"Exact: {result.Exact}  ExactAny: {result.ExactAnyArchive}  Fuzzy: {result.FuzzyObject}  NotFound: {result.NotFound}  " +
                    $"({result.NotFound * 100.0 / Math.Max(result.TotalTarget, 1):F1}% missing) — CSV saved.";

                Process.Start("explorer.exe", $"/select,\"{csvPath}\"");
            }
            catch (Exception ex)
            {
                _autoTransferStatus = $"Analyzer error: {ex.Message}";
                Console.WriteLine($"[Analyzer] {ex.Message}");
            }
        }
    }
}
