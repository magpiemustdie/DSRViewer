using System.Diagnostics;
using DSRViewer.Core;
using DSRViewer.FileProcess;
using DSRViewer.UI.Base;
using ImGuiNET;
using SoulsFormats;

namespace DSRViewer.Editors.Explorer.Tools
{
    /// <summary>Инструмент извлечения файлов из архивов в папку на диске.</summary>
    public class Extractor : ImGuiChild
    {
        private readonly Config _config;
        private readonly Action<string> _onInjectionComplete;

        public Extractor(Config config, Action<string> onInjectionComplete = null)
        {
            _config = config;
            _onInjectionComplete = onInjectionComplete;
        }

        private string _syncStatus = "";
        private bool _syncExpanded = false;
        private FolderSync.SyncResult _lastSync = null;
        private FileNode _syncNode = null; // узел на момент последнего Sync

        /// <summary>Компактный рендер для тулбара фиксированной высоты.</summary>
        public void RenderCompact(FileNode treeRoot, FileNode selected)
        {
            bool hasFolder   = !string.IsNullOrEmpty(_config.ExtractFolder);
            bool hasSelected = selected != null && !string.IsNullOrEmpty(selected.VirtualPath);

            // Строка 1: папка
            if (ImGui.SmallButton("Folder")) _config.SelectExtractFolder();
            ImGui.SameLine();
            if (ImGui.SmallButton("Open"))   OpenExtractFolder(_config.ExtractFolder);
            ImGui.SameLine();
            ImGui.TextDisabled(string.IsNullOrEmpty(_config.ExtractFolder)
                ? "not set" : Path.GetFileName(_config.ExtractFolder));

            // Строка 2: операции
            if (ImGui.SmallButton("Extract"))
            {
                if (hasFolder && hasSelected)
                { var r = MassTextureExtractor.Extract(selected, _config.ExtractFolder, treeRoot); _syncStatus = $"Extracted:{r.Extracted} Fail:{r.Failed}"; _lastSync = null; }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Extract textures from selected node");
            ImGui.SameLine();
            if (ImGui.SmallButton("Inject"))
            {
                if (hasFolder && hasSelected)
                { 
                    var r = MassTextureInjector.InjectSubtree(treeRoot, selected, _config.ExtractFolder, null); 
                    _syncStatus = $"R:{r.Injected} A:{r.Added} C:{r.Created} NF:{r.NotFound}"; 
                    foreach (var p in r.ModifiedArchives) _onInjectionComplete?.Invoke(p); 
                    _lastSync = null; 
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Inject into selected node's archive (subtree only)");
            ImGui.SameLine();
            if (ImGui.SmallButton("Sync"))
            {
                if (hasFolder && hasSelected)
                { _lastSync = FolderSync.Compare(treeRoot, selected, _config.ExtractFolder); _syncNode = selected; _syncStatus = $"M:{_lastSync.ModifiedCount} N:{_lastSync.NewCount} D:{_lastSync.DeletedCount} R:{_lastSync.RenamedCount}"; _syncExpanded = true; }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Compare folder with tree");
            if (!string.IsNullOrEmpty(_syncStatus)) { ImGui.SameLine(); ImGui.TextDisabled(_syncStatus); }
        }

        public void Render(FileNode treeRoot, FileNode selected)
        {
            if (!ImGui.CollapsingHeader("Extractor")) return;

            // ── Папка ────────────────────────────────────────────────────
            if (ImGui.Button("Set folder"))
                _config.SelectExtractFolder();
            ImGui.SameLine();
            if (ImGui.Button("Open"))
                OpenExtractFolder(_config.ExtractFolder);
            ImGui.SameLine();
            ImGui.TextDisabled(string.IsNullOrEmpty(_config.ExtractFolder)
                ? "not set" : _config.ExtractFolder);

            bool hasFolder   = !string.IsNullOrEmpty(_config.ExtractFolder);
            bool hasSelected = selected != null && !string.IsNullOrEmpty(selected.VirtualPath);

            ImGui.Spacing();
            ImGui.Separator();

            // ── Шаг 1: Извлечь ───────────────────────────────────────────
            ImGui.TextDisabled("Step 1 — Extract textures to folder");
            if (ImGui.Button("Extract Selected"))
            {
                if (!hasFolder)        { Console.WriteLine("Extract folder not set!"); }
                else if (!hasSelected) { Console.WriteLine("No node selected!"); }
                else
                {
                    var r = MassTextureExtractor.Extract(selected, _config.ExtractFolder, treeRoot);
                    _syncStatus = $"Extracted: {r.Extracted}  Failed: {r.Failed}";
                    foreach (var e in r.Errors) Console.WriteLine($"  {e}");
                    _lastSync = null;
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Extract all textures from selected archive/folder to the extract folder.\nFiles are named [index]textureName.dds inside archive subfolders.");

            ImGui.SameLine();
            if (ImGui.Button("Extract file"))
            {
                if (!hasFolder) Console.WriteLine("Extract folder not set!");
                else ExtractFile(selected, _config.ExtractFolder);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Extract the single selected file (texture, FLVER, etc.)");

            ImGui.Spacing();
            ImGui.Separator();

            // ── Шаг 2: Редактировать файлы в папке ───────────────────────
            ImGui.TextDisabled("Step 2 — Edit files in folder, then:");

            ImGui.Spacing();
            ImGui.Separator();

            // ── Шаг 3: Синхронизировать ──────────────────────────────────
            ImGui.TextDisabled("Step 3 — Sync: compare folder with archive");
            if (ImGui.Button("Sync"))
            {
                if (!hasFolder)        { Console.WriteLine("Extract folder not set!"); }
                else if (!hasSelected) { Console.WriteLine("No node selected!"); }
                else
                {
                    _lastSync = FolderSync.Compare(treeRoot, selected, _config.ExtractFolder);
                    _syncNode = selected;
                    _syncStatus = $"Modified: {_lastSync.ModifiedCount}  New: {_lastSync.NewCount}  Deleted: {_lastSync.DeletedCount}  Renamed: {_lastSync.RenamedCount}";
                    if (_lastSync.ModifiedCount == 0 && _lastSync.DeletedCount > 0)
                        _syncStatus += "  ⚠ wrong node?";
                    _syncExpanded = true;
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Compare extract folder with archive tree:\n  Modified — file exists in both, bytes may differ\n  New      — file in folder, not in archive → will be added\n  Deleted  — file in archive, not in folder → will be removed\n  Renamed  — file index matches but name changed → will be renamed\n\nSelect a node that contains textures (chrbnd, tpf, folder).");

            if (!string.IsNullOrEmpty(_syncStatus))
            {
                ImGui.TextDisabled(_syncStatus);
                if (_syncNode != null)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"  [node: {_syncNode.Name}]");
                }
            }

            // Список изменений
            if (_lastSync != null && _syncExpanded)
            {
                ImGui.Spacing();
                if (ImGui.SmallButton("Hide")) _syncExpanded = false;
                ImGui.SameLine();

                if (ImGui.SmallButton("Apply Sync"))
                {
                    if (_syncNode == null) { Console.WriteLine("No sync node!"); }
                    else if (selected == null) { Console.WriteLine("No node selected!"); }
                    else if (_syncNode != selected)
                    {
                        Console.WriteLine($"Cannot apply sync: selected node '{selected.Name}' differs from sync node '{_syncNode.Name}'");
                        _syncStatus = $"⚠ Select '{_syncNode.Name}' to apply sync";
                    }
                    else
                    {
                        var r = FolderSync.Apply(_lastSync, _config.ExtractFolder, treeRoot);
                        _syncStatus = $"Added: {r.Injected}  Removed: {r.Removed}  Renamed: {r.Renamed}  Failed: {r.Failed}";
                        foreach (var e in r.Errors) Console.WriteLine($"  {e}");
                        foreach (var p in r.ModifiedArchives) _onInjectionComplete?.Invoke(p);
                        _lastSync = null;
                        _syncNode = null;
                        _syncExpanded = false;
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Apply structural changes only:\n  New     → add to archive\n  Deleted → remove from archive\n  Renamed → rename in archive (bytes unchanged)\n\nModified files are NOT touched here.\nUse 'Inject All' to replace bytes.");

                ImGui.SameLine();

                if (ImGui.SmallButton("Apply + Inject"))
                {
                    if (_syncNode == null) { Console.WriteLine("No sync node!"); }
                    else if (selected == null) { Console.WriteLine("No node selected!"); }
                    else if (_syncNode != selected)
                    {
                        Console.WriteLine($"Cannot apply sync: selected node '{selected.Name}' differs from sync node '{_syncNode.Name}'");
                        _syncStatus = $"⚠ Select '{_syncNode.Name}' to apply sync";
                    }
                    else
                    {
                        var r = FolderSync.Apply(_lastSync, _config.ExtractFolder, treeRoot);
                        Console.WriteLine($"[Apply] Added: {r.Injected}  Removed: {r.Removed}  Renamed: {r.Renamed}  Failed: {r.Failed}");
                        foreach (var e in r.Errors) Console.WriteLine($"  {e}");
                        
                        var injectResult = MassTextureInjector.InjectSubtree(treeRoot, _syncNode, _config.ExtractFolder,
                            msg => Console.WriteLine($"[Inject] {msg}"));
                        
                        _syncStatus = $"Applied: +{r.Injected} -{r.Removed} ~{r.Renamed} | Injected: {injectResult.Injected} replaced, {injectResult.Added} added";
                        if (r.Failed > 0 || injectResult.Failed > 0)
                            _syncStatus += $" | Failed: {r.Failed + injectResult.Failed}";
                        
                        foreach (var e in injectResult.Errors) Console.WriteLine($"  {e}");
                        foreach (var p in r.ModifiedArchives) _onInjectionComplete?.Invoke(p);
                        foreach (var p in injectResult.ModifiedArchives) _onInjectionComplete?.Invoke(p);
                        
                        _lastSync = null;
                        _syncNode = null;
                        _syncExpanded = false;
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Apply ALL changes (structural + bytes):\n  1. Apply Sync (New/Deleted/Renamed)\n  2. Inject All (replace bytes of Modified files)\n\nThis is slower but convenient for full sync.");

                ImGui.BeginChild("##sync_list", new System.Numerics.Vector2(-1, 120),
                    ImGuiChildFlags.Borders);
                foreach (var entry in _lastSync?.Entries ?? [])
                {
                    var color = entry.Status switch
                    {
                        FolderSync.FileStatus.New      => new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f),
                        FolderSync.FileStatus.Deleted  => new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f),
                        FolderSync.FileStatus.Modified => new System.Numerics.Vector4(1f, 0.9f, 0.3f, 1f),
                        FolderSync.FileStatus.Renamed  => new System.Numerics.Vector4(0.4f, 0.8f, 1f, 1f),
                        _                              => new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1f)
                    };
                    string label = entry.Status switch
                    {
                        FolderSync.FileStatus.Renamed  => $"[Renamed ] {FolderSync.StripLeadingBrackets(Path.GetFileNameWithoutExtension(entry.RelativePath))}  (was: {entry.RenamedFromName ?? entry.RenamedFromPath?.Split('|').Last()})",
                        FolderSync.FileStatus.Deleted  => $"[Deleted ] {Path.GetFileName(entry.VirtualPath?.Split('|')[0] ?? entry.RelativePath)}|{entry.VirtualPath?.Split('|').Last()}",
                        FolderSync.FileStatus.New      => $"[New     ] {entry.RelativePath}",
                        FolderSync.FileStatus.Modified => $"[Modified] {entry.RelativePath}",
                        _                              => $"[{entry.Status}] {entry.RelativePath}"
                    };
                    ImGui.TextColored(color, label);
                }
                ImGui.EndChild();
            }

            ImGui.Spacing();
            ImGui.Separator();

            // ── Шаг 4: Инжектировать ─────────────────────────────────────
            ImGui.TextDisabled("Step 4 — Inject: replace bytes in archive");
            if (ImGui.Button("Inject All"))
            {
                if (!hasFolder)        { Console.WriteLine("Extract folder not set!"); }
                else if (!hasSelected) { Console.WriteLine("No node selected!"); }
                else
                {
                    var r = MassTextureInjector.InjectSubtree(treeRoot, selected, _config.ExtractFolder,
                        msg => Console.WriteLine($"[MassInject] {msg}"));
                    _syncStatus = $"Replaced: {r.Injected}  Added: {r.Added}  Created: {r.Created}  NotFound: {r.NotFound}  Failed: {r.Failed}";
                    foreach (var e in r.Errors) Console.WriteLine($"  {e}");
                    foreach (var p in r.ModifiedArchives) _onInjectionComplete?.Invoke(p);
                    _lastSync = null;
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Inject ALL files from folder into archive:\n  [idx]name.dds → replace texture by index\n  name.dds      → add new texture\n\nOnly injects files matching selected subtree.\nRuns after Apply Sync to also replace bytes of renamed textures.\nSafe to run multiple times.");
        }

        /// <summary>Извлекает выбранный файл в указанную папку.</summary>
        public static void ExtractFile(FileNode selected, string outputDir)
        {
            var binder = new FileBinders();
            binder.ProcessPaths([selected.VirtualPath], new FileOperation { GetObject = true });
            var obj = binder.GetObject();

            string fileName = Path.GetFileName(selected.Name);

            switch (obj)
            {
                case FlverWithFallback fb:
                    fb.Flver.Write(Path.Combine(outputDir, fileName));
                    break;
                case BinderFile file:
                    File.WriteAllBytes(Path.Combine(outputDir, fileName), file.Bytes);
                    break;
                case TPF.Texture texture:
                    File.WriteAllBytes(Path.Combine(outputDir, fileName + ".dds"), texture.Bytes);
                    break;
                case FLVER2 flver:
                    flver.Write(Path.Combine(outputDir, fileName));
                    break;
                case TPF tpf:
                    tpf.Write(Path.Combine(outputDir, fileName));
                    break;
                case BND bnd:
                    bnd.Write(Path.Combine(outputDir, fileName));
                    break;
                case byte[] bytes:
                    File.WriteAllBytes(Path.Combine(outputDir, fileName), bytes);
                    break;
                default:
                    Console.WriteLine($"[Extractor] Unknown object type for: {selected.VirtualPath}");
                    break;
            }
        }

        private static void OpenExtractFolder(string folder)
        {
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                Process.Start("explorer.exe", folder);
        }
    }
}
