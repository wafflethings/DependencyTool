using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Newtonsoft.Json;

namespace DependencyTool.Processors;

public static class Replacer
{
    public static void ProcessFiles(AssetsManager manager, string bundlePath, string bundleName, string outputPath)
    {
        string cachePath = Path.Combine(outputPath, Generator.IdDictFileName);
        
        if (!File.Exists(cachePath))
        {
            Console.Error.WriteLine($"Could not find cache at {cachePath}");
            Environment.Exit(0);
            return;
        }
        
        Dictionary<string, AssetData>? data = JsonConvert.DeserializeObject<Dictionary<string, AssetData>?>(File.ReadAllText(cachePath));
        
        if (data == null)
        {
            Console.Error.WriteLine("Cache data could not be read");
            Environment.Exit(0);
            return;
        }

        BundleFileInstance bundleFile = manager.LoadBundleFile(Path.Combine(bundlePath, bundleName));

        Dictionary<string, AssetsFileInstance> pathToBundle = new();
        foreach (string path in Directory.GetFiles(bundlePath))
        {
            if (path == Path.Combine(bundlePath, bundleName)) //skip self
            {
                continue;
            }

            string fileName = path.Split("\\")[^1];
            string tempPath = Path.Combine(AppContext.BaseDirectory, "temp");
            string unpackedPath = Path.Combine(tempPath, fileName + "_unpacked");
            
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }
            
            Console.WriteLine("Loading bundle, this may take a while: " + fileName);
            
            BundleFileInstance extBundle = manager.LoadBundleFile(path, false);
            FileStream saveUnpacked = new FileStream(unpackedPath, FileMode.Create);
            extBundle.file = BundleHelper.UnpackBundleToStream(extBundle.file, saveUnpacked);
            AssetsFileInstance extFile = manager.LoadAssetsFileFromBundle(extBundle, 0);
            // bundles are named like [CAB-647db3267299754e47c24ba670730875],
            // bundle ext names are like [archive:/CAB-647db3267299754e47c24ba670730875/CAB-647db3267299754e47c24ba670730875], we need to convert it
            pathToBundle.Add($"archive:/{extFile.name}/{extFile.name}", extFile); 
        }

        List<AssetsFileInstance> assetsFiles = new();

        bool hasntErroredYet = true;
        int currentFileIndex = 0;
        do
        {
            try
            {
                assetsFiles.Add(manager.LoadAssetsFileFromBundle(bundleFile, currentFileIndex));
                Console.WriteLine($"Loaded inner asset file {currentFileIndex}");
            }
            catch
            {
                hasntErroredYet = false; //trycatch logic is HORRID but i havent found a better way to do this
            }
            currentFileIndex++;
        } 
        while (hasntErroredYet);
        
        int assetFileIndex = 0;
        foreach (AssetsFileInstance assetsFile in assetsFiles)
        {
            Console.WriteLine($"Processing file {assetsFile.name}");
            foreach (AssetFileInfo asset in assetsFile.file.AssetInfos)
            {
                if ((AssetClassID)asset.TypeId is AssetClassID.GameObject or AssetClassID.Transform) continue;
                
                Console.WriteLine($"Attempting to fix a {(AssetClassID)asset.TypeId}");
                try
                {
                    FixPptrs(manager, pathToBundle, assetsFile, manager.GetBaseField(assetsFile, asset), data);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
            }

            bundleFile.file.BlockAndDirInfo.DirectoryInfos[assetFileIndex++].SetNewData(assetsFile.file);
        }

        using AssetsFileWriter writer = new(Path.Combine(AppContext.BaseDirectory, "output", bundleName + "_fixed"));
        bundleFile.file.Write(writer);
    }

    private static void FixPptrs(AssetsManager manager, Dictionary<string, AssetsFileInstance> fileNameToFile, AssetsFileInstance file, AssetTypeValueField field, Dictionary<string, AssetData> nameToData)
    {
        if (field.TypeName.Contains("PPtr"))
        {
            AssetPPtr pptr = new AssetPPtr(field["m_FileID"].AsInt, field["m_PathID"].AsLong);

            if (pptr.FileId == 0 && pptr.PathId == 0)
            {
                return; //null ptr
            }
            
            AssetFileInfo? asset = GetByPptr(fileNameToFile, file, pptr, out AssetsFileInstance assetFile);

            if (asset == null)
            {
                Console.Error.WriteLine($"Could not find file of {pptr.PathId} in file {pptr.FileId}");
                return;
            }
            
            if (!Program.CacheableAssets.Contains((AssetClassID)asset.TypeId))
            {
                return;
            }
            
            Console.WriteLine($"File {assetFile.name} asset {asset.PathId}");
            
            string assetName = manager.GetBaseField(assetFile, asset)["m_Name"].AsString;

            if (!nameToData.ContainsKey(assetName))
            {
                Console.WriteLine($"Cache doesn't contain {assetName}");
                return;
            }
            
            AssetData data = nameToData[assetName];
            Console.WriteLine($"Was at {field["m_FileID"].AsInt}::{field["m_PathID"].AsLong}, moved to {GetExternalFileId(data.FileName, file)}::{data.PathId}");
            field["m_FileID"].AsInt = GetExternalFileId(data.FileName, file);
            field["m_PathID"].AsLong = data.PathId;
            asset.SetNewData(field);
            
            return;
        }

        foreach (AssetTypeValueField child in field.Children)
        {
            FixPptrs(manager, fileNameToFile, file, child, nameToData);
        }
    }

    private static AssetFileInfo? GetByPptr(Dictionary<string, AssetsFileInstance> fileNameToFile, AssetsFileInstance file, AssetPPtr pptr, out AssetsFileInstance assetFile)
    {
        // FileID 0 refers to the parent bundle - fileid 1 is external[0]
        if (pptr.FileId == 0)
        {
            assetFile = file;
        }
        else
        {
            string key = file.file.Metadata.Externals[pptr.FileId - 1].PathName;
            if (!fileNameToFile.ContainsKey(key))
            {
                // Console.WriteLine($"Couldn't find {key} in externals");
                assetFile = null;
                return null;
            }
            assetFile = fileNameToFile[key];
        }

        AssetFileInfo? asset = assetFile.file.AssetInfos.FirstOrDefault(info => info.PathId == pptr.PathId);

        if (asset == null)
        {
            Console.Error.WriteLine($"Could not find asset at {assetFile.name}::{pptr.PathId}");
        }
        
        return asset;
    }

    private static int GetExternalFileId(string name, AssetsFileInstance file)
    {
        int index = file.file.Metadata.Externals.FindIndex(external => external.PathName == name);
        
        if (index == -1)
        {
            file.file.Metadata.Externals.Add(new AssetsFileExternal()
            {
                OriginalPathName = name,
                PathName = name,
                VirtualAssetPathName = string.Empty
            });
            Console.WriteLine($"Added external {name} to {file.name}");
            return file.file.Metadata.Externals.Count; // -1 to get index, +1 because 0 is self and not a external
        }

        return index + 1; // +1 as 0 is self
    }
}