using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSRViewer.FileHelper.DDSHelper
{
    public class DDS_FlagFormatList
    {
        public static Dictionary<int, string> DDSFlagList = new()
        {
            {0, "BC1_UNORM"},
            {1, "BC1_UNORM"},
            {3, "BC2_UNORM"},
            {5, "BC3_UNORM"},
            {9, "B8G8R8A8_UNORM"},
            {10, "R8G8B8A8_UNORM"},
            {24, "BC1_UNORM"},
            {35, "R16G16_FLOAT"},
            {36, "BC5_UNORM"},
            {37, "BC6H_UF16"},
            {38, "BC7_UNORM"},
        };

        public static Dictionary<string, int> DDSFlagListSet = new()
        {
            {"BC1_UNORM", 1},
            {"BC2_UNORM", 3},
            {"BC3_UNORM", 5},
            {"B8G8R8A8_UNORM", 9},
            {"R8G8B8A8_UNORM", 10},
            {"R16G16_FLOAT", 35},
            {"BC5_UNORM", 36},
            {"BC6H_UF16", 37},
            {"BC7_UNORM", 38},
        };
    }
}
