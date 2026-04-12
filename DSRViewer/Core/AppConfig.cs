using System.Text.Json;

namespace DSRViewer.Core
{
    /// <summary>Данные конфигурации приложения, сериализуемые в JSON.</summary>
    public class ConfigData
    {
        public string GameFolder { get; set; } = "";
        public string ExtractFolder { get; set; } = "";
        public string MtdFolder { get; set; } = "";
        public bool LazyLoading { get; set; } = false;
    }

    /// <summary>Управляет конфигурацией приложения: загрузка, сохранение и выбор папок.</summary>
    public class Config
    {
        private readonly string _configName;
        private ConfigData _configData;

        public string GameFolder   => _configData.GameFolder;
        public string ExtractFolder => _configData.ExtractFolder;
        public string MtdFolder    => _configData.MtdFolder;
        public bool   LazyLoading  => _configData.LazyLoading;

        public void SetLazyLoading(bool value)
        {
            _configData.LazyLoading = value;
            Save();
        }

        public Config(string configName = "Default")
        {
            _configName = configName;
            _configData = new ConfigData();
            Load();
        }

        private string FilePath => Path.Combine(AppContext.BaseDirectory, $"{_configName}.json");

        /// <summary>Загружает конфигурацию из JSON-файла.</summary>
        public void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    _configData = JsonSerializer.Deserialize<ConfigData>(json) ?? new ConfigData();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config] Load failed: {ex.Message}");
            }
        }

        /// <summary>Сохраняет конфигурацию в JSON-файл.</summary>
        public void Save()
        {
            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_configData, opts);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config] Save failed: {ex.Message}");
            }
        }

        /// <summary>Открывает диалог выбора папки игры и сохраняет путь.</summary>
        public bool SelectGameFolder()    => SelectFolder("Select Game directory",    p => _configData.GameFolder = p);
        /// <summary>Открывает диалог выбора папки извлечения и сохраняет путь.</summary>
        public bool SelectExtractFolder() => SelectFolder("Select Extract directory", p => _configData.ExtractFolder = p);
        /// <summary>Открывает диалог выбора папки MTD и сохраняет путь.</summary>
        public bool SelectMtdFolder()     => SelectFolder("Select MTD directory",     p => _configData.MtdFolder = p);

        private bool SelectFolder(string description, Action<string> onSelected)
        {
            string path = DialogHelper.SelectFolder(description);
            if (string.IsNullOrEmpty(path)) return false;
            onSelected(path);
            Save();
            return true;
        }
    }
}
