using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Common
{
    public class vhdxParser : IDisposable
    {
        private FileStream sourceStream;

        public vhdxParser(string file)
        {
            try
            {
                this.sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            }
            catch (Exception ex)
            {

            }
        }

        //closes the sourceStream
        public void close()
        {
            this.sourceStream.Close();
        }

        //reads blockSize from MetadataTable
        public UInt32 getBlockSize(MetadataTable metadataTable)
        {
            UInt32 offset = 0;
            UInt32 length = 0;
            foreach (MetadataTableEntry entry in metadataTable.entries)
            {
                if (entry.itemID[0] == 0x37)
                {
                    offset = entry.offset;
                    length = entry.length;
                }
            }

            //jump to destination
            this.sourceStream.Seek(offset, SeekOrigin.Begin);

            //read block size
            byte[] buffer = new byte[8];
            this.sourceStream.Read(buffer, 0, 8);

            return BitConverter.ToUInt32(buffer, 0);
        }

        //reads logicalSectorSize from MetadataTable
        public UInt32 getLogicalSectorSize(MetadataTable metadataTable)
        {
            UInt32 offset = 0;
            UInt32 length = 0;
            foreach (MetadataTableEntry entry in metadataTable.entries)
            {
                if (entry.itemID[0] == 0x1D)
                {
                    offset = entry.offset;
                    length = entry.length;
                }
            }

            //jump to destination
            this.sourceStream.Seek(offset, SeekOrigin.Begin);

            //read logicalSectorSize size
            byte[] buffer = new byte[8];
            this.sourceStream.Read(buffer, 0, 8);

            return BitConverter.ToUInt32(buffer, 0);
        }

        //parses the metadata region
        public MetadataTable parseMetadataTable(RegionTable table)
        {
            MetadataTable metadataTable = new MetadataTable();
            metadataTable.entries = new List<MetadataTableEntry>();
            UInt64 metadataTableOffset = 0;
            UInt32 metadataTableLength = 0;

            //read offset and length for BAT table
            foreach (RegionTableEntry entry in table.entries)
            {
                if (entry.guid[0] == 0x06)
                {
                    metadataTableOffset = entry.fileOffset;
                    metadataTableLength = entry.length;
                    break;
                }
            }

            //jump to first BAT entry
            this.sourceStream.Seek((long)metadataTableOffset, SeekOrigin.Begin);
            byte[] buffer = new byte[metadataTableLength];
            this.sourceStream.Read(buffer, 0, buffer.Length);

            MetadataTableHeader metadataTableHeader = new MetadataTableHeader();

            //read signature
            metadataTableHeader.signature = Encoding.UTF8.GetString(buffer, 0, 8);

            //read reserved
            metadataTableHeader.reserved = BitConverter.ToUInt16(buffer, 8);

            //read entry count
            metadataTableHeader.entryCount = BitConverter.ToUInt16(buffer, 10);

            UInt32 entryOffset = 32;
            //iterate through all entries
            for (int i = 0; i < metadataTableHeader.entryCount; i++)
            {
                MetadataTableEntry entry = new MetadataTableEntry();

                //read itemID
                entry.itemID = new byte[16];
                Array.Copy(buffer, entryOffset, entry.itemID, 0, 16);

                //read offset
                entry.offset = BitConverter.ToUInt32(buffer, (int)entryOffset + 16) + (uint)metadataTableOffset; //add metadataTableOffset to get offset from beginning of file

                //read length
                entry.length = BitConverter.ToUInt32(buffer, (int)entryOffset + 20);

                metadataTable.entries.Add(entry);

                entryOffset += 32;
            }

            return metadataTable;
        }


        //parses the BAT table (chunkSize just necessary when removeSectorMask is set)
        public BATTable parseBATTable(RegionTable table, UInt32 chunkSize, bool removeSectorMask)
        {
            BATTable batTable = new BATTable();
            batTable.entries = new List<BATEntry>();
            UInt64 batOffset = 0;
            UInt32 batLength = 0;

            //read offset and length for BAT table
            foreach (RegionTableEntry entry in table.entries)
            {
                if (entry.guid[0] == 0x66)
                {
                    batOffset = entry.fileOffset;
                    batLength = entry.length;
                    break;
                }
            }

            //jump to first BAT entry
            this.sourceStream.Seek((long)batOffset, SeekOrigin.Begin);

            //read whole table
            byte[] buffer = new byte[batLength];
            this.sourceStream.Read(buffer, 0, (int)batLength);

            //each entry consists of 64bit, iterate
            UInt32 entryCount = batLength / 64;
            for (int i = 0; i < entryCount; i++)
            {
                //jump over sector mask?
                if (removeSectorMask && i > 0 && i % chunkSize == 0)
                {
                    continue;
                }

                BATEntry newEntry = new BATEntry();
                UInt64 batEntry = BitConverter.ToUInt64(buffer, 64 * i);
                newEntry.state = (byte)(batEntry % 8);
                batEntry = batEntry >> 3;
                UInt32 reserved = (UInt32)(batEntry % Math.Pow(2, 17));
                batEntry = batEntry >> 17;
                UInt64 fileOffsetMB = batEntry;

                newEntry.FileOffsetMB = fileOffsetMB;
                newEntry.reserved = reserved;

                batTable.entries.Add(newEntry);
            }

            return batTable;
        }

        //parses the region table
        public RegionTable parseRegionTable()
        {
            //file opened?
            if (this.sourceStream == null)
            {
                RegionTable dummyRegionTable = new RegionTable();
                dummyRegionTable.isValid = false;
                return dummyRegionTable;
            }

            //set pointer to RegionTable 1
            this.sourceStream.Seek(192 * 1024, SeekOrigin.Begin);

            //reserve buffer
            int regionSize = 256 * 1024 - 128 * 1024;
            byte[] buffer = new byte[regionSize];
            this.sourceStream.Read(buffer, 0, regionSize);

            RegionTable regionTable = new RegionTable();

            //===== parse header =====

            RegionTableHeader regionTableHeader = new RegionTableHeader();

            //read signature
            regionTableHeader.signature = Encoding.UTF8.GetString(buffer, 0, 4);

            //read checksum
            regionTableHeader.checksum = BitConverter.ToUInt32(buffer, 4);

            //read entry count
            regionTableHeader.entryCount = BitConverter.ToUInt32(buffer, 8);

            //read reserved
            regionTableHeader.reserved = BitConverter.ToUInt32(buffer, 12);

            regionTable.header = regionTableHeader;

            //===== parse entries =====

            RegionTableEntry[] entries = new RegionTableEntry[regionTableHeader.entryCount];
            Int32 entryByteOffset = 16;
            for (int i = 0; i < regionTableHeader.entryCount; i++)
            {
                RegionTableEntry entry = new RegionTableEntry();
                entry.guid = new byte[16];

                //copy guid
                Array.Copy(buffer, entryByteOffset, entry.guid, 0, 16);

                //read file offset
                entry.fileOffset = BitConverter.ToUInt64(buffer, entryByteOffset + 16);

                //read length
                entry.length = BitConverter.ToUInt32(buffer, entryByteOffset + 16 + 8);

                //read required
                entry.required = BitConverter.ToUInt32(buffer, entryByteOffset + 16 + 8 + 8);

                entries[i] = entry;
                entryByteOffset += 16 + 8 + 8;
            }

            regionTable.entries = entries;
            return regionTable;
        }

        public void Dispose()
        {
            this.close();
        }
    }
    public struct RegionTable
    {
        public RegionTableHeader header;
        public RegionTableEntry[] entries;
        public bool isValid;
    }

    public struct RegionTableHeader
    {
        public string signature;
        public UInt32 checksum;
        public UInt32 entryCount;
        public UInt32 reserved;
    }

    public struct RegionTableEntry
    {
        public byte[] guid;
        public UInt64 fileOffset;
        public UInt32 length;
        public UInt32 required;
    }

    public struct BATTable
    {
        public List<BATEntry> entries;
    }

    public struct BATEntry
    {
        public byte state;
        public UInt32 reserved;
        public UInt64 FileOffsetMB;
        public UInt32 payload;
    }

    public struct MetadataTable
    {
        public MetadataTableHeader header;
        public List<MetadataTableEntry> entries;
    }

    public struct MetadataTableHeader
    {
        public string signature;
        public UInt16 reserved;
        public UInt16 entryCount;
        public byte[] reserved2;
    }

    public struct MetadataTableEntry
    {
        public byte[] itemID;
        public UInt32 offset;
        public UInt32 length;
    }

    public struct FileParametersTable
    {
        public UInt32 blockSize;
        public UInt32 reserved;
    }

}
