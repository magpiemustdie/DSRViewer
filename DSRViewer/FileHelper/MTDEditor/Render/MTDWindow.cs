using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using SoulsFormats;
using DSRViewer.ImGuiHelper;
using System.Windows.Forms;
using System.IO;
using DSRViewer.FileHelper.FileExplorer.Render;

namespace DSRViewer.FileHelper.MTDEditor.Render
{
    public class MTDWindow : ImGuiWindow
    {
        public MTDWindow(string windowName, bool showWindow)
        {
            _windowName = windowName;
            _showWindow = showWindow;
        }

        public MTDWindow(string windowName, bool showWindow, Config config)
        {
            _windowName = windowName;
            _showWindow = showWindow;
            _config = config;
        }

        Config _config;

        List<MTDShortDetails> mtdList = [];
        string _mtdDir = "";
        MTDReader mtdReader = new();
        MTDTools mtdTools = new();

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

        private string SetMTDDir()
        {
            var file = "";
            var thread = new Thread(() =>
            {
                using (var folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select MTD directory";
                    folderDialog.UseDescriptionForTitle = true;
                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        file = folderDialog.SelectedPath;
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return file;
        }

        public void SetMTDPath(string mtdPath)
        {
            _mtdDir = mtdPath;
            UpdateLists();
        }

        public void SetMTDPath(Config config)
        {
            _mtdDir = config.MtdFolder;
            UpdateLists();
        }

        public override void Render()
        {
            if (_showWindow)
            {
                ImGui.Begin(_windowName, ref _showWindow, _windowFlags | ImGuiWindowFlags.MenuBar);
                {
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

                        if (ImGui.BeginMenu("Tools"))
                        {
                            if (ImGui.MenuItem("Unpack all MTDs"))
                            {
                                mtdTools.Unpack(Path.Combine(_mtdDir, "mtd.mtdbnd"));
                            }

                            ImGui.EndMenu();
                        }

                        ImGui.EndMenuBar();
                    }

                    // Левая панель - список MTD
                    ImGui.BeginChild("MTD_List", new Vector2(400, -1), _childFlags);
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
                    ImGui.BeginChild("MTD_Editor", new Vector2(500, -1), _childFlags);
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
                    ImGui.BeginChild("MTD_Textures", new Vector2(200, -1), _childFlags);
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
                }
                ImGui.End();
            }
        }

        public void UpdateLists()
        {
            try
            {
                // Для Dark Souls Remastered используем путь к mtdbnd файлу
                string mtdbndPath = Path.Combine(_mtdDir, "mtd.mtdbnd.dcx");

                if (File.Exists(mtdbndPath))
                {
                    mtdList = mtdReader.MTDViewer(mtdbndPath);
                }
                else
                {
                    // Попробуем найти с другим именем
                    var files = Directory.GetFiles(_mtdDir, "*.mtdbnd.dcx");
                    if (files.Length > 0)
                    {
                        mtdbndPath = files[0];
                        mtdList = mtdReader.MTDViewer(mtdbndPath);
                    }
                }

                // Очищаем текущий MTD при обновлении списка
                currentMTD = null;
                hasUnsavedChanges = false;
                ClearEditVariables();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating MTD list: {ex.Message}");
                mtdList = [];
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
            if (index < 0 || index >= mtdList.Count)
                return;

            var mtdDetails = mtdList[index];

            // Находим путь к mtdbnd файлу
            string mtdbndPath = Path.Combine(_mtdDir, "mtd.mtdbnd.dcx");
            if (!File.Exists(mtdbndPath))
            {
                var files = Directory.GetFiles(_mtdDir, "*.mtdbnd.dcx");
                if (files.Length > 0)
                {
                    mtdbndPath = files[0];
                }
            }

            if (File.Exists(mtdbndPath))
            {
                try
                {
                    // Загружаем MTD
                    currentMTD = mtdReader.LoadMTDByName(mtdbndPath, mtdList[selectedMTDList].Name);
                    currentMTDFilePath = mtdbndPath;

                    // Инициализируем переменные для редактирования
                    InitializeEditVariables();

                    // Загружаем информацию о текстурах
                    mtd_textype = mtdDetails.TexType;

                    hasUnsavedChanges = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading MTD: {ex.Message}");
                    currentMTD = null;
                }
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
            
            if (currentMTD == null || string.IsNullOrEmpty(currentMTDFilePath) || !hasUnsavedChanges)
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
            try
            {
                // Читаем BND3 архив
                BND3 bnd = BND3.Read(bndPath);

                // Ищем файл MTD в архиве
                var mtdFile = bnd.Files.FirstOrDefault(f =>
                    f.Name.Contains(mtdName, StringComparison.OrdinalIgnoreCase));

                if (mtdFile != null)
                {
                    // Записываем MTD в байты
                    byte[] mtdBytes = mtdData.Write();
                    mtdFile.Bytes = mtdBytes;

                    // Сохраняем BND3 архив
                    bnd.Write(bndPath);
                }
                else
                {
                    Console.WriteLine($"MTD file {mtdName} not found in BND archive");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving MTD to BND: {ex.Message}");
                throw;
            }
        }


        public List<MTDShortDetails> GetMTDList() => mtdList;
        public string GetMTDFolder() => _mtdDir;
    }
}