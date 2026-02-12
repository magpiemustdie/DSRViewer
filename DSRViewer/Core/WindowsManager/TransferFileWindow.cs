using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSRViewer.FileHelper.FileExplorer.Render;
using DSRViewer.FileHelper;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using System.Numerics;
using SoulsFormats;
using DSRViewer.FileHelper.FileExplorer.DDSHelper;

namespace DSRViewer.Core.WindowsManager
{
    public class TransferFilesWindow : ImGuiWindow
    {
        public List<ExplorerWindow> ExplorerWindows { get; set; }

        // Состояние для левого (source) и правого (target) списков
        private int _sourceExplorerIndex = 0;
        private int _targetExplorerIndex = 1;

        private List<FileItem> _sourceFiles = new();
        private List<FileItem> _targetFiles = new();

        private FileItem _selectedSourceFile;
        private FileItem _selectedTargetFile;

        private bool _useSourceFileName = false;

        // Вспомогательный класс для хранения информации о файле
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

        public TransferFilesWindow(string windowName, bool showWindow)
        {
            _windowName = windowName;
            _showWindow = showWindow;
        }

        public TransferFilesWindow(string windowName, bool showWindow, List<ExplorerWindow> explorerWindows)
            : this(windowName, showWindow)
        {
            ExplorerWindows = explorerWindows;
        }

        public override void Render()
        {
            if (!_showWindow) return;

            ImGui.Begin(_windowName, ref _showWindow, ImGuiWindowFlags.NoDocking);

            if (ExplorerWindows == null || ExplorerWindows.Count == 0)
            {
                ImGui.Text("No explorer windows open.");
                ImGui.End();
                return;
            }
            
            // Корректировка индексов

            if (ExplorerWindows != null)
            {
                _sourceExplorerIndex = Math.Clamp(_sourceExplorerIndex, 0, ExplorerWindows.Count - 1);
                _targetExplorerIndex = Math.Clamp(_targetExplorerIndex, 0, ExplorerWindows.Count - 1);
                if (_sourceExplorerIndex == _targetExplorerIndex && ExplorerWindows.Count > 1)
                    _targetExplorerIndex = (_sourceExplorerIndex + 1) % ExplorerWindows.Count;

                _sourceFiles = BuildFileItemsForExplorer(ExplorerWindows[_sourceExplorerIndex]);
                _targetFiles = BuildFileItemsForExplorer(ExplorerWindows[_targetExplorerIndex]);
            }

            var explorerNames = ExplorerWindows.Select(w => w.GetWindowName()).ToArray();

            ImGui.Separator();

            if (_selectedSourceFile != null && _selectedTargetFile != null)
            {
                if (ImGui.Button("Transfer file (replace target)"))
                {
                    if (_selectedSourceFile.Node.Type == _selectedTargetFile.Node.Type)
                        TransferFile(_selectedSourceFile, _selectedTargetFile);
                    else
                        Console.WriteLine("Please select files of the same type");
                }
                ImGui.SameLine();
                if (ImGui.RadioButton("Use source name", _useSourceFileName))
                {
                    _useSourceFileName = !_useSourceFileName;
                }
                ImGui.Text($"Replace '{_selectedTargetFile.VirtualPath}' with '{_selectedSourceFile.VirtualPath}'");
                ImGui.Text($"Replace '{_selectedTargetFile.Node.Type}' with '{_selectedSourceFile.Node.Type}'");
            }
            else
            {
                ImGui.Text("Select source and target files to enable transfer.");
            }

            // Таблица с двумя колонками, занимает всю доступную ширину
            if (ImGui.BeginTable("TransferTable", 2, ImGuiTableFlags.SizingStretchSame))
            {
                // === Колонка 1 (Source) ===
                ImGui.TableNextColumn();
                ImGui.Text("Source Explorer");
                ImGui.SameLine();
                if (ImGui.Combo("##SourceExplorer", ref _sourceExplorerIndex, explorerNames, explorerNames.Length))
                {
                    _selectedSourceFile = null;
                    _sourceFiles = BuildFileItemsForExplorer(ExplorerWindows[_sourceExplorerIndex]);
                }
                ImGui.Separator();

                // Дочернее окно с прокруткой – будет выровнено по верху ячейки
                if (ImGui.BeginChild("SourceFileList", new Vector2(-1, -1), ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar))
                {
                    RenderFileList(ref _sourceFiles, ref _selectedSourceFile, isSource: true);
                }
                ImGui.EndChild();

                // === Колонка 2 (Target) ===
                ImGui.TableNextColumn();
                ImGui.Text("Target Explorer");
                ImGui.SameLine();
                if (ImGui.Combo("##TargetExplorer", ref _targetExplorerIndex, explorerNames, explorerNames.Length))
                {
                    _selectedTargetFile = null;
                    _targetFiles = BuildFileItemsForExplorer(ExplorerWindows[_targetExplorerIndex]);
                }
                ImGui.Separator();

                if (ImGui.BeginChild("TargetFileList", new Vector2(-1, -1), ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar))
                {
                    RenderFileList(ref _targetFiles, ref _selectedTargetFile, isSource: false);
                }
                ImGui.EndChild();

                ImGui.EndTable();
            }

            ImGui.End();
        }

        // Отрисовка одного списка файлов с группировкой по RootPath
        private void RenderFileList(ref List<FileItem> files, ref FileItem selectedItem, bool isSource)
        {
            if (files == null) files = new List<FileItem>();

            if (files.Count == 0)
            {
                ImGui.Text("No selected files in this explorer.");
                return;
            }

            var groups = files.GroupBy(f => f.RootPath).OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                if (ImGui.TreeNode($"{group.Key}##{group.Key}_{(isSource ? "src" : "dst")}"))
                {
                    foreach (var file in group.OrderBy(f => f.VirtualPath))
                    {
                        bool isSelected = selectedItem != null &&
                                           selectedItem.RootPath == file.RootPath &&
                                           selectedItem.VirtualPath == file.VirtualPath;

                        if (ImGui.Selectable($"{file.VirtualPath}##{file.RootPath}{file.VirtualPath}", isSelected))
                        {
                            selectedItem = file;
                        }

                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip($"Full path: {file.Name}");
                    }
                    ImGui.TreePop();
                }
            }
        }

        // Собрать все выделенные файлы из конкретного проводника
        private List<FileItem> BuildFileItemsForExplorer(ExplorerWindow explorer)
        {
            var items = new List<FileItem>();
            var treeChildren = explorer.GetTreeChildList();
            if (treeChildren == null) return items;

            foreach (var tree in treeChildren)
            {
                string rootPath = tree.RootFilePath ?? "Unknown";
                var selectedNode = tree.Selected;

                if (selectedNode != null && !string.IsNullOrEmpty(selectedNode.VirtualPath))
                {
                    items.Add(new FileItem
                    {
                        RootPath = rootPath,
                        VirtualPath = selectedNode.VirtualPath,
                        Name = selectedNode.Name,
                        Type = selectedNode.Type,
                        Node = selectedNode,
                        SourceWindow = explorer,
                        SourceTree = tree
                    });
                }
            }
            return items;
        }

        // Заглушка переноса файла – здесь необходимо подключить реальную логику экспорта/импорта
        private void TransferFile(FileItem source, FileItem target)
        {
            try
            {
                // Извлечение байтов
                byte[] data = ExtractFileBytes(source.Node);
                if (data == null)
                {
                    ImGui.OpenPopup("TransferFailed_Extract");
                    return;
                }

                // Внедрение
                var injector = target.SourceTree.Injector;

                bool success = false;

                if (_useSourceFileName)
                    success = injector.InjectBytes(target.SourceTree.Root, target.Node, data, source.Name);
                else
                    success = injector.InjectBytes(target.SourceTree.Root, target.Node, data, target.Name);


                if (success)
                {
                    // Уведомление целевого проводника об обновлении
                    injector.OnInjectionComplete?.Invoke(target.Node.VirtualPath);
                    Debug.WriteLine($"Transfer OK: {source.VirtualPath} -> {target.VirtualPath}");
                }
                else
                {
                    ImGui.OpenPopup("TransferFailed_Inject");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Transfer error: {ex.Message}");
                ImGui.OpenPopup("TransferError");
            }
        }

        private byte[] ExtractFileBytes(FileNode node)
        {
            var binder = new FileBinders();
            var operation = new FileOperation { GetObject = true };
            binder.ProcessPaths(new[] { node.VirtualPath }, operation);
            var obj = binder.GetObject();

            return obj switch
            {
                BinderFile file => file.Bytes,
                TPF.Texture texture => texture.Bytes,
                FLVER2 flver => flver.Write(),    // Write() возвращает byte[]
                TPF tpf => tpf.Write(),
                BND bnd => bnd.Write()
            };
        }
    }
}
