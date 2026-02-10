using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DSRViewer.FileHelper.FlverEditor.Render;
using DSRViewer.FileHelper.MTDEditor.Render;
using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;

namespace DSRViewer.FileHelper.FlverEditor.Tools.FlverTexFinder
{
    internal class FlverTexFinder : ImGuiWindow
    {
        private ModelFinderList _modelFinderList;
        private MTDFinderList _mtdFinderList;
        private MTDTexTypeList _mtdTexTypeList;

        // Добавляем поле для хранения списка MTD
        private List<MTDShortDetails> _mtdList = [];

        private FlverTools flverTools = new();

        private string _textureNameFinder = "";

        public FlverTexFinder(string windowName, bool showWindow, List<MTDShortDetails> mtdList)
        {
            _windowName = windowName;
            _showWindow = showWindow;

            _mtdList = mtdList;

            _modelFinderList = new ModelFinderList();
            _mtdFinderList = new MTDFinderList();
            _mtdTexTypeList = new MTDTexTypeList();

            _mtdFinderList.OnItemSelected += OnMTDSelected;
        }

        private void OnMTDSelected(int selectedIndex, string materialPath)
        {
            if (string.IsNullOrEmpty(materialPath) || _mtdList == null) return;

            var materialName = materialPath.Split("\\").Last();
            var matchingMtd = _mtdList.FirstOrDefault(mtd =>
                mtd.Name.Equals(materialName, StringComparison.CurrentCultureIgnoreCase));

            if (matchingMtd != null)
            {
                _mtdTexTypeList.SetTextures(matchingMtd.TexType);
            }
        }

        public void Render(List<FileNode> flverFileList, List<MTDShortDetails> mtdList)
        {
            // Сохраняем ссылку на список MTD
            _mtdList = mtdList;

            if (_showWindow)
            {
                ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
                {
                    if (ImGui.Button("Texture finder"))
                    {
                        FindTextures(flverFileList);
                    }

                    ImGui.SetNextItemWidth(300);
                    ImGui.InputText($"##tex_finder", ref _textureNameFinder, 256);

                    _modelFinderList.Render();
                    ImGui.SameLine();
                    _mtdFinderList.Render();
                    ImGui.SameLine();
                    _mtdTexTypeList.Render();
                }
                ImGui.End();
            }
        }

        private void FindTextures(List<FileNode> flverFileList)
        {
            _modelFinderList.Clear();
            _mtdFinderList.Clear();
            _mtdTexTypeList.Clear();

            foreach (var file in flverFileList)
            {
                try
                {
                    var binder = new FileBinders();
                    var operation = new FileOperation
                    {
                        GetObject = true
                    };
                    binder.ProcessPaths(new[] { file.VirtualPath }, operation);
                    FLVER2 flver_main = (FLVER2)binder.GetObject();

                    List<FLVER2.Material> flver_materials = flver_main.Materials;

                    if (flverTools.TexFinder(flver_materials, _textureNameFinder))
                    {
                        _modelFinderList.AddModel(file);
                        AddMaterialsFromFlver(flver_materials);
                    }
                    Console.WriteLine($"Done: {file}");
                }
                catch
                {
                    Console.WriteLine($"Fail: {file}");
                }
            }

            _mtdFinderList.UpdateMtdTypes(_mtdList);
        }

        private void AddMaterialsFromFlver(List<FLVER2.Material> flver_materials)
        {
            var materialPaths = new List<string>();
            flverTools.MTDFinderList(flver_materials, _textureNameFinder, materialPaths);
            materialPaths = materialPaths.Distinct().ToList();

            foreach (var materialPath in materialPaths)
            {
                _mtdFinderList.AddMaterial(materialPath);
            }
        }
    }
}