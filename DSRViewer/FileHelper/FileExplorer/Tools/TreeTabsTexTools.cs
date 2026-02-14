using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ImGuiNET;

namespace DSRViewer.FileHelper.FileExplorer.Tools
{
    internal class TreeTabsTexTools
    {
        private readonly Action<string> _onInjectionComplete;

        // Общие поля для операций
        private string _newObjectName = "";
        private byte[] _newObjectBytes = [];
        private string _replaceFilePath = "";
        private string _newArchiveName = "";
        private int _formatFlag = 0;
        private bool _useTargetName = false;   // true – оставить имя таргета, false – взять имя из нового файла

        public TreeTabsTexTools(Action<string> onInjectionComplete = null)
        {
            _onInjectionComplete = onInjectionComplete;
        }

        // ------------------------------------------------------------
        // ВСПОМОГАТЕЛЬНОЕ
        // ------------------------------------------------------------
        private string GetParentArchivePath(FileNode node)
        {
            if (node?.VirtualPath == null) return null;
            int lastPipe = node.VirtualPath.LastIndexOf('|');
            return lastPipe > 0 ? node.VirtualPath.Substring(0, lastPipe) : node.VirtualPath;
        }

        // ------------------------------------------------------------
        // УНИВЕРСАЛЬНЫЕ ОПЕРАЦИИ
        // ------------------------------------------------------------
        public void RenderAddObjectControls(FileNode selectedNode)
        {
            if (selectedNode == null) return;
            bool isArchive = selectedNode.IsBndArchive || selectedNode.IsNestedBndArchive ||
                             selectedNode.IsBxfArchive || selectedNode.IsNestedBxfArchive ||
                             selectedNode.IsTpfArchive || selectedNode.IsNestedTpfArchive;
            if (!isArchive) return;

            ImGui.Separator();
            ImGui.Text("Add to Archive:");

            ImGui.InputText("Name", ref _newObjectName, 255);
            if (ImGui.Button("Select File..."))
            {
                var thread = new Thread(() =>
                {
                    using var ofd = new OpenFileDialog();
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        _newObjectBytes = File.ReadAllBytes(ofd.FileName);
                        if (string.IsNullOrEmpty(_newObjectName))
                            _newObjectName = Path.GetFileName(ofd.FileName);
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }
            ImGui.SameLine();
            if (ImGui.Button("Add"))
                AddObject(selectedNode);

            if (_newObjectBytes.Length > 0)
                ImGui.Text($"Selected: {_newObjectName} ({_newObjectBytes.Length} bytes)");
        }

        public void RenderRemoveObjectControls(FileNode selectedNode)
        {
            if (selectedNode?.VirtualPath == null) return;
            if (ImGui.Button("Remove"))
                RemoveObject(selectedNode);
        }

        public void RenderRenameObjectControls(FileNode selectedNode)
        {
            if (selectedNode?.VirtualPath == null) return;
            ImGui.InputText("New Name", ref _newObjectName, 255);
            ImGui.SameLine();
            if (ImGui.Button("Rename"))
                RenameObject(selectedNode);
        }

        public void RenderReplaceObjectControls(FileNode selectedNode)
        {
            if (selectedNode?.VirtualPath == null || selectedNode.IsFolder) return;
            ImGui.Text("Replace Content:");
            if (ImGui.Button("Select File...##Replace"))
            {
                var thread = new Thread(() =>
                {
                    using var ofd = new OpenFileDialog();
                    if (ofd.ShowDialog() == DialogResult.OK)
                        _replaceFilePath = ofd.FileName;
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }
            if (!string.IsNullOrEmpty(_replaceFilePath))
            {
                ImGui.Text($"Replace with: {Path.GetFileName(_replaceFilePath)}");

                // Группа радиокнопок: выбор источника имени
                if (ImGui.RadioButton("Use file name", !_useTargetName))
                    _useTargetName = !_useTargetName;
                ImGui.SameLine();

                ImGui.SameLine();
                if (ImGui.Button("Replace"))
                    ReplaceObject(selectedNode);
            }
        }

        // ------------------------------------------------------------
        // СПЕЦИАЛИЗИРОВАННЫЕ ОПЕРАЦИИ
        // ------------------------------------------------------------
        public void RenderChangeFormatControls(FileNode selectedNode)
        {
            if (selectedNode == null) return;
            if (!selectedNode.IsNestedDDS && !selectedNode.IsFlver) return;

            ImGui.InputInt("Format Flag", ref _formatFlag, 1);
            ImGui.SameLine();
            string btn = selectedNode.IsNestedDDS ? "Set Texture Format" : "Set FLVER Format";
            if (ImGui.Button(btn))
                ChangeFormat(selectedNode);
        }

        public void RenderAddTpfDcxControls(FileNode selectedNode)
        {
            if (selectedNode == null) return;
            if (!selectedNode.IsBxfArchive && !selectedNode.IsNestedBxfArchive) return;

            ImGui.Text("Add tpf.dcx:");
            ImGui.InputText("Archive Name", ref _newArchiveName, 255);
            if (ImGui.Button("Add tpf.dcx"))
                AddTpfDcx(selectedNode);
        }

        // ------------------------------------------------------------
        // ОБРАБОТЧИКИ
        // ------------------------------------------------------------
        private void AddObject(FileNode selectedNode)
        {
            if (string.IsNullOrEmpty(_newObjectName) || _newObjectBytes.Length == 0)
            {
                MessageBox.Show("Name and file are required.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                var binder = new FileBinders();
                var operation = new FileOperation
                {
                    WriteObject = true,
                    AddObject = true,
                    NewObjectName = _newObjectName,
                    NewObjectBytes = _newObjectBytes
                };
                binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);

                _onInjectionComplete?.Invoke(selectedNode.VirtualPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Add failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _newObjectName = "";
                _newObjectBytes = Array.Empty<byte>();
            }
        }

        private void RemoveObject(FileNode selectedNode)
        {
            try
            {
                var binder = new FileBinders();
                var operation = new FileOperation { 
                    WriteObject = true, 
                    RemoveObject = true };
                binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);

                _onInjectionComplete?.Invoke(selectedNode.VirtualPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Remove failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenameObject(FileNode selectedNode)
        {
            if (string.IsNullOrEmpty(_newObjectName))
            {
                MessageBox.Show("New name cannot be empty.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                var binder = new FileBinders();
                var operation = new FileOperation
                {
                    WriteObject = true,
                    RenameObject = true,
                    NewObjectName = _newObjectName
                };
                binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);

                _onInjectionComplete?.Invoke(selectedNode.VirtualPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rename failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _newObjectName = "";
            }
        }

        private void ReplaceObject(FileNode selectedNode)
        {
            if (string.IsNullOrEmpty(_replaceFilePath) || !File.Exists(_replaceFilePath))
            {
                MessageBox.Show("Replacement file missing.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                byte[] newBytes = File.ReadAllBytes(_replaceFilePath);
                var binder = new FileBinders();
                var operation = new FileOperation
                {
                    WriteObject = true,
                    ReplaceObject = true,
                    NewObjectBytes = newBytes
                };

                // Если выбран вариант «имя из нового файла» – добавляем переименование
                if (_useTargetName)
                {
                    operation.RenameObject = true;
                    operation.NewObjectName = selectedNode.Name;
                    
                }
                else
                {
                    operation.RenameObject = true;
                    operation.NewObjectName = Path.GetFileName(_replaceFilePath);
                }

                binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);

                _onInjectionComplete?.Invoke(selectedNode.VirtualPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Replace failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _replaceFilePath = "";
            }
        }

        private void ChangeFormat(FileNode selectedNode)
        {
            try
            {
                var binder = new FileBinders();
                var operation = new FileOperation
                {
                    WriteObject = true,
                    ChangeTextureFormat = true,
                    NewTextureFormat = (byte)_formatFlag
                };
                binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);

                _onInjectionComplete?.Invoke(selectedNode.VirtualPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Change format failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddTpfDcx(FileNode selectedNode)
        {
            if (string.IsNullOrEmpty(_newArchiveName))
            {
                MessageBox.Show("Archive name and texture name required.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                var binder = new FileBinders();
                var operation = new FileOperation
                {
                    WriteObject = true,
                    AddTpfDcx = true,
                    NewTpfDcxArchiveName = _newArchiveName,
                };
                binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);
                _onInjectionComplete?.Invoke(selectedNode.VirtualPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Add tpf.dcx failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ------------------------------------------------------------
        // ГЛАВНЫЙ МЕТОД
        // ------------------------------------------------------------
        public void RenderAllControls(FileNode selectedNode)
        {
            if (selectedNode == null) return;

            RenderAddObjectControls(selectedNode);
            RenderRemoveObjectControls(selectedNode);
            RenderRenameObjectControls(selectedNode);
            RenderReplaceObjectControls(selectedNode);
            RenderChangeFormatControls(selectedNode);
            RenderAddTpfDcxControls(selectedNode);
        }
    }
}