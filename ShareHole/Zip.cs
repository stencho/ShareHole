using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using System;
using System.IO;

namespace ShareHole;

public static class Zip {
    static ConcurrentCache<byte[]> folder_zip_cache =  new ConcurrentCache<byte[]>(60 * 60 * 48);

    public static byte[] ZipFolderToCache(string folder_path) {
        if (folder_zip_cache.Test(folder_path)) 
            return folder_zip_cache.Request(folder_path);
        
        folder_zip_cache.Store(folder_path, ZipFolderToBytes(folder_path));
        
        GC.Collect();
        
        folder_zip_cache.CropToNewest(5);

        long total_length = 0;
        
        foreach (var key in folder_zip_cache.Cache.Keys) {
            total_length += folder_zip_cache.Cache[key].item.Length;
        }
        
        Logging.Message($"Folder Zip Cache currently contains {folder_zip_cache.Count} items, totalling ~{total_length /1024/1024} MB");
        
        return folder_zip_cache.Request(folder_path);
    }

    public static byte[] CacheRequest(string folder_path) {
        return folder_zip_cache.Request(folder_path);
    }
    
    private static byte[] ZipFolderToBytes(string folder_path)
    {
        if (!Directory.Exists(folder_path))
            throw new DirectoryNotFoundException(folder_path);

        var files = Directory.EnumerateFiles(folder_path, "*", SearchOption.AllDirectories).ToList();

        // compress each of the files in memory
        var entries = files.AsParallel().Select(file =>
        {
            using (var file_stream = new MemoryStream()) {
                using (var zip = new ZipOutputStream(file_stream)) {
                    zip.SetLevel(9);
                    var entry = new ZipEntry(Path.GetFileName(file)) {
                        DateTime = File.GetLastWriteTimeUtc(file)
                    };
                    zip.PutNextEntry(entry);
                    using var fs = File.OpenRead(file);
                    fs.CopyTo(zip);
                    zip.CloseEntry();
                    zip.Finish();
                }

                return new { file, data = file_stream.ToArray() };
            }
        }).ToList();

        // combine all the files into one big zip
        using var memory_stream = new MemoryStream();
        using (var zip = new ZipOutputStream(memory_stream))
        {
            //files are already compressed
            zip.SetLevel(0);
            
            foreach (var e in entries)
            {
                var rel = Path.GetRelativePath(folder_path, e.file);
                var entry = new ZipEntry(rel)
                {
                    DateTime = File.GetLastWriteTimeUtc(e.file)
                };
                
                zip.PutNextEntry(entry);
                zip.Write(e.data, 0, e.data.Length);
                zip.CloseEntry();
            }
            zip.Finish();
        }

        return memory_stream.ToArray();
    }
}