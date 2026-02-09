using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ImGuiNET;

namespace DSRViewer.FileHelper.FileExplorer.Tools
{
    internal class TreeTabsTexTools
    {
        private Action<string> _onInjectionComplete; // Колбэк для обновления дерева
        private string _texName = "";
        private int _texFlag = 0;
        private bool _rename = false;
        private bool _replaceFlag = false;
        public TreeTabsTexTools(Action<string> onInjectionComplete = null)
        {
            _onInjectionComplete = onInjectionComplete;
        }
        public void ButtonAddTexture(FileNode selected)
        {
            if (ImGui.Button("Add texture"))
            {
                if (selected.IsNestedTpfArchive || selected.IsTpfArchive)
                {
                    FileBinders binders = new();
                    binders.SetDds(true, false, false);
                    binders.SetCommon(false, true);
                    binders.Read(selected.VirtualPath);
                    _onInjectionComplete?.Invoke(selected.VirtualPath);
                }
            }
        }
        public void ButtonRemoveTexture(FileNode selected)
        {
            if (ImGui.Button("Remove texture"))
            {
                if (selected.IsNestedDDS)
                {
                    FileBinders binders = new();
                    binders.SetDds(false, true, false);
                    binders.SetCommon(false, true);
                    binders.Read(selected.VirtualPath);

                    var tempVirtualPath = selected.VirtualPath;
                    tempVirtualPath = tempVirtualPath[..tempVirtualPath.LastIndexOf('|')];

                    _onInjectionComplete?.Invoke(tempVirtualPath);
                }
            }
        }

        public void ButtonRenameTexture(FileNode selected)
        {
            if (ImGui.Button("Rename texture"))
            {
                if (selected.IsNestedDDS)
                {
                    FileBinders binders = new();
                    binders.SetDds(false, false, false, true, false, (byte)selected.DDSFormatFlag, _texName);
                    binders.SetCommon(false, true);
                    binders.Read(selected.VirtualPath);
                    _onInjectionComplete?.Invoke(selected.VirtualPath);
                }
            }

            ImGui.SameLine();
            ImGui.InputText("Name", ref _texName, 255);
        }

        public void ButtonReFlagTexture(FileNode selected)
        {
            if (ImGui.Button("Set format flag"))
            {
                if (selected.IsNestedDDS)
                {
                    FileBinders binders = new();
                    binders.SetDds(false, false, false, false, true, (byte)_texFlag, selected.Name);
                    binders.SetCommon(false, true);
                    binders.Read(selected.VirtualPath);
                    _onInjectionComplete?.Invoke(selected.VirtualPath);
                }
            }

            ImGui.SameLine();
            ImGui.InputInt("Flag", ref _texFlag, 1);
        }
    }
}
