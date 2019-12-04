using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Streams;

namespace Common
{
    class LZ4Archive : IArchive
    {
        void IArchive.addDirectory(string folder, CompressionLevel compressionLevel)
        {
            throw new NotImplementedException();
        }

        void IArchive.addFile(string file, string path, CompressionLevel compressionLevel)
        {
            throw new NotImplementedException();
        }

        void IArchive.close()
        {
            throw new NotImplementedException();
        }

        void IArchive.create()
        {
            throw new NotImplementedException();
        }

        Stream IArchive.createAndGetFileStream(string path, CompressionLevel compressionLevel)
        {
            throw new NotImplementedException();
        }

        void IArchive.getFile(string archivePath, string destinationPath)
        {
            throw new NotImplementedException();
        }

        List<string> IArchive.listEntries()
        {
            throw new NotImplementedException();
        }

        void IArchive.open(ZipArchiveMode mode)
        {
            throw new NotImplementedException();
        }

        Stream IArchive.openAndGetFileStream(string path)
        {
            throw new NotImplementedException();
        }
    }
}
