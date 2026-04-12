namespace DSRViewer.Core
{
    /// <summary>Предоставляет методы для выбора папок и файлов через диалоги и получения их содержимого.</summary>
    public class FileBrowser
    {
        /// <summary>Открывает диалог выбора папки и возвращает путь.</summary>
        public string SetFolderPath() => DialogHelper.SelectFolder();

        /// <summary>Открывает диалог выбора файла и возвращает путь.</summary>
        public string SetFilePath(string title, string filter) => DialogHelper.SelectFile(title, filter);

        /// <summary>Возвращает список файлов в указанной папке.</summary>
        public List<string> GetFileList(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return [];
            Console.WriteLine("Building list: ...");
            var result = Directory.GetFiles(folderPath).ToList();
            Console.WriteLine("Building list: Done");
            return result;
        }

        /// <summary>Возвращает список подпапок в указанной папке.</summary>
        public List<string> GetFolderList(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return [];
            Console.WriteLine("Building list: ...");
            var result = Directory.GetDirectories(folderPath).ToList();
            Console.WriteLine("Building list: Done");
            return result;
        }
    }
}
