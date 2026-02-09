using System.Text.Json;
using System.Windows.Forms;

namespace DSRViewer.Core
{
    public class Config
    {
        private string _configName;
        private AppConfig _configData;

        public string GameFolder => _configData.GameFolder;
        public string ExtractFolder => _configData.ExtractFolder;
        public string MtdFolder => _configData.MtdFolder;

        public Config(string configName = "Default")
        {
            _configName = configName;
            _configData = new AppConfig();
            Load();
        }

        public void Load()
        {
            try
            {
                string filePath = $"{_configName}.json";
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    _configData = JsonSerializer.Deserialize<AppConfig>(json);
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                string filePath = $"{_configName}.json";
                string json = JsonSerializer.Serialize(_configData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch { }
        }

        public bool SelectGameFolder()
        {
            bool success = false;
            var thread = new Thread(() =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select Game directory";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        _configData.GameFolder = dialog.SelectedPath;
                        Save();
                        success = true;
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return success;
        }

        public bool SelectExtractFolder()
        {
            bool success = false;
            var thread = new Thread(() =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select Extract directory";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        _configData.ExtractFolder = dialog.SelectedPath;
                        Save();
                        success = true;
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return success;
        }

        public bool SelectMtdFolder()
        {
            bool success = false;
            var thread = new Thread(() =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select MTD directory";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        _configData.MtdFolder = dialog.SelectedPath;
                        Save();
                        success = true;
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return success;
        }

        public class AppConfig
        {
            public string GameFolder { get; set; } = "";
            public string ExtractFolder { get; set; } = "";
            public string MtdFolder { get; set; } = "";
        }
    }
}