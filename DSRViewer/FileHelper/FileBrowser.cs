using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DSRViewer.FileHelper
{
    public class FileBrowser
    {
        string file = "";

        List<string> fileList = [];

        public string SetFolderPath()
        {
            file = "";
            var thread = new Thread(() =>
            {
                using (var folderDialog = new FolderBrowserDialog()) //Windows dialog
                {
                    folderDialog.Description = "Select a directory";
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

        public string SetFilePath()
        {
            file = "";
            var thread = new Thread(() =>
            {
                using (var fileDialog = new OpenFileDialog()) //Windows dialog
                {
                    fileDialog.Title = "Open flver";
                    fileDialog.Filter = "Flver files (*.flver)|*.flver|Flver files (*.flver.dcx)|*.flver.dcx|All (*.*)|*.*";
                    if (fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        file = fileDialog.FileName;
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return file;
        }

        public List<string> GetFileList(string folderPath)
        {
            Console.WriteLine("Building list: ...");
            fileList = [.. Directory.GetFiles(folderPath)];
            Console.WriteLine("Building list: Done");

            return fileList;
        }

        public List<string> GetFolderList(string folderPath)
        {
            Console.WriteLine("Building list: ...");
            fileList = [.. Directory.GetDirectories(folderPath)];
            Console.WriteLine("Building list: Done");

            return fileList;
        }
    }
}
