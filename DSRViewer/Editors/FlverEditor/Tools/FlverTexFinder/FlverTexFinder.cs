using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DSRViewer.Editors.FlverEditor;
using DSRViewer.FileProcess;
using DSRViewer.Editors.MTDEditor;
using DSRViewer.UI.Base;
using ImGuiNET;
using SoulsFormats;

namespace DSRViewer.Editors.FlverEditor.Tools.FlverTexFinder
{
    internal class FlverTexFinder : ImGuiWindow
    {
        private ModelFinderList _modelFinderList;
        private MTDFinderList _mtdFinderList;
        private MTDTexTypeList _mtdTexTypeList;

        private List<MTDShortDetails> _mtdList = [];

        private FlverTools flverTools = new();

        private string _textureNameFinder = "";

        public FlverTexFinder(string windowName, bool showWindow, List<MTDShortDetails> mtdList) : base(windowName, showWindow)
        {
            _mtdList = mtdList;
            _modelFinderList = new ModelFinderList();
            _mtdFinderList   = new MTDFinderList();
            _mtdTexTypeList  = new MTDTexTypeList();

            _modelFinderList.SetSize(new Vector2(300, 400));
            _mtdFinderList  .SetSize(new Vector2(300, 400));
            _mtdTexTypeList .SetSize(new Vector2(200, 400));

            _minSize = new(850, 480);
            _maxSize = new(float.MaxValue, float.MaxValue);
            _windowFlags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            _mtdFinderList.OnItemSelected += OnMTDSelected;
        }

        private void OnMTDSelected(int selectedIndex, string materialPath)
        {
            if (string.IsNullOrEmpty(materialPath) || _mtdList == null) return;

            var materialName = materialPath.Split("\\").Last();
            var matchingMtd = _mtdList.FirstOrDefault(mtd =>
                mtd.Name.Equals(materialName, StringComparison.OrdinalIgnoreCase));

            if (matchingMtd != null)
            {
                _mtdTexTypeList.SetTextures(matchingMtd.TexType);
            }
        }

        public void Render(List<FileNode> flverFileList, List<MTDShortDetails> mtdList)
        {
            _mtdList = mtdList;
            if (!_showWindow) return;

            ImGui.Begin(_windowName, ref _showWindow, _windowFlags);

            if (ImGui.Button("Texture finder"))
                FindTextures(flverFileList);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(250);
            ImGui.InputText("##tex_finder", ref _textureNameFinder, 256);

            if (ImGui.BeginTable("##texfinder_layout", 3,
                ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Models",    ImGuiTableColumnFlags.WidthStretch, 0.40f);
                ImGui.TableSetupColumn("Materials", ImGuiTableColumnFlags.WidthStretch, 0.40f);
                ImGui.TableSetupColumn("Tex Types", ImGuiTableColumnFlags.WidthStretch, 0.20f);
                ImGui.TableHeadersRow();

                ImGui.TableNextColumn();
                _modelFinderList.SetSize(new Vector2(-1, ImGui.GetContentRegionAvail().Y));
                _modelFinderList.Render();

                ImGui.TableNextColumn();
                _mtdFinderList.SetSize(new Vector2(-1, ImGui.GetContentRegionAvail().Y));
                _mtdFinderList.Render();

                ImGui.TableNextColumn();
                _mtdTexTypeList.SetSize(new Vector2(-1, ImGui.GetContentRegionAvail().Y));
                _mtdTexTypeList.Render();

                ImGui.EndTable();
            }

            ImGui.End();
        }

        private void FindTextures(List<FileNode> flverFileList)
        {
            _modelFinderList.Clear();
            _mtdFinderList.Clear();
            _mtdTexTypeList.Clear();

            var paths = flverFileList
                .Where(n => n.VirtualPath != null)
                .Select(n => n.VirtualPath)
                .ToList();

            new FileBinders().ProcessPaths(paths, new FileOperation
            {
                UseFlverDelegate = true,
                AdditionalFlverProcessing = (flver, virtualPath, _, __) =>
                {
                    if (!flverTools.TexFinder(flver.Materials, _textureNameFinder)) return;

                    _modelFinderList.AddModel(virtualPath);

                    var materialPaths = new List<string>();
                    flverTools.MTDFinderList(flver.Materials, _textureNameFinder, materialPaths);
                    foreach (var mp in materialPaths.Distinct())
                        _mtdFinderList.AddMaterial(mp);
                }
            });

            _mtdFinderList.UpdateMtdTypes(_mtdList);
        }
    }
}