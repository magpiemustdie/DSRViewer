using DSRViewer.Core;
using DSRViewer.FileProcess;
using DSRViewer.UI.Base;
using DSRViewer.Editors.MTDEditor;
using ImGuiNET;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace DSRViewer.UI.Windows
{
    /// <summary>Окно редактора MTD-материалов с просмотром и редактированием параметров.</summary>
    public class MTDWindow : ImGuiWindow
    {
        public MTDWindow(string windowName, bool showWindow) : base(windowName, showWindow)
        {
            _minSize = new(700, 500);
            _maxSize = new(float.MaxValue, float.MaxValue);
        }

        public MTDWindow(string windowName, bool showWindow, Config config) : this(windowName, showWindow)
        {
            _config = config;
            _mtdDir = _config.MtdFolder;
            if (!string.IsNullOrEmpty(_mtdDir))
                UpdateLists();
        }

        Config _config = new();

        List<MTDShortDetails> mtdList = [];
        string _mtdDir = "";
        MTDReader mtdReader = new();
        MTDTools _mtdTools = new();
        MTDParamEditor _paramEditor = new("MTD Param Editor", false);

        int selectedMTDList = -1;
        List<string> mtd_textype = [];

        // Для редактирования параметров
        private MTD currentMTD = null;
        private string currentMTDFilePath = "";

        // Словари для редактирования
        private Dictionary<string, bool> boolParams = new();
        private Dictionary<string, float> floatParams = new();
        private Dictionary<string, int> intParams = new();

        private Dictionary<string, Vector2> float2Params = new();
        private Dictionary<string, Vector3> float3Params = new();
        private Dictionary<string, Vector4> float4Params = new();

        private bool hasUnsavedChanges = false;

        public override void Render()
        {
            if (!_showWindow) return;

            ApplySizeConstraints();
            ImGui.Begin(_windowName, ref _showWindow, _windowFlags | ImGuiWindowFlags.MenuBar);
                    if (ImGui.BeginMenuBar())
                    {
                        if (ImGui.BeginMenu("File"))
                        {
                            if (ImGui.MenuItem("Set MTD path"))
                            {
                                _mtdDir = SetMTDDir();
                                UpdateLists();
                            }

                            if (ImGui.MenuItem("Reload MTDs"))
                            {
                                UpdateLists();
                            }

                            if (ImGui.MenuItem("Save Changes", "", false, hasUnsavedChanges))
                            {
                                SaveChanges();
                            }

                            ImGui.EndMenu();
                        }

                        if (ImGui.BeginMenu("MTD Tools"))
                        {
                            if (ImGui.MenuItem("Add material workflow"))
                                _mtdTools.MassAddMaterialWorkflow(_mtdDir);

                            if (ImGui.MenuItem("Merge with ptde mtds"))
                            {
                                string ptdePath = DialogHelper.SelectFolder("Select PTDE mtd folder");
                                if (!string.IsNullOrEmpty(ptdePath))
                                    _mtdTools.MergeWithPtdeMTDs(_mtdDir, ptdePath);
                            }

                            if (ImGui.MenuItem("Reduce specular"))
                                _mtdTools.ReduceSpecular(_mtdDir);

                            if (ImGui.MenuItem("Param editor..."))
                            {
                                _paramEditor.SetMtdDir(_mtdDir);
                                _paramEditor.ShowWindow(true);
                            }

                            if (ImGui.MenuItem("Export MW list to CSV"))
                            {
                                var paths = _mtdTools.ExportMWList(_mtdDir);
                                if (paths.Count > 0)
                                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{paths[0]}\"");
                            }

                            ImGui.EndMenu();
                        }

                        ImGui.EndMenuBar();
                    }

                    // Левая панель - список MTD
                    ImGui.BeginChild("MTD_List", new Vector2(400, -1), ImGuiChildFlags.Borders);
                    {
                        ImGui.Text("Available MTDs:");
                        ImGui.Separator();

                        for (int i = 0; i < mtdList.Count; i++)
                        {
                            var mtd = mtdList[i];
                            string label = $"{mtd.Name}";
                            if (!string.IsNullOrEmpty(mtd.MW.ToString()))
                                label += $" [{mtd.MW}]";

                            if (ImGui.Selectable(label, selectedMTDList == i))
                            {
                                selectedMTDList = i;
                                LoadMTDForEditing(i);
                            }

                            // Отображаем информацию при наведении
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text($"Name: {mtd.Name}");
                                ImGui.Text($"MaterialWorkflow: {mtd.MW}");
                                ImGui.Text($"Textures: {mtd.TexType.Count}");
                                ImGui.EndTooltip();
                            }
                        }
                    }
                    ImGui.EndChild();

                    ImGui.SameLine();

                    // Центральная панель - редактор параметров
                    ImGui.BeginChild("MTD_Editor", new Vector2(500, -1), ImGuiChildFlags.Borders);
                    {
                        if (currentMTD != null)
                        {
                            ImGui.Text($"Editing: {mtdList[selectedMTDList].Name}");
                            ImGui.Text($"Shader: {currentMTD.ShaderPath}");
                            ImGui.Separator();

                            if (hasUnsavedChanges)
                            {
                                ImGui.TextColored(new Vector4(1, 1, 0, 1), "★ Unsaved Changes!");
                            }

                            ImGui.Separator();

                            // Редактирование параметров
                            if (boolParams.Count > 0)
                            {
                                if (ImGui.CollapsingHeader("Boolean Parameters", ImGuiTreeNodeFlags.DefaultOpen))
                                {
                                    foreach (var param in boolParams.ToList())
                                    {
                                        bool value = param.Value;
                                        if (ImGui.Checkbox(param.Key, ref value))
                                        {
                                            boolParams[param.Key] = value;
                                            hasUnsavedChanges = true;
                                        }
                                    }
                                }
                            }

                            if (floatParams.Count > 0)
                            {
                                if (ImGui.CollapsingHeader("Float Parameters", ImGuiTreeNodeFlags.DefaultOpen))
                                {
                                    foreach (var param in floatParams.ToList())
                                    {
                                        float value = param.Value;
                                        if (ImGui.InputFloat(param.Key, ref value))
                                        {
                                            floatParams[param.Key] = value;
                                            hasUnsavedChanges = true;
                                        }
                                    }
                                }
                            }

                            if (intParams.Count > 0)
                            {
                                if (ImGui.CollapsingHeader("Integer Parameters", ImGuiTreeNodeFlags.DefaultOpen))
                                {
                                    foreach (var param in intParams.ToList())
                                    {
                                        int value = param.Value;
                                        if (ImGui.InputInt(param.Key, ref value))
                                        {
                                            intParams[param.Key] = value;
                                            hasUnsavedChanges = true;
                                        }
                                    }
                                }
                            }

                            if (float2Params.Count > 0)
                            {
                                if (ImGui.CollapsingHeader("Vector2 Parameters", ImGuiTreeNodeFlags.DefaultOpen))
                                {
                                    foreach (var param in float2Params.ToList())
                                    {
                                        Vector2 value = param.Value;
                                        if (ImGui.InputFloat2(param.Key, ref value))
                                        {
                                            float2Params[param.Key] = value;
                                            hasUnsavedChanges = true;
                                        }
                                    }
                                }
                            }

                            if (float3Params.Count > 0)
                            {
                                if (ImGui.CollapsingHeader("Vector3 Parameters", ImGuiTreeNodeFlags.DefaultOpen))
                                {
                                    foreach (var param in float3Params.ToList())
                                    {
                                        Vector3 value = param.Value;
                                        if (ImGui.InputFloat3(param.Key, ref value))
                                        {
                                            float3Params[param.Key] = value;
                                            hasUnsavedChanges = true;
                                        }
                                    }
                                }
                            }

                            if (float4Params.Count > 0)
                            {
                                if (ImGui.CollapsingHeader("Vector4 Parameters", ImGuiTreeNodeFlags.DefaultOpen))
                                {
                                    foreach (var param in float4Params.ToList())
                                    {
                                        Vector4 value = param.Value;
                                        if (ImGui.InputFloat4(param.Key, ref value))
                                        {
                                            float4Params[param.Key] = value;
                                            hasUnsavedChanges = true;
                                        }
                                    }
                                }
                            }

                            ImGui.Separator();

                            // Кнопки управления
                            if (ImGui.Button("Save Changes", new Vector2(150, 30)))
                            {
                                SaveChanges();
                            }

                            ImGui.SameLine();

                            if (ImGui.Button("Reload MTD", new Vector2(150, 30)))
                            {
                                LoadMTDForEditing(selectedMTDList);
                            }
                        }
                        else
                        {
                            ImGui.Text("Select an MTD to edit");
                            ImGui.Text("Path: " + _mtdDir);
                            ImGui.Text($"MTDs loaded: {mtdList.Count}");
                        }
                    }
                    ImGui.EndChild();

                    ImGui.SameLine();

                    // Правая панель - список текстур
                    ImGui.BeginChild("MTD_Textures", new Vector2(200, -1), ImGuiChildFlags.Borders);
                    {
                        ImGui.Text("Texture Samplers:");
                        ImGui.Separator();

                        if (currentMTD != null && currentMTD.Textures != null)
                        {
                            foreach (var sampler in currentMTD.Textures)
                            {
                                string label = $"{sampler.Type}";
                                if (!string.IsNullOrEmpty(sampler.Path))
                                    label += $" ({sampler.Path})";

                                ImGui.Text(label);

                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.BeginTooltip();
                                    ImGui.Text($"Type: {sampler.Type}");
                                    ImGui.Text($"Path: {sampler.Path}");
                                    ImGui.EndTooltip();
                                }
                            }
                        }
                        else if (mtd_textype.Count > 0)
                        {
                            // Используем старый список текстур, если есть
                            foreach (var texType in mtd_textype)
                            {
                                ImGui.Text(texType);
                            }
                        }
                        else
                        {
                            ImGui.Text("No texture samplers");
                        }
                    }
                    ImGui.EndChild();

            ImGui.End();

            if (_paramEditor.IsShowWindow())
                _paramEditor.Render();
        }

        private string SetMTDDir() =>
            DialogHelper.SelectFolder("Select MTD directory");

        /// <summary>Устанавливает путь к папке MTD по строке пути и обновляет списки.</summary>
        public void SetMTDPath(string mtdPath)
        {
            _mtdDir = mtdPath;
            UpdateLists();
        }

        /// <summary>Устанавливает путь к папке MTD из конфигурации и обновляет списки.</summary>
        public void SetMTDPath(Config config)
        {
            _mtdDir = config.MtdFolder;
            UpdateLists();
        }

        

        // Основной файл для записи (не Patch)
        private string ResolveMainBndPath() =>
            MTDReader.ResolveMainBndPath(_mtdDir);

        /// <summary>Перезагружает список MTD из всех архивов в текущей папке.</summary>
        public void UpdateLists()
        {
            try
            {
                List<MTDShortDetails> loaded = string.IsNullOrEmpty(_mtdDir)
                    ? []
                    : mtdReader.MTDViewer(_mtdDir);

                mtdList.Clear();
                mtdList.AddRange(loaded);
                currentMTD = null;
                hasUnsavedChanges = false;
                ClearEditVariables();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MTDWindow] Error updating MTD list: {ex.Message}");
                mtdList.Clear();
            }
        }

        private void ClearEditVariables()
        {
            boolParams.Clear();
            floatParams.Clear();
            intParams.Clear();
            float2Params.Clear();
            float3Params.Clear();
            float4Params.Clear();
        }

        private void LoadMTDForEditing(int index)
        {
            if (index < 0 || index >= mtdList.Count) return;

            string mainPath = ResolveMainBndPath();
            if (string.IsNullOrEmpty(mainPath)) return;

            try
            {
                currentMTD = mtdReader.LoadMTDByName(_mtdDir, mtdList[index].Name);
                currentMTDFilePath = mainPath;
                InitializeEditVariables();
                mtd_textype = mtdList[index].TexType;
                hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MTDWindow] Error loading MTD: {ex.Message}");
                currentMTD = null;
            }
        }

        private void InitializeEditVariables()
        {
            if (currentMTD == null)
                return;

            ClearEditVariables();

            // Заполняем словари значениями из MTD
            foreach (var param in currentMTD.Params)
            {
                switch (param.Type)
                {
                    case MTD.ParamType.Bool:
                        boolParams[param.Name] = (bool)param.Value;
                        break;
                    case MTD.ParamType.Float:
                        floatParams[param.Name] = (float)param.Value;
                        break;
                    case MTD.ParamType.Int:
                        intParams[param.Name] = (int)param.Value;
                        break;
                    case MTD.ParamType.Float2:
                        if (param.Value is float[] float2Array && float2Array.Length >= 2)
                            float2Params[param.Name] = new Vector2(float2Array[0], float2Array[1]);
                        else if (param.Value is Vector2 vec2)
                            float2Params[param.Name] = vec2;
                        break;
                    case MTD.ParamType.Float3:
                        if (param.Value is float[] float3Array && float3Array.Length >= 3)
                            float3Params[param.Name] = new Vector3(float3Array[0], float3Array[1], float3Array[2]);
                        else if (param.Value is Vector3 vec3)
                            float3Params[param.Name] = vec3;
                        break;
                    case MTD.ParamType.Float4:
                        if (param.Value is float[] float4Array && float4Array.Length >= 4)
                            float4Params[param.Name] = new Vector4(float4Array[0], float4Array[1], float4Array[2], float4Array[3]);
                        else if (param.Value is Vector4 vec4)
                            float4Params[param.Name] = vec4;
                        break;
                }
            }
        }

        private void SaveChanges()
        {
            if (currentMTD == null || string.IsNullOrEmpty(currentMTDFilePath)
                || !hasUnsavedChanges || selectedMTDList < 0 || selectedMTDList >= mtdList.Count)
                return;

            try
            {
                // Обновляем значения в MTD
                UpdateMTDValues();

                // Сохраняем изменения обратно в BND3 архив
                SaveMTDToBND(currentMTDFilePath, mtdList[selectedMTDList].Name, currentMTD);

                hasUnsavedChanges = false;
                Console.WriteLine("Changes saved successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving MTD: {ex.Message}");
            }
            
        }

        private void UpdateMTDValues()
        {
            if (currentMTD == null)
                return;

            // Обновляем значения параметров в MTD объекте
            foreach (var param in currentMTD.Params)
            {
                switch (param.Type)
                {
                    case MTD.ParamType.Bool:
                        if (boolParams.ContainsKey(param.Name))
                            param.Value = boolParams[param.Name];
                        break;
                    case MTD.ParamType.Float:
                        if (floatParams.ContainsKey(param.Name))
                            param.Value = floatParams[param.Name];
                        break;
                    case MTD.ParamType.Int:
                        if (intParams.ContainsKey(param.Name))
                            param.Value = intParams[param.Name];
                        break;
                    case MTD.ParamType.Float2:
                        if (float2Params.ContainsKey(param.Name))
                        {
                            Vector2 vec2 = float2Params[param.Name];
                            param.Value = new float[] { vec2.X, vec2.Y };
                        }
                        break;
                    case MTD.ParamType.Float3:
                        if (float3Params.ContainsKey(param.Name))
                        {
                            Vector3 vec3 = float3Params[param.Name];
                            param.Value = new float[] { vec3.X, vec3.Y, vec3.Z };
                        }
                        break;
                    case MTD.ParamType.Float4:
                        if (float4Params.ContainsKey(param.Name))
                        {
                            Vector4 vec4 = float4Params[param.Name];
                            param.Value = new float[] { vec4.X, vec4.Y, vec4.Z, vec4.W };
                        }
                        break;
                }
            }
        }

        private void SaveMTDToBND(string bndPath, string mtdName, MTD mtdData)
        {
            // Ищем файл во всех BND (Patch имеет приоритет)
            var files = MTDReader.ResolveMtdbndFiles(_mtdDir);
            string targetPath = files.LastOrDefault(f =>
            {
                try
                {
                    var b = MTDReader.ReadBnd(f);
                    return b.Files.Any(e => e.Name.Equals(mtdName, StringComparison.OrdinalIgnoreCase));
                }
                catch { return false; }
            }) ?? bndPath;

            try
            {
                var bnd = MTDReader.ReadBnd(targetPath);
                var entry = bnd.Files.FirstOrDefault(f =>
                    f.Name.Equals(mtdName, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    entry.Bytes = mtdData.Write();
                    bnd.Write(targetPath);
                    Console.WriteLine($"[MTDWindow] Saved '{mtdName}' to {Path.GetFileName(targetPath)}");
                }
                else
                {
                    Console.WriteLine($"[MTDWindow] MTD '{mtdName}' not found in any BND");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MTDWindow] Error saving MTD: {ex.Message}");
                throw;
            }
        }

        /// <summary>Возвращает загруженный список MTD.</summary>
        public List<MTDShortDetails> GetMTDList() => mtdList;
        /// <summary>Возвращает путь к папке MTD.</summary>
        public string GetMTDFolder() => _mtdDir;
    }
}