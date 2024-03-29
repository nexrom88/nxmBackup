﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Net.Http;
using System.Net;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class FolderDownloader
    {
        private string folder;

        public FolderDownloader(string folder)
        {
            this.folder = folder;
        }

        public async void WriteToStream(Stream outputStream, HttpContent content, TransportContext context)
        {
            WriteOnlyStreamWrapper streamWrapper = new WriteOnlyStreamWrapper(outputStream);

            //open zip archive stream and bind it to output stream
            System.IO.Compression.ZipArchive zipStream = new System.IO.Compression.ZipArchive(streamWrapper, System.IO.Compression.ZipArchiveMode.Create);

            //get all files from directory
            string[] files = System.IO.Directory.GetFiles(this.folder, "*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                System.IO.FileStream fileStream;
                try
                {
                    //open file stream
                    fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
                }
                catch (Exception ex)
                {
                    //file could not be opened, ignore it
                    continue;
                }


                //remove base path from string
                string relFile = file.Substring(this.folder.Length + 1);

                //create zip file entry
                System.IO.Compression.ZipArchiveEntry zipEntry = zipStream.CreateEntry(relFile);
                Stream entryStream = zipEntry.Open();

                try
                {

                    //write to archive
                    int buffersize = 1000000;
                    byte[] buffer = new byte[buffersize];
                    int bytesRead = fileStream.Read(buffer, 0, buffersize);
                    while (bytesRead == buffersize)
                    {
                        await entryStream.WriteAsync(buffer, 0, bytesRead);
                        bytesRead = fileStream.Read(buffer, 0, buffersize);
                    }
                    await entryStream.WriteAsync(buffer, 0, bytesRead);
                }catch (Exception ex)
                {

                }


                //close filestream and entry stream
                fileStream.Close();
                fileStream.Dispose();
                entryStream.Close();
                entryStream.Dispose();

            }

            //close zip archive
            zipStream.Dispose();

            streamWrapper.Close();
            outputStream.Close();

            //try
            //{
            //    var buffer = new byte[65536];

            //    using (var video = File.Open(_filename, FileMode.Open, FileAccess.Read))
            //    {
            //        var length = (int)video.Length;
            //        var bytesRead = 1;

            //        while (length > 0 && bytesRead > 0)
            //        {
            //            bytesRead = video.Read(buffer, 0, Math.Min(length, buffer.Length));
            //            await outputStream.WriteAsync(buffer, 0, bytesRead);
            //            length -= bytesRead;
            //        }
            //    }
            //}
            //catch (HttpException ex)
            //{
            //    return;
            //}
            //finally
            //{
            //    outputStream.Close();
            //}

        }
    }
}