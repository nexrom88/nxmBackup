using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;

namespace Common
{
    public interface IArchive
    {
        //creates a new zip archive
        void create();

        //opens an existing zip archive
        void open(ZipArchiveMode mode);




    }
}
