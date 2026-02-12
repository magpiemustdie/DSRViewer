using System;
using ImGuiNET;

namespace DSRViewer.FileHelper.FileExplorer.Tools
{
    internal class TreeTabsTexTools
    {
        private readonly Action<string> _onInjectionComplete;

        // Поля для операций с текстурами
        private string _newTextureName = "";
        private string _renameTextureName = "";
        private string _newArchiveName = "";
        private int _textureFormatFlag = 0;
        private bool _renameOperation = false;
        private bool _replaceFlagOperation = false;

        public TreeTabsTexTools(Action<string> onInjectionComplete = null)
        {
            _onInjectionComplete = onInjectionComplete;
        }

        public void RenderAddTextureControls(FileNode selectedNode)
        {
            if (selectedNode == null || !(selectedNode.IsNestedTpfArchive || selectedNode.IsTpfArchive))
                return;

            ImGui.InputText("Texture Name", ref _newTextureName, 255);
            ImGui.SameLine();

            if (ImGui.Button("Add Texture"))
            {
                AddTexture(selectedNode);
            }
        }

        public void RenderRemoveTextureControls(FileNode selectedNode)
        {
            if (selectedNode == null || !selectedNode.IsNestedDDS)
                return;

            if (ImGui.Button("Remove Texture"))
            {
                RemoveTexture(selectedNode);
            }
        }

        public void RenderRenameTextureControls(FileNode selectedNode)
        {
            if (selectedNode == null || !selectedNode.IsNestedDDS)
                return;

            ImGui.InputText("New Name", ref _renameTextureName, 255);
            ImGui.SameLine();

            if (ImGui.Button("Rename Texture"))
            {
                RenameTexture(selectedNode);
            }
        }

        public void RenderReFlagTextureControls(FileNode selectedNode)
        {
            if (selectedNode == null || !selectedNode.IsNestedDDS)
                return;

            ImGui.InputInt("Format Flag", ref _textureFormatFlag, 1);
            ImGui.SameLine();

            if (ImGui.Button("Set Format Flag"))
            {
                ReFlagTexture(selectedNode);
            }
        }

        public void RenderAddTpfDcxControls(FileNode selectedNode)
        {
            if (selectedNode == null || !(selectedNode.IsBxfArchive || selectedNode.IsNestedBxfArchive))
                return;

            ImGui.InputText("Archive Name", ref _newArchiveName, 255);
            ImGui.InputText("Texture Name", ref _newTextureName, 255);
            ImGui.SameLine();

            if (ImGui.Button("Add tpf.dcx"))
            {
                AddTpfDcx(selectedNode);
            }
        }

        public void RenderRemoveTpfDcxControls(FileNode selectedNode)
        {
            if (selectedNode == null || !(selectedNode.IsNestedTpfArchive))
                return;

            if (ImGui.Button("Remove tpf.dcx"))
            {
                RemoveTpfDcx(selectedNode);
            }
        }

        private void AddTexture(FileNode selectedNode)
        {
            if (string.IsNullOrEmpty(_newTextureName))
            {
                // Можно добавить сообщение об ошибке
                return;
            }

            var binder = new FileBinders();
            var operation = new FileOperation
            {
                WriteObject = true,
                AddTexture = true,
                NewTextureName = _newTextureName
            };

            binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);
            _onInjectionComplete?.Invoke(selectedNode.VirtualPath);
        }

        private void RemoveTexture(FileNode selectedNode)
        {
            var binder = new FileBinders();
            var operation = new FileOperation
            {
                WriteObject = true,
                RemoveTexture = true
            };

            binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);

            // Обновляем родительский путь
            var parentVirtualPath = selectedNode.VirtualPath[..selectedNode.VirtualPath.LastIndexOf('|')];
            _onInjectionComplete?.Invoke(parentVirtualPath);
        }

        private void RenameTexture(FileNode selectedNode)
        {
            if (string.IsNullOrEmpty(_renameTextureName))
            {
                // Можно добавить сообщение об ошибке
                return;
            }

            var binder = new FileBinders();
            var operation = new FileOperation
            {
                WriteObject = true,
                RenameTexture = true,
                NewTextureName = _renameTextureName
            };

            binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);
            _onInjectionComplete?.Invoke(selectedNode.VirtualPath);
        }

        private void ReFlagTexture(FileNode selectedNode)
        {
            var binder = new FileBinders();
            var operation = new FileOperation
            {
                WriteObject = true,
                ChangeTextureFormat = true,
                NewTextureFormat = (byte)_textureFormatFlag
            };

            binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);
            _onInjectionComplete?.Invoke(selectedNode.VirtualPath);
        }

        private void AddTpfDcx(FileNode selectedNode)
        {
            if (string.IsNullOrEmpty(_newArchiveName) || string.IsNullOrEmpty(_newTextureName))
            {
                // Можно добавить сообщение об ошибке
                return;
            }

            var binder = new FileBinders();
            var operation = new FileOperation
            {
                WriteObject = true,
                AddTpfDcx = true,
                AddTexture = true,
                NewTpfDcxArchiveName = _newArchiveName,
                NewTextureName = _newTextureName
            };

            binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);
            _onInjectionComplete?.Invoke(selectedNode.VirtualPath);
        }

        private void RemoveTpfDcx(FileNode selectedNode)
        {
            var binder = new FileBinders();
            var operation = new FileOperation
            {
                WriteObject = true,
                RemoveTpfDcx = true,
            };

            binder.ProcessPaths(new[] { selectedNode.VirtualPath }, operation);

            var parentVirtualPath = selectedNode.VirtualPath[..selectedNode.VirtualPath.LastIndexOf('|')];
            _onInjectionComplete?.Invoke(parentVirtualPath);
        }

        // Метод для отрисовки всех элементов управления
        public void RenderAllControls(FileNode selectedNode)
        {
            if (selectedNode == null)
                return;

            ImGui.Separator();
            ImGui.Text("Texture Operations:");

            RenderAddTextureControls(selectedNode);
            RenderRemoveTextureControls(selectedNode);
            RenderRenameTextureControls(selectedNode);
            RenderReFlagTextureControls(selectedNode);
            RenderAddTpfDcxControls(selectedNode);
            RenderRemoveTpfDcxControls(selectedNode);
        }
    }
}