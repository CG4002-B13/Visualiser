#if UNITY_IOS
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;


public class iOSBuildPostProcessor
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget != BuildTarget.iOS)
            return;


        Debug.Log("iOS Post Process: Starting...");


        // Get the project path
        string projectPath = pathToBuiltProject + "/Unity-iPhone.xcodeproj/project.pbxproj";


        // Load the Xcode project
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);


        // Get BOTH target GUIDs
        string mainTargetGuid = project.GetUnityMainTargetGuid();
        string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();


        // Add Photos framework to MAIN target
        project.AddFrameworkToProject(mainTargetGuid, "Photos.framework", false);
        project.AddFrameworkToProject(mainTargetGuid, "PhotosUI.framework", false);


        Debug.Log("iOS Post Process: Added Photos frameworks to Unity-iPhone target");


        // Add Photos framework to FRAMEWORK target (this is the important one!)
        project.AddFrameworkToProject(frameworkTargetGuid, "Photos.framework", false);
        project.AddFrameworkToProject(frameworkTargetGuid, "PhotosUI.framework", false);


        Debug.Log("iOS Post Process: Added Photos frameworks to UnityFramework target");


        // Write the modified project back
        project.WriteToFile(projectPath);


        // ===== Add Info.plist permissions =====
        // Get the Info.plist file path
        string infoPlistPath = pathToBuiltProject + "/Info.plist";


        // Load the plist file
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(infoPlistPath);


        // Get root dict
        PlistElementDict rootDict = plist.root;


        // Add Photo Library Add Usage Description (for saving to Photos)
        if (!rootDict.values.ContainsKey("NSPhotoLibraryAddUsageDescription"))
        {
            rootDict.SetString("NSPhotoLibraryAddUsageDescription",
                "This app needs access to your photos to save screenshots to your Photos library.");
            Debug.Log("iOS Post Process: Added NSPhotoLibraryAddUsageDescription");
        }


        // Add Photo Library Usage Description (for reading from Photos - optional but good practice)
        if (!rootDict.values.ContainsKey("NSPhotoLibraryUsageDescription"))
        {
            rootDict.SetString("NSPhotoLibraryUsageDescription",
                "This app needs access to your photos to view and manage your screenshots.");
            Debug.Log("iOS Post Process: Added NSPhotoLibraryUsageDescription");
        }


        // Save the modified plist
        plist.WriteToFile(infoPlistPath);


        Debug.Log("iOS Post Process: Info.plist permissions added successfully!");
        Debug.Log("iOS Post Process: Completed successfully!");
    }
}
#endif