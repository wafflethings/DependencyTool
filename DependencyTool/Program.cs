using AssetsTools.NET;
using AssetsTools.NET.Extra;
using DependencyTool.Processors;
using Newtonsoft.Json;

namespace DependencyTool;

public class Program
{
    public static readonly AssetClassID[] CacheableAssets =
    [
        AssetClassID.AnimationClip,
        AssetClassID.AnimatorController,
        AssetClassID.Avatar,
        AssetClassID.ComputeShader,
        AssetClassID.Cubemap,
        //AssetClassID.Font,
        AssetClassID.LightProbes,
        AssetClassID.Material,
        AssetClassID.Mesh,
        AssetClassID.MonoBehaviour,
        AssetClassID.NavMeshData,
        AssetClassID.OcclusionCullingData,
        AssetClassID.PhysicMaterial,
        AssetClassID.PrefabInstance,
        AssetClassID.Shader,
        AssetClassID.Sprite,
        AssetClassID.TextAsset,
        AssetClassID.Texture2D,
        AssetClassID.Texture3D
    ];

    private static void Main(string[] args)
    {
        if (args.Length < 2 || !(args[0] is "output" or "replace") || (args[0] == "replace" && args.Length < 4))
        {
            Console.Error.WriteLine("Incorrect usage:\n    DependencyTool output (path to game_Data)\n    DependencyTool replace (bundle path) (name of bundle to fix) (output folder)");
            Environment.Exit(0);
            return;
        }
        
        AssetsManager manager = new();

        switch (args[0])
        {
            case "output":
                Generator.ProcessFiles(manager, args[1]);
                break;
            case "replace":
                Replacer.ProcessFiles(manager, args[1], args[2], args[3]);
                break;
        }
    }
}