using AssetsTools.NET.Extra;

namespace DependencyTool;

public class AssetData
{
    public string Name { get; private set; }
    public AssetClassID Type { get; private set; }
    public string FileName { get; private set; }
    public long PathId { get; private set; }

    public AssetData(string name, AssetClassID type, string fileName, long pathId)
    {
        Name = name;
        Type = type;
        FileName = fileName;
        PathId = pathId;
    }
}