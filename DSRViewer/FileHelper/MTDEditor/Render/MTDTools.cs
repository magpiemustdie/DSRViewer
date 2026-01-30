using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;

namespace DSRViewer.FileHelper.MTDEditor.Render
{
    public class MTDTools
    {
        public void MassAddMaterialWorkflow(string path)
        {
            BND3 mtdbnd = BND3.Read(path);

            foreach (var mtds in mtdbnd.Files)
            {
                MTD mtd = MTD.Read(mtds.Bytes);
                int mw = 0;
                bool mw_test = false;

                foreach (var prm in mtd.Params)
                {
                    if (prm.Name == "g_MaterialWorkflow")
                    {
                        mw = (int)prm.Value;
                        mw_test = true;
                    }
                }

                if (!mw_test)
                {
                    mtd.Params.Add(new MTD.Param());
                    mtd.Params.Last().Name = "g_MaterialWorkflow";
                }
                mtds.Bytes = mtd.Write();
            }

            mtdbnd.Write(path);

            Console.WriteLine("Material workflow added");
        }

        public void AddMaterialWorkflow(string path, string mtdName)
        {
            BND3 mtdbnd = BND3.Read(path);

            foreach (var mtds in mtdbnd.Files)
            {
                if (mtds.Name == mtdName)
                {
                    MTD mtd = MTD.Read(mtds.Bytes);
                    int mw = 0;
                    bool mw_test = false;

                    foreach (var prm in mtd.Params)
                    {
                        if (prm.Name == "g_MaterialWorkflow")
                        {
                            mw = (int)prm.Value;
                            mw_test = true;
                        }
                    }

                    if (!mw_test)
                    {
                        mtd.Params.Add(new MTD.Param());
                        mtd.Params.Last().Name = "g_MaterialWorkflow";
                    }
                    mtds.Bytes = mtd.Write();
                }
            }

            mtdbnd.Write(path);

            Console.WriteLine("Material workflow added");
        }

        public void SwapMaterialWorkflow(string path, string mtdName)
        {
            BND3 mtdbnd = BND3.Read(path);

            foreach (var mtds in mtdbnd.Files)
            {
                if (mtds.Name == mtdName)
                {
                    MTD mtd = MTD.Read(mtds.Bytes);
                    int mw = 0;
                    bool mw_test = false;

                    foreach (var prm in mtd.Params)
                    {
                        if (prm.Name == "g_MaterialWorkflow")
                        {
                            mw = (int)prm.Value;
                            mw_test = true;
                        }
                    }

                    if (!mw_test)
                    {
                        mtd.Params.Add(new MTD.Param());
                        mtd.Params.Last().Name = "g_MaterialWorkflow";
                    }

                    foreach (var prm in mtd.Params)
                    {
                        if (prm.Name == "g_MaterialWorkflow")
                        {
                            prm.Value = Convert.ToInt32(!Convert.ToBoolean(mw));
                        }
                    }
                    mtds.Bytes = mtd.Write();
                }
            }

            mtdbnd.Write(path);

            Console.WriteLine("Material workflow added and changed");
        }

        public void Unpack(string path)
        {
            var file = DCX.Decompress(path);
            File.WriteAllBytes("Unpacked mtd", file);
            Console.WriteLine("Unpack done");
        }
    }
}
