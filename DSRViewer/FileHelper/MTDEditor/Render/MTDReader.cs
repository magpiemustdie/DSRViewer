using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using SoulsFormats;

namespace DSRViewer.FileHelper.MTDEditor.Render
{
    public class MTDReader //MTD viewer in folder
    {
        public List<MTDShortDetails> MTDViewer(string path)
        {
            List<MTDShortDetails> mtdList = [];


            BND3 mtdbnd = BND3.Read(path);

            foreach (var mtds in mtdbnd.Files)
            {
                List<string> mtdtex = [];

                MTD mtd = MTD.Read(mtds.Bytes);
                int mw = 0;

                foreach (var tex in mtd.Textures)
                {
                    mtdtex.Add(tex.Type);
                }

                bool mw_test = false;

                foreach (var prm in mtd.Params)
                {
                    if (prm.Name == "g_MaterialWorkflow")
                    {
                        mw = (int)prm.Value;
                    }
                }

                mtdList.Add(new MTDShortDetails
                {
                    Name = mtds.Name,
                    MW = mw,
                    TexType = mtdtex
                });
            }

            return mtdList;
        }

        public MTD LoadMTDByName(string path, string name)
        {
            List<MTDShortDetails> mtdList = [];
            MTD mtd = new();

            BND3 mtdbnd = BND3.Read(path);

            foreach (var mtds in mtdbnd.Files)
            {
                if (name == mtds.Name)
                {
                    mtd = MTD.Read(mtds.Bytes);
                }
            }

            return mtd;
        }
    }
}
