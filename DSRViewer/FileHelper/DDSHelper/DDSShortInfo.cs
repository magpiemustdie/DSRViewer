using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DSRViewer.FileHelper.DDSHelper
{
    internal class DDSShortInfo
    {
        public int ID { get; set; }
        public string Name { get; set; }

        public byte Format { get; set; }
        public byte[] Bytes { get; set; }
    }
}
