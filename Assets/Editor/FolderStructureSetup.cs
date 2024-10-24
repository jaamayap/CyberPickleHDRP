using UnityEditor;
using System.IO;

public class FolderStructureSetup
{
    [MenuItem("Tools/Setup CyberPickle Folders")]
    private static void CreateFolderStructure()
    {
        CreateDirectories("Assets", new string[]
        {
            "_CyberPickle",
            "_CyberPickle/Art",
            "_CyberPickle/Audio",
            "_CyberPickle/Code",
            "_CyberPickle/Data",
            "_CyberPickle/Prefabs",
            "_CyberPickle/Scenes",
            "ThirdParty"
        });

        // Create detailed subfolders
        CreateDirectories("Assets/_CyberPickle", new string[]
        {
            "Art/Materials",
            "Art/Models",
            "Art/Animations",
            "Art/UI",
            "Art/VFX",
            "Audio/Music",
            "Audio/SFX",
            "Code/Core",
            "Code/Core/Managers",
            "Code/Core/Systems",
            "Code/Gameplay",
            "Code/Gameplay/Player",
            "Code/Gameplay/Enemies",
            "Code/Gameplay/Weapons",
            "Code/UI",
            "Data/ScriptableObjects",
            "Data/ScriptableObjects/Configs",
            "Data/ScriptableObjects/Events",
            "Prefabs/Characters",
            "Prefabs/Environment",
            "Prefabs/UI",
            "Scenes/Boot",
            "Scenes/Menu",
            "Scenes/Game"
        });

        AssetDatabase.Refresh();
    }

    private static void CreateDirectories(string root, string[] dirs)
    {
        foreach (string dir in dirs)
        {
            string fullPath = Path.Combine(root, dir);
            Directory.CreateDirectory(fullPath);
        }
    }
}
