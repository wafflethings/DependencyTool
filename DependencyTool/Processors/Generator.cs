using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Newtonsoft.Json;

namespace DependencyTool.Processors;

public static class Generator
{
    public const string IdDictFileName = "name_path_id_map.json";
    
    public static void ProcessFiles(AssetsManager manager, string assetPath)
    {
        manager.LoadClassPackage(Path.Combine(AppContext.BaseDirectory, "uncompressed.tpk"));
        Dictionary<string, AssetData> assets = new();
        
        foreach (string path in Directory.GetFiles(assetPath))
        {
            if (!path.EndsWith(".assets"))
            {
                continue;
            }
            
            Console.WriteLine($"Loading asset file: {path.Split("\\")[^1]}");
            AssetsFileInstance file = manager.LoadAssetsFile(path);
            
            manager.LoadClassDatabaseFromPackage(file.file.Metadata.UnityVersion);
            
            foreach (AssetFileInfo asset in file.file.AssetInfos)
            {
                if (!Program.CacheableAssets.Contains((AssetClassID)asset.TypeId))
                {
                    continue;
                }
                
                Console.WriteLine($"File {file.name} -> {manager.GetBaseField(file, asset)["m_Name"].AsString} {asset.PathId}");
                string name = manager.GetBaseField(file, asset)["m_Name"].AsString;

                if (name == string.Empty)
                {
                    Console.Error.WriteLine($"{(AssetClassID)asset.TypeId} asset has no name - not adding to cache");   
                    continue;
                }
                
                if (assets.ContainsKey(name))
                {
                    Console.Error.WriteLine($"Already contains key {name}!");
                    continue;
                }
                
                assets.Add(name, new AssetData(name, (AssetClassID)asset.GetTypeId(file.file), file.name, asset.PathId));
            }
        }
        
        string outputDirectory = Path.Combine(AppContext.BaseDirectory, "output");
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        File.WriteAllText(Path.Combine(outputDirectory, IdDictFileName), JsonConvert.SerializeObject(assets));
    }
}