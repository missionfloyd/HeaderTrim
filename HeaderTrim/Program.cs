using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeaderTrim
{
    class Program
    {
        static readonly byte[] nesSig = { 0x4e, 0x45, 0x53, 0x1A };// "NES" + 0x1A
        static readonly byte[] fdsSig = { 0x46, 0x44, 0x53, 0x1A };// "FDS" + 0x1A
        static readonly byte[] a78Sig = { 0x41, 0x54, 0x41, 0x52, 0x49, 0x37, 0x38, 0x30, 0x30 };// "ATARI7800"
        static readonly byte[] lnxSig = { 0x4c, 0x59, 0x4E, 0x58 };// "LYNX"

        static readonly string[] exts = { ".zip", ".nes", ".fds", ".fcn", ".a78", ".lnx", ".smc", ".j64" };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: HeaderTrim <file | folder> ..." + Environment.NewLine);
                Console.WriteLine("Supported filetypes: " + string.Join(", ", exts));
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (Directory.Exists(args[i]))
                {
                    var dirinfo = new DirectoryInfo(args[i]);
                    var dirfiles = dirinfo.GetFiles("*", SearchOption.AllDirectories).Where(file => exts.Contains(file.Extension, StringComparer.OrdinalIgnoreCase)).ToArray();

                    for (int file = 0; file < dirfiles.Length; file++)
                    {
                        OpenFile(dirfiles[file]);
                    }
                }
                else if (File.Exists(args[i]) && exts.Contains(Path.GetExtension(args[i]), StringComparer.OrdinalIgnoreCase))
                {
                    OpenFile(new FileInfo(args[i]));
                }
            }
        }

        static void OpenFile(FileInfo fileinfo)
        {
            if (fileinfo.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using (var archive = ZipFile.Open(fileinfo.FullName, ZipArchiveMode.Update))
                {
                    var entries = archive.Entries.Where(file => exts.Skip(1).Contains(Path.GetExtension(file.Name), StringComparer.OrdinalIgnoreCase)).ToArray();
                    if (entries.Length > 0)
                    {
                        for (int i = 0; i < entries.Length; i++)
                        {
                            var dateModified = entries[i].LastWriteTime;
                            using (var stream = entries[i].Open())
                            {
                                RemoveHeader(stream, entries[i].Name);
                            }
                            entries[i].LastWriteTime = dateModified;
                        }
                    }
                }
            }
            else
            {
                var dateModified = fileinfo.LastWriteTime;
                using (var fs = File.Open(fileinfo.FullName, FileMode.Open, FileAccess.ReadWrite))
                {
                    RemoveHeader(fs, fileinfo.Name);
                }
                fileinfo.LastWriteTime = dateModified;
            }
        }

        static void RemoveHeader(Stream fs, string filename)
        {
            Console.WriteLine(filename);
            var ext = Path.GetExtension(filename);
            int headerLength;
            var header = new byte[16];
            fs.Read(header, 0, 16);

            if (
                (ext.Equals(".nes", StringComparison.OrdinalIgnoreCase) && fs.Length % 1024 == 16 && header.Take(4).SequenceEqual(nesSig)) ||
                (ext.Equals(".fcn", StringComparison.OrdinalIgnoreCase) && fs.Length % 1024 == 16 && header.Take(4).SequenceEqual(nesSig)) ||
                (ext.Equals(".fds", StringComparison.OrdinalIgnoreCase) && fs.Length % 65500 == 16 && header.Take(4).SequenceEqual(fdsSig))
            )
            {
                headerLength = 16;
            }
            else if (ext.Equals(".lnx", StringComparison.OrdinalIgnoreCase) && fs.Length % 1024 == 64 && header.Take(4).SequenceEqual(lnxSig)) 
            {
                headerLength = 64;
            }
            else if (ext.Equals(".a78", StringComparison.OrdinalIgnoreCase) && fs.Length % 1024 == 128 && header.Skip(1).Take(9).SequenceEqual(a78Sig))
            {
                headerLength = 128;
            }
            else if (ext.Equals(".smc", StringComparison.OrdinalIgnoreCase) && fs.Length % 1024 == 512)
            {
                headerLength = 512;
            }
            else if (ext.Equals(".j64", StringComparison.OrdinalIgnoreCase) && fs.Length % 1048576 == 0)
            {
                headerLength = 8192;
            }
            else
            { 
                Console.WriteLine("No header found" + Environment.NewLine);
                return;
            }

            var file = new byte[fs.Length - headerLength];
            fs.Position = headerLength;
            fs.Read(file, 0, (int)fs.Length - headerLength);
            fs.Position = 0;
            fs.Write(file, 0, (int)fs.Length - headerLength);
            fs.SetLength(fs.Length - headerLength);

            Console.WriteLine("Removed header" + Environment.NewLine);
        }
    }
}