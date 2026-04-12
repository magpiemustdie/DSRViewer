using System.Windows.Forms;

namespace DSRViewer.Core
{
    /// <summary>Вспомогательный класс для открытия системных диалогов выбора файлов и папок.</summary>
    public static class DialogHelper
    {
        /// <summary>Открывает диалог выбора папки и возвращает выбранный путь.</summary>
        public static string SelectFolder(string description = "Select a directory")
        {
            string result = "";
            var thread = new Thread(() =>
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = description,
                    UseDescriptionForTitle = true
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                    result = dialog.SelectedPath;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }

        /// <summary>Открывает диалог выбора одного файла и возвращает путь к нему.</summary>
        public static string SelectFile(string title, string filter, bool multiselect = false)
        {
            string result = "";
            var thread = new Thread(() =>
            {
                using var dialog = new OpenFileDialog
                {
                    Title = title,
                    Filter = filter,
                    Multiselect = multiselect
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                    result = dialog.FileName;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }

        /// <summary>Открывает диалог выбора нескольких файлов и возвращает массив путей.</summary>
        public static string[] SelectFiles(string title, string filter)
        {
            string[] result = [];
            var thread = new Thread(() =>
            {
                using var dialog = new OpenFileDialog
                {
                    Title = title,
                    Filter = filter,
                    Multiselect = true
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                    result = dialog.FileNames;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }
    }
}
