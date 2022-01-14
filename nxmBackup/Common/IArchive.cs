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
        //creates a new archive
        void create();

        //opens an existing archive
        void open(ZipArchiveMode mode);

        //closes the archive
        void close();

        //creates a new entry and returns the io-strem
        System.IO.Stream createAndGetFileStream(string path);

        //opens an entry and returns the io-stream
        System.IO.Stream openAndGetFileStream(string path);

        //adds a file to the archive
        TransferDetails addFile(string file, string path);

        //adds a whole folder to the archive
        void addDirectory(string folder);

        //gets a file from the archive
        bool getFile(string archivePath, string destinationPath);

        //lists all archive entries
        List<string> listEntries();
    }


}
