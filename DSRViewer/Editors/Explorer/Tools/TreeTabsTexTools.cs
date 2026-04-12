using DSRViewer.Core;
using DSRViewer.Editors.Explorer.DDSHelper;
using DSRViewer.FileProcess;
using ImGuiNET;
using System;
using System.IO;
using System.Windows.Forms;
using Veldrid;

namespace DSRViewer.Editors.Explorer.Tools
{
    /// <summary>Инструменты текстурных операций: добавление, замена, переименование, удаление, смена формата.</summary>
    public class TreeTabsTexTools
    {
        private readonly Action<string> _onInjectionComplete;
        private readonly GraphicsDevice _gd;
        private readonly Injector _injector;
        private readonly TextureEditor _textureEditor;

        // Add
        private string _addName = "";
        private byte[] _addBytes = [];

        // Rename
        private string _renameName = "";

        // Replace
        private string _replaceFilePath = "";
        private bool _useTargetName = false;

        // Format
        private int _formatFlag = 0;

        // Add tpf.dcx
        private string _newArchiveName = "";

        public TreeTabsTexTools(GraphicsDevice gd, Injector injector, Action<string> onInjectionComplete = null)
        {
            _gd = gd;
            _injector = injector;
            _onInjectionComplete = onInjectionComplete;
            _textureEditor = new TextureEditor(gd);
        }

        /// <summary>Устарело — операции перенесены в контекстное меню.</summary>
        public void RenderAllControls(FileNode node) { }

        /// <summary>Рендерит операции с узлом как статичные элементы (без popup/menu).</summary>
        public void RenderContextMenuItems(FileNode node)
        {
            if (node == null) return;

            bool isArchive = node.IsBndArchive || node.IsBxfArchive || node.IsTpfArchive;
            bool isBxf     = node.IsBxfArchive;
            bool isTpfLike = node.IsTpfArchive || isBxf;
            bool isFile    = !node.IsFolder
                          && !(node.Type == NodeType.BndArchive)
                          && !(node.Type == NodeType.TpfArchive)
                          && !(node.Type == NodeType.BxfArchive);
            bool isDds     = node.IsDDS || node.IsNestedDDS;

            // Добавить файл в архив
            if (isArchive)
            {
                ImGui.TextDisabled("Add file");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##addname", ref _addName, 255);
                if (ImGui.SmallButton("Browse##add"))
                {
                    string path = DialogHelper.SelectFile("Select file to add", "All files (*.*)|*.*");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _addBytes = File.ReadAllBytes(path);
                        if (string.IsNullOrEmpty(_addName)) _addName = Path.GetFileName(path);
                    }
                }
                ImGui.SameLine();
                if (_addBytes.Length > 0)
                {
                    if (ImGui.SmallButton($"Add##do"))
                        AddObject(node);
                }
                else
                {
                    ImGui.TextDisabled("Add##do");
                }
                ImGui.Spacing();
            }

            if (isBxf)
            {
                ImGui.TextDisabled("Add tpf.dcx");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##tpfdcxname", ref _newArchiveName, 255);
                if (ImGui.SmallButton("Add##tpfdcx")) AddTpfDcx(node);
                ImGui.Spacing();
            }

            if (isFile)
            {
                if (ImGui.SmallButton("Replace..."))
                {
                    string path = DialogHelper.SelectFile("Select replacement file", "All files (*.*)|*.*");
                    if (!string.IsNullOrEmpty(path)) { _replaceFilePath = path; ReplaceObject(node); }
                }

                ImGui.Spacing();
                ImGui.TextDisabled("Rename");
                if (isDds) ImGui.TextDisabled("(no extension needed)");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##rename", ref _renameName, 255);
                if (ImGui.SmallButton("Apply##rename")) RenameObject(node);

                ImGui.Spacing();
                bool isNestedArchive = node.IsNestedBndArchive || node.IsNestedTpfArchive || node.IsNestedBxfArchive;
                if (ImGui.SmallButton(isNestedArchive ? "Remove archive" : "Remove"))
                    RemoveObject(node);
            }

            if (isDds)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("Format flag");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputInt("##fmt", ref _formatFlag, 1);
                _formatFlag = Math.Clamp(_formatFlag, 0, 255);
                if (ImGui.SmallButton("Apply##fmt")) ChangeFormat(node);
            }

            if (isTpfLike && !node.IsFolder)
            {
                ImGui.Spacing();
                ImGui.Separator();
                // TODO: раскомментировать когда Magick будет нужен
                // if (ImGui.SmallButton("Edit env cubemaps"))
                //     _textureEditor.EditAllInNode(node,
                //         filter: n => n.Name.Contains("GI_EnvSpc") || n.Name.Contains("GI_EnvDif"),
                //         onComplete: _ => _onInjectionComplete?.Invoke(node.VirtualPath));
                // if (ImGui.SmallButton("Edit all textures"))
                //     _textureEditor.EditAllInNode(node,
                //         onComplete: _ => _onInjectionComplete?.Invoke(node.VirtualPath));
            }
        }

        // ---- Операции ----

        private void AddObject(FileNode node)
        {
            if (string.IsNullOrEmpty(_addName) || _addBytes.Length == 0)
            {
                MessageBox.Show("Name and file are required.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                var op = new FileOperation
                {
                    WriteObject = true,
                    AddObject = true,
                    NewObjectName = _addName,
                    NewObjectBytes = _addBytes
                };

                if (FileSignatures.IsDdsData(_addBytes))
                {
                    string fmt = DDSTools.ReadDDSImageFormat(_addBytes);
                    if (DDS_FlagFormatList.DDSFlagListSet.TryGetValue(fmt, out int flag))
                    {
                        op.ChangeTextureFormat = true;
                        op.NewTextureFormat = Convert.ToByte(flag);
                    }
                }

                new FileBinders().ProcessPaths([node.VirtualPath], op);
                _onInjectionComplete?.Invoke(node.VirtualPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Add failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _addName = "";
                _addBytes = [];
            }
        }

        private void RemoveObject(FileNode node)
        {
            try
            {
                new FileBinders().ProcessPaths([node.VirtualPath], new FileOperation
                {
                    WriteObject  = true,
                    RemoveObject = true
                });

                // Передаём путь родительского архива для обновления дерева.
                // Для вложенных архивов (NestedBnd/Tpf/Bxf) родитель — корневой файл.
                string parentPath = node.VirtualPath.Contains('|')
                    ? node.VirtualPath[..node.VirtualPath.LastIndexOf('|')]
                    : node.VirtualPath;
                _onInjectionComplete?.Invoke(parentPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Remove failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenameObject(FileNode node)
        {
            if (string.IsNullOrEmpty(_renameName))
            {
                MessageBox.Show("New name cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                // Для DDS-текстур имя в TPF хранится без расширения
                string newName = (node.IsNestedDDS || node.IsDDS)
                    ? Path.GetFileNameWithoutExtension(_renameName)
                    : _renameName;

                new FileBinders().ProcessPaths([node.VirtualPath], new FileOperation
                {
                    WriteObject    = true,
                    RenameObject   = true,
                    NewObjectName  = newName
                });

                // Обновляем родительский архив — путь узла изменился
                string parentPath = node.VirtualPath.Contains('|')
                    ? node.VirtualPath[..node.VirtualPath.LastIndexOf('|')]
                    : node.VirtualPath;
                _onInjectionComplete?.Invoke(parentPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rename failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _renameName = ""; }
        }

        private void ReplaceObject(FileNode node)
        {
            if (string.IsNullOrEmpty(_replaceFilePath) || !File.Exists(_replaceFilePath))
            {
                MessageBox.Show("Replacement file missing.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                byte[] newBytes = File.ReadAllBytes(_replaceFilePath);
                // Для DDS-текстур имя в TPF хранится без расширения
                bool isDds = (node.IsNestedDDS || node.IsDDS)
                          || _replaceFilePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase);
                string srcName = isDds
                    ? Path.GetFileNameWithoutExtension(_replaceFilePath)
                    : Path.GetFileName(_replaceFilePath);
                string newName = _useTargetName ? node.Name : srcName;

                // Вся логика замены (DDS формат, rename и т.д.) — в Injector
                bool success = _injector.InjectBytes(node, newBytes, newName);
                if (success)
                    _onInjectionComplete?.Invoke(node.VirtualPath);
                else
                    MessageBox.Show("Replace failed. Check console.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Replace failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _replaceFilePath = ""; }
        }

        private void ChangeFormat(FileNode node)
        {
            try
            {
                new FileBinders().ProcessPaths([node.VirtualPath], new FileOperation
                {
                    WriteObject = true,
                    ChangeTextureFormat = true,
                    NewTextureFormat = (byte)_formatFlag
                });
                _onInjectionComplete?.Invoke(node.VirtualPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Change format failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddTpfDcx(FileNode node)
        {
            if (string.IsNullOrEmpty(_newArchiveName))
            {
                MessageBox.Show("Archive name is required.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                new FileBinders().ProcessPaths([node.VirtualPath], new FileOperation
                {
                    WriteObject = true,
                    AddTpfDcx = true,
                    NewTpfDcxArchiveName = _newArchiveName
                });
                _onInjectionComplete?.Invoke(node.VirtualPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Add tpf.dcx failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
