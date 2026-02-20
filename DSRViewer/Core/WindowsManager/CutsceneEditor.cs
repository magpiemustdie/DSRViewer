using DSRViewer.ImGuiHelper;
using ImGuiNET;
using SoulsFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace DSRViewer.Core.WindowsManager
{
    public class CutsceneEditor : ImGuiWindow
    {
        public CutsceneEditor()
        {
            _windowName = "Remo tae editor";
        }
        public CutsceneEditor(string windowName, bool showWindow)
        {
            _windowName = windowName;
            _showWindow = showWindow;
        }

        FileBrowser _fileBrowser = new();
        string _filePath = "";

        private BND3? _bnd;
        private TAE? _currentTae;
        private List<BinderFile> _taeFiles = new(); // Список всех .tae файлов в BND
        private int _selectedTaeIndex = -1;         // Индекс выбранного .tae файла
        private List<TAE.Animation> _allAnimations = [];
        private int _selectedEventIndex = -1;
        private int _selectedAnimIndex = -1;
        private string _editHexString = "";
        private string _errorMessage = "";

        public override void Render()
        {
            if (!_showWindow) return;
            ImGui.Begin(_windowName, ref _showWindow, _windowFlags);
            {
                if (ImGui.Button("Set remo file"))
                {
                    _filePath = _fileBrowser.SetFilePath("Open remo file", "Remo files (*.remobnd)|*.remobnd; *.remobnd.dcx|All (*.*)|*.*");
                    LoadFile();
                }

                if (_bnd != null)
                {
                    ImGui.Separator();

                    if (_taeFiles.Count > 0)
                    {
                        // Выбор .tae файла из списка
                        string[] taeNames = _taeFiles.Select(f => f.Name).ToArray();
                        ImGui.Combo("Select TAE file", ref _selectedTaeIndex, taeNames, taeNames.Length);

                        // Если выбранный файл изменился, загружаем его
                        if (ImGui.IsItemEdited())
                        {
                            LoadSelectedTae();
                        }

                        ImGui.Separator();

                        if (_currentTae != null)
                        {
                            RenderEventList();
                            RenderEventEditor();
                        }
                    }
                    else
                    {
                        ImGui.Text("No .tae files found in this BND.");
                    }
                }
            }
            ImGui.End();
        }

        private void LoadFile()
        {
            try
            {
                _bnd = BND3.Read(_filePath);
                _taeFiles = _bnd.Files.Where(f => f.Name.EndsWith(".tae", StringComparison.OrdinalIgnoreCase)).ToList();
                _selectedTaeIndex = -1;
                _currentTae = null;
                _allAnimations.Clear();
                _selectedAnimIndex = -1;
                _selectedEventIndex = -1;
                _editHexString = "";
                _errorMessage = "";

                if (_taeFiles.Count > 0)
                {
                    _selectedTaeIndex = 0; // Автоматически выбираем первый файл
                    LoadSelectedTae();
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error loading file: {ex.Message}";
            }
        }

        private void LoadSelectedTae()
        {
            if (_selectedTaeIndex < 0 || _selectedTaeIndex >= _taeFiles.Count)
                return;

            try
            {
                var taeFile = _taeFiles[_selectedTaeIndex];
                _currentTae = TAE.Read(taeFile.Bytes);
                BuildEventList();
                _selectedAnimIndex = -1;
                _selectedEventIndex = -1;
                _editHexString = "";
                _errorMessage = "";
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error loading TAE: {ex.Message}";
                _currentTae = null;
            }
        }

        private void BuildEventList()
        {
            _allAnimations.Clear();
            if (_currentTae == null) return;

            foreach (var item in _currentTae.Animations)
            {
                _allAnimations.Add(item);
            }
        }

        private void RenderEventList()
        {
            ImGui.BeginChild("EventList", new Vector2(300, 0));
            ImGui.Text($"Animations ({_allAnimations.Count})");

            for (int i = 0; i < _allAnimations.Count; i++)
            {
                var anim = _allAnimations[i];
                string label = $"[{i}] Anim ID: {anim.ID}";

                if (ImGui.Selectable(label, _selectedAnimIndex == i))
                {
                    _selectedAnimIndex = i;
                    _selectedEventIndex = -1;
                    _editHexString = "";
                    _errorMessage = "";
                }

                if (_selectedAnimIndex == i && anim.Events.Count > 0)
                {
                    ImGui.Indent();
                    for (int j = 0; j < anim.Events.Count; j++)
                    {
                        var ev = anim.Events[j];
                        string eventLabel = $"  [Event {j}] Type: {ev.GetType().Name}";

                        if (ImGui.Selectable(eventLabel, _selectedEventIndex == j))
                        {
                            _selectedEventIndex = j;
                            UpdateEditHexFromEvent(anim.Events[j]);
                            _errorMessage = "";
                        }
                    }
                    ImGui.Unindent();
                }
            }

            ImGui.EndChild();
        }

        private void UpdateEditHexFromEvent(TAE.Event ev)
        {
            byte[] bytes = ev.GetParameterBytes(true);
            string hex = BitConverter.ToString(bytes).Replace("-", "");
            _editHexString = FormatHexWithSpaces(hex);
        }

        private string FormatHexWithSpaces(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return "";

            var sb = new StringBuilder();
            for (int i = 0; i < hex.Length; i += 2)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(hex.Substring(i, Math.Min(2, hex.Length - i)));
            }
            return sb.ToString();
        }

        private void RenderEventEditor()
        {
            ImGui.SameLine();
            ImGui.BeginChild("EventEditor", new Vector2(0, 0));

            if (_selectedAnimIndex >= 0 && _selectedEventIndex >= 0)
            {
                var anim = _allAnimations[_selectedAnimIndex];
                var ev = anim.Events[_selectedEventIndex];

                ImGui.Text($"Editing Event {_selectedEventIndex} of Animation {anim.ID}");
                ImGui.Text($"Event Type: {ev.GetType().Name}");
                ImGui.Separator();

                byte[] currentBytes = ev.GetParameterBytes(true);
                ImGui.Text($"Current bytes ({currentBytes.Length}):");
                ImGui.TextWrapped(BitConverter.ToString(currentBytes).Replace("-", " "));

                ImGui.Spacing();

                ImGui.InputText("Hex (spaces allowed)", ref _editHexString, 10000);

                if (ImGui.Button("Apply to Event"))
                {
                    ApplyHexToEvent(ev);
                }

                ImGui.SameLine();
                if (ImGui.Button("Save to File"))
                {
                    SaveFile();
                }

                if (!string.IsNullOrEmpty(_errorMessage))
                {
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), _errorMessage);
                }
            }
            else
            {
                ImGui.Text("Select an animation and an event to edit.");
            }

            ImGui.EndChild();
        }

        private void ApplyHexToEvent(TAE.Event ev)
        {
            try
            {
                string hex = _editHexString.Replace(" ", "").Replace("-", "").ToUpperInvariant();
                if (string.IsNullOrEmpty(hex))
                {
                    _errorMessage = "Hex string is empty.";
                    return;
                }

                if (hex.Length % 2 != 0)
                {
                    _errorMessage = "Hex string length must be even.";
                    return;
                }

                byte[] newBytes = new byte[hex.Length / 2];
                for (int i = 0; i < newBytes.Length; i++)
                {
                    newBytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                }

                ev.SetParameterBytes(true, newBytes);
                _errorMessage = "";
                UpdateEditHexFromEvent(ev);
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error parsing hex: {ex.Message}";
            }
        }

        private void SaveFile()
        {
            try
            {
                if (_bnd == null || _currentTae == null || _selectedTaeIndex < 0)
                {
                    _errorMessage = "No file loaded or no TAE selected.";
                    return;
                }

                var taeFile = _taeFiles[_selectedTaeIndex];
                byte[] newTaeBytes = _currentTae.Write();
                taeFile.Bytes = newTaeBytes;
                _bnd.Write(_filePath);

                _errorMessage = "";
                ImGui.OpenPopup("SaveSuccess");
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error saving file: {ex.Message}";
            }
        }
    }
}