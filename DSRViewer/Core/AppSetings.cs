using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DSRFileViewer
{
    public class Config
    {
        public static string game_Folder { get; set; } = string.Empty;
        public static string extract_Folder { get; set; } = string.Empty;
        public static string mtd_Folder { get; set; } = string.Empty;

        public Config()
        {
            SetGameFolder();
            SetMTDFolder();
            SetExtractFolder();
        }
        public void SetGameFolder()
        {
            if (File.Exists("AppConfig.json"))
            {
                AppConfig appConfig = new();
                appConfig = AppConfig.Load("AppConfig.json");
                Console.WriteLine($"Game folder: {appConfig.GameFolder}");
                if (Directory.Exists(appConfig.GameFolder))
                    game_Folder = appConfig.GameFolder;
                if (game_Folder == null | game_Folder == "")
                    SelectGameFolder();
            }
            else
            {
                SelectGameFolder();
            }
        }

        private void SelectGameFolder()
        {
            using (var folderDialog = new FolderBrowserDialog()) //Windows dialog
            {
                folderDialog.Description = "Select game directory";
                folderDialog.UseDescriptionForTitle = true;
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    game_Folder = folderDialog.SelectedPath;
                    AppConfig appConfig = new();
                    if (File.Exists("AppConfig.json"))
                        appConfig = AppConfig.Load("AppConfig.json");
                    appConfig.GameFolder = game_Folder;
                    appConfig.Save("AppConfig.json");
                }
            }
        }

        public void SetExtractFolder()
        {
            if (File.Exists("AppConfig.json"))
            {
                AppConfig appConfig = new();
                appConfig = AppConfig.Load("AppConfig.json");
                Console.WriteLine($"Extract folder: {appConfig.ExtractFolder}");
                if (Directory.Exists(appConfig.ExtractFolder))
                    extract_Folder = appConfig.ExtractFolder;
                if (extract_Folder == null | extract_Folder == "")
                    SelectExtractFolder();
            }
            else
            {
                SelectExtractFolder();
            }
        }

        private void SelectExtractFolder()
        {
            using (var folderDialog = new FolderBrowserDialog()) //Windows dialog
            {
                folderDialog.Description = "Select extract directory";
                folderDialog.UseDescriptionForTitle = true;
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    extract_Folder = folderDialog.SelectedPath;
                    AppConfig appConfig = new();
                    if (File.Exists("AppConfig.json"))
                        appConfig = AppConfig.Load("AppConfig.json");
                    appConfig.ExtractFolder = extract_Folder;
                    appConfig.Save("AppConfig.json");
                }
            }
        }

        public void SetMTDFolder()
        {
            if (File.Exists("AppConfig.json"))
            {
                AppConfig appConfig = new();
                appConfig = AppConfig.Load("AppConfig.json");
                Console.WriteLine($"MTD folder: {appConfig.MTDFolder}");
                if (Directory.Exists(appConfig.MTDFolder))
                    mtd_Folder = appConfig.MTDFolder;
                if (mtd_Folder == null | mtd_Folder == "")
                    SelectMTDFolder();
            }
            else
            {
                File.Create("AppConfig.json");
                SelectMTDFolder();
            }
        }

        private void SelectMTDFolder()
        {
            using (var folderDialog = new FolderBrowserDialog()) //Windows dialog
            {
                folderDialog.Description = "Select mtd directory";
                folderDialog.UseDescriptionForTitle = true;
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    mtd_Folder = folderDialog.SelectedPath;
                    AppConfig appConfig = new();
                    if (File.Exists("AppConfig.json"))
                        appConfig = AppConfig.Load("AppConfig.json");
                    appConfig.MTDFolder = mtd_Folder;
                    appConfig.Save("AppConfig.json");
                }
            }
        }
    }
    class AppConfig
    {
        public string GameFolder { get; set; }
        public string MTDFolder { get; set; }
        public string ExtractFolder { get; set; }

        public static AppConfig Load(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<AppConfig>(json);
        }

        public void Save(string filePath)
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
    }
}
