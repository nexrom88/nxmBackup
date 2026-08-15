using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;

namespace Common
{
    //Parses VMware VMDK disk descriptors and sparse extent headers.
    //Supports the monolithicSparse, twoGbMaxExtentSparse and streamOptimized
    //extent formats. The descriptor is plain text and the binary sparse extents
    //follow the VMDK on-disk specification (little endian).
    public class vmdkParser : IDisposable
    {
        private FileStream sourceStream;
        private string sourceFile;

        //supported VMDK create types parsed from the descriptor
        public enum CreateType
        {
            Unknown,
            MonolithicSparse,
            MonolithicFlat,
            TwoGbMaxExtentSparse,
            TwoGbMaxExtentFlat,
            StreamOptimized,
            Vmfs,
            VmfsSparse,
            SeSparse
        }

        //supported extent access modes
        public enum AccessMode
        {
            Unknown,
            RW,
            RDONLY,
            NOACCESS
        }

        public vmdkParser(string file)
        {
            this.sourceFile = file;
            try
            {
                this.sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (Exception)
            {
                this.sourceStream = null;
            }
        }

        //returns the raw VMDK descriptor as text. For monolithic sparse disks the
        //descriptor is embedded in the header sector (offset 512). For descriptor-only
        //VMDKs (.vmdk text file) it is read from the beginning of the file.
        public string parseDescriptor()
        {
            if (this.sourceStream == null)
            {
                return null;
            }

            //probe the first bytes to detect an embedded descriptor
            this.sourceStream.Seek(0, SeekOrigin.Begin);
            byte[] magicBuffer = new byte[4];
            int read = this.sourceStream.Read(magicBuffer, 0, 4);

            //KDMV magic ("VMDK" little endian) signals an embedded binary sparse extent
            if (read == 4 && magicBuffer[0] == 0x4B && magicBuffer[1] == 0x44 && magicBuffer[2] == 0x4D && magicBuffer[3] == 0x56)
            {
                //descriptor is embedded starting at sector 1 (offset 512)
                this.sourceStream.Seek(512, SeekOrigin.Begin);
                byte[] descBuffer = new byte[20 * 512]; //descriptor is at most 20 sectors
                int descRead = this.sourceStream.Read(descBuffer, 0, descBuffer.Length);

                //the descriptor is zero terminated
                int len = Array.IndexOf(descBuffer, (byte)0);
                if (len < 0 || len > descRead)
                {
                    len = descRead;
                }

                return Encoding.ASCII.GetString(descBuffer, 0, len);
            }

            //otherwise treat the whole file as a plain text descriptor
            this.sourceStream.Seek(0, SeekOrigin.Begin);
            byte[] rawDescriptor = new byte[this.sourceStream.Length];
            int total = this.sourceStream.Read(rawDescriptor, 0, rawDescriptor.Length);
            return Encoding.ASCII.GetString(rawDescriptor, 0, total);
        }

        //parses the descriptor into a structured VmdkDescriptor object
        public VmdkDescriptor parseDescriptorTable()
        {
            string descriptorText = this.parseDescriptor();
            VmdkDescriptor descriptor = new VmdkDescriptor();
            descriptor.extents = new List<VmdkExtentDescription>();
            descriptor.extendedProperties = new Dictionary<string, string>();
            descriptor.createType = CreateType.Unknown;

            if (string.IsNullOrEmpty(descriptorText))
            {
                descriptor.isValid = false;
                return descriptor;
            }

            descriptor.isValid = true;

            //normalize line endings and split into lines
            string[] lines = descriptorText.Replace("\r\n", "\n").Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                {
                    continue;
                }

                //descriptor lines are either "key value" pairs or extent definitions
                //like "RW 41943040 SPARSE \"disk.vmdk\"". Extent definitions start with
                //a known access mode token.
                string firstToken = line.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[0];
                bool isExtent = firstToken.Equals("RW", StringComparison.OrdinalIgnoreCase)
                            || firstToken.Equals("RDONLY", StringComparison.OrdinalIgnoreCase)
                            || firstToken.Equals("NOACCESS", StringComparison.OrdinalIgnoreCase);

                if (isExtent)
                {
                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        VmdkExtentDescription extent = new VmdkExtentDescription();
                        extent.access = parseAccessMode(parts[0]);

                        if (ulong.TryParse(parts[1], out ulong sectors))
                        {
                            extent.numSectors = sectors;
                        }

                        extent.type = parts[2].Trim('"');
                        extent.fileName = parts[3].Trim('"');

                        //optional extent offset for flat extents
                        if (parts.Length >= 5 && ulong.TryParse(parts[4], out ulong extentOffset))
                        {
                            extent.rangeOffset = extentOffset;
                        }

                        descriptor.extents.Add(extent);
                    }
                    continue;
                }

                //key value pairs: key value (value may be quoted)
                int spaceIdx = line.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    string key = line.Substring(0, spaceIdx).Trim();
                    string value = line.Substring(spaceIdx + 1).Trim().Trim('"');

                    if (key.Equals("version", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(value, out descriptor.version);
                    }
                    else if (key.Equals("encoding", StringComparison.OrdinalIgnoreCase))
                    {
                        descriptor.encoding = value;
                    }
                    else if (key.Equals("CID", StringComparison.OrdinalIgnoreCase))
                    {
                        descriptor.CID = value;
                    }
                    else if (key.Equals("parentCID", StringComparison.OrdinalIgnoreCase))
                    {
                        descriptor.parentCID = value;
                    }
                    else if (key.Equals("createType", StringComparison.OrdinalIgnoreCase))
                    {
                        descriptor.createType = parseCreateType(value);
                        descriptor.createTypeString = value;
                    }
                    else if (key.Equals("parentFileNameHint", StringComparison.OrdinalIgnoreCase))
                    {
                        descriptor.parentFileNameHint = value;
                    }
                    else
                    {
                        descriptor.extendedProperties[key] = value;
                    }
                }
            }

            return descriptor;
        }

        //parses the sparse extent header of the first sparse extent within the file.
        //For monolithic sparse VMDKs the sparse extent immediately follows the embedded
        //descriptor. For extent files the header is at the beginning of the extent file.
        public SparseExtentHeader parseSparseExtentHeader()
        {
            if (this.sourceStream == null)
            {
                return null;
            }

            //check whether the file starts with the KDMV magic
            this.sourceStream.Seek(0, SeekOrigin.Begin);
            byte[] magic = new byte[4];
            int read = this.sourceStream.Read(magic, 0, 4);
            if (read != 4 || magic[0] != 0x4B || magic[1] != 0x44 || magic[2] != 0x4D || magic[3] != 0x56)
            {
                //no embedded sparse extent at offset 0
                return null;
            }

            //read the full 512 byte header sector
            this.sourceStream.Seek(0, SeekOrigin.Begin);
            byte[] buffer = new byte[512];
            int headerRead = this.sourceStream.Read(buffer, 0, buffer.Length);
            if (headerRead < 512)
            {
                return null;
            }

            return parseSparseExtentHeader(buffer, 0);
        }

        //parses a SparseExtentHeader from a raw byte buffer at the given offset
        public SparseExtentHeader parseSparseExtentHeader(byte[] buffer, int offset)
        {
            SparseExtentHeader header = new SparseExtentHeader();

            //magic number "KDMV" (little endian 0x564D444B)
            header.magicNumber = BitConverter.ToUInt32(buffer, offset + 0);

            //format version (1 or 3)
            header.version = BitConverter.ToUInt32(buffer, offset + 4);

            //flag bits
            header.flags = BitConverter.ToUInt32(buffer, offset + 8);

            //maximum data sectors covered by a single grain table entry
            header.grainSize = BitConverter.ToUInt64(buffer, offset + 12);

            //sector of the first grain (embedded descriptor follows)
            header.descriptorOffset = BitConverter.ToUInt64(buffer, offset + 20);

            //number of sectors the embedded descriptor occupies
            header.descriptorSize = BitConverter.ToUInt64(buffer, offset + 28);

            //number of sectors in the redundant grain table
            header.numGTEsPerGT = BitConverter.ToUInt32(buffer, offset + 36);

            //sector of the next redundant header (0 for non redundant)
            header.rgdOffset = BitConverter.ToUInt64(buffer, offset + 40);

            //sector of the grain directory
            header.gdOffset = BitConverter.ToUInt64(buffer, offset + 48);

            //sector of the grain table
            header.gtOffset = BitConverter.ToUInt64(buffer, offset + 56);

            //total number of sectors described by this extent
            header.capacity = BitConverter.ToUInt64(buffer, offset + 64);

            //total number of sectors actually stored in the extent file
            header.grainTableCapacity = BitConverter.ToUInt64(buffer, offset + 72);

            //flags for stream optimized extents (only version 3)
            header.flags2 = BitConverter.ToUInt32(buffer, offset + 80);

            header.isValid = (header.magicNumber == 0x564D444B);

            return header;
        }

        //parses the grain directory referenced by the given sparse extent header.
        //Returns the list of grain table sector addresses.
        public GrainDirectory parseGrainDirectory(SparseExtentHeader header)
        {
            GrainDirectory gd = new GrainDirectory();
            gd.entries = new List<UInt32>();

            if (this.sourceStream == null || header == null || !header.isValid || header.gdOffset == 0)
            {
                gd.isValid = false;
                return gd;
            }

            //the grain directory has one 4 byte entry per grain table
            UInt32 gdEntries = (UInt32)(header.capacity / (header.grainSize * header.numGTEsPerGT));
            if (header.capacity % (header.grainSize * header.numGTEsPerGT) != 0)
            {
                gdEntries++;
            }

            //jump to grain directory
            this.sourceStream.Seek((long)(header.gdOffset * 512), SeekOrigin.Begin);

            byte[] buffer = new byte[gdEntries * 4];
            int read = this.sourceStream.Read(buffer, 0, buffer.Length);
            gd.isValid = (read == buffer.Length);

            for (int i = 0; i < gdEntries; i++)
            {
                gd.entries.Add(BitConverter.ToUInt32(buffer, i * 4));
            }

            gd.entryCount = gdEntries;
            return gd;
        }

        //parses a single grain table located at the given sector address.
        //Each entry holds the sector offset of the corresponding grain (0 means
        //the grain is not allocated).
        public GrainTable parseGrainTable(UInt32 gtSector, UInt32 numGTEs)
        {
            GrainTable gt = new GrainTable();
            gt.entries = new List<UInt32>();

            if (this.sourceStream == null || gtSector == 0)
            {
                gt.isValid = false;
                return gt;
            }

            this.sourceStream.Seek((long)(gtSector * 512), SeekOrigin.Begin);

            byte[] buffer = new byte[numGTEs * 4];
            int read = this.sourceStream.Read(buffer, 0, buffer.Length);
            gt.isValid = (read == buffer.Length);

            for (int i = 0; i < numGTEs; i++)
            {
                gt.entries.Add(BitConverter.ToUInt32(buffer, i * 4));
            }

            gt.entryCount = numGTEs;
            return gt;
        }

        //returns the raw bytes of the grain located at the given sector offset.
        //grainSize is in sectors, the resulting buffer is grainSize * 512 bytes.
        public byte[] readGrain(UInt32 grainSector, UInt64 grainSize)
        {
            if (this.sourceStream == null || grainSector == 0)
            {
                return null;
            }

            this.sourceStream.Seek((long)(grainSector * 512), SeekOrigin.Begin);
            int length = (int)(grainSize * 512);
            byte[] buffer = new byte[length];
            int read = this.sourceStream.Read(buffer, 0, length);
            if (read != length)
            {
                Array.Resize(ref buffer, read);
            }

            return buffer;
        }

        //returns the raw bytes of the embedded descriptor (first 20 sectors)
        public byte[] getRawDescriptor()
        {
            if (this.sourceStream == null)
            {
                return null;
            }

            //check for embedded sparse extent
            this.sourceStream.Seek(0, SeekOrigin.Begin);
            byte[] magic = new byte[4];
            if (this.sourceStream.Read(magic, 0, 4) != 4 || magic[0] != 0x4B || magic[1] != 0x44 || magic[2] != 0x4D || magic[3] != 0x56)
            {
                //plain text descriptor file
                this.sourceStream.Seek(0, SeekOrigin.Begin);
                byte[] raw = new byte[this.sourceStream.Length];
                this.sourceStream.Read(raw, 0, raw.Length);
                return raw;
            }

            this.sourceStream.Seek(512, SeekOrigin.Begin);
            byte[] buffer = new byte[20 * 512];
            this.sourceStream.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        //returns the raw 512 byte sparse extent header sector
        public byte[] getRawHeader()
        {
            if (this.sourceStream == null)
            {
                return null;
            }

            this.sourceStream.Seek(0, SeekOrigin.Begin);
            byte[] buffer = new byte[512];
            this.sourceStream.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        //public static function to retrieve the create type from a given vmdk file
        public static CreateType getCreateTypeFromFile(string file)
        {
            vmdkParser parser = new vmdkParser(file);
            VmdkDescriptor descriptor = parser.parseDescriptorTable();
            parser.close();
            return descriptor.createType;
        }

        //public static function to retrieve the sparse extent header from a given vmdk file
        public static SparseExtentHeader getSparseExtentHeaderFromFile(string file)
        {
            vmdkParser parser = new vmdkParser(file);
            SparseExtentHeader header = parser.parseSparseExtentHeader();
            parser.close();
            return header;
        }

        //public static function to retrieve the parent file name hint from a given vmdk file
        public static string getParentFileNameHintFromFile(string file)
        {
            vmdkParser parser = new vmdkParser(file);
            VmdkDescriptor descriptor = parser.parseDescriptorTable();
            parser.close();
            return descriptor.parentFileNameHint;
        }

        //closes the sourceStream
        public void close()
        {
            if (this.sourceStream != null)
            {
                this.sourceStream.Close();
            }
        }

        //maps a createType string to the corresponding enum value
        private static CreateType parseCreateType(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return CreateType.Unknown;
            }

            switch (value.ToLowerInvariant())
            {
                case "monolithicsparse":
                    return CreateType.MonolithicSparse;
                case "monolithicflat":
                    return CreateType.MonolithicFlat;
                case "twogbmaxextentsparse":
                    return CreateType.TwoGbMaxExtentSparse;
                case "twogbmaxextentflat":
                    return CreateType.TwoGbMaxExtentFlat;
                case "streamoptimized":
                    return CreateType.StreamOptimized;
                case "vmfs":
                    return CreateType.Vmfs;
                case "vmfssparse":
                    return CreateType.VmfsSparse;
                case "sesparse":
                    return CreateType.SeSparse;
                default:
                    return CreateType.Unknown;
            }
        }

        //maps an access mode string to the corresponding enum value
        private static AccessMode parseAccessMode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return AccessMode.Unknown;
            }

            switch (value.ToUpperInvariant())
            {
                case "RW":
                    return AccessMode.RW;
                case "RDONLY":
                    return AccessMode.RDONLY;
                case "NOACCESS":
                    return AccessMode.NOACCESS;
                default:
                    return AccessMode.Unknown;
            }
        }

        public void Dispose()
        {
            this.close();
        }
    }

    //structured representation of the parsed VMDK text descriptor
    public struct VmdkDescriptor
    {
        public bool isValid;
        public int version;
        public string encoding;
        public string CID;
        public string parentCID;
        public CreateType createType;
        public string createTypeString;
        public string parentFileNameHint;
        public List<VmdkExtentDescription> extents;
        public Dictionary<string, string> extendedProperties;
    }

    //describes a single extent referenced in the descriptor
    public struct VmdkExtentDescription
    {
        public AccessMode access;
        public UInt64 numSectors;
        public string type;
        public string fileName;
        public UInt64 rangeOffset;
    }

    //binary sparse extent header according to the VMDK specification
    public struct SparseExtentHeader
    {
        public bool isValid;
        public UInt32 magicNumber;
        public UInt32 version;
        public UInt32 flags;
        public UInt64 grainSize;
        public UInt64 descriptorOffset;
        public UInt64 descriptorSize;
        public UInt32 numGTEsPerGT;
        public UInt64 rgdOffset;
        public UInt64 gdOffset;
        public UInt64 gtOffset;
        public UInt64 capacity;
        public UInt64 grainTableCapacity;
        public UInt32 flags2;
    }

    //grain directory holding sector addresses of all grain tables
    public struct GrainDirectory
    {
        public bool isValid;
        public UInt32 entryCount;
        public List<UInt32> entries;
    }

    //grain table holding sector addresses of all grains
    public struct GrainTable
    {
        public bool isValid;
        public UInt32 entryCount;
        public List<UInt32> entries;
    }

}
