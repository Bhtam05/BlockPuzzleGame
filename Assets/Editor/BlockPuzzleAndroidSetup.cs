using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Menu Tools > Block Puzzle > Configure Android Build: đặt sẵn các thiết lập Player Settings cần cho
    /// game dọc trên Android, thêm scene đang mở vào Build Settings và báo lại những gì còn phải tự làm.
    /// Không tự đổi Package Name (đã đặt rồi thì khó đổi lại sau khi đưa lên Google Play).
    /// </summary>
    public static class BlockPuzzleAndroidSetup
    {
        [MenuItem("Tools/Block Puzzle/Configure Android Build")]
        public static void Configure()
        {
            var done = new List<string>();
            var todo = new List<string>();

            // 1) Khóa màn hình dọc
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            done.Add("Orientation = Portrait (khóa dọc)");

            // 2) IL2CPP + ARM64 (Google Play yêu cầu 64-bit)
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            done.Add("Scripting Backend = IL2CPP, kiến trúc ARM64");

            // 3) Thêm scene đang mở vào danh sách build
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                todo.Add("Scene chưa được lưu: nhấn Ctrl+S rồi chạy lại menu này để thêm vào Build.");
            }
            else
            {
                var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                if (!scenes.Exists(s => s.path == scene.path))
                {
                    scenes.Add(new EditorBuildSettingsScene(scene.path, true));
                    EditorBuildSettings.scenes = scenes.ToArray();
                    done.Add("Đã thêm scene " + scene.name + " vào Build");
                }
                else
                {
                    done.Add("Scene " + scene.name + " đã có trong Build");
                }
            }

            // 4) Những việc không tự làm được hoặc không nên tự làm
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                todo.Add("Chưa cài Android Build Support: Unity Hub > Installs > Add modules > Android Build Support (gồm OpenJDK, SDK, NDK).");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                todo.Add("Đang ở nền tảng " + EditorUserBuildSettings.activeBuildTarget +
                         ": File > Build Profiles, chọn Android rồi bấm Switch Platform.");

            string id = PlayerSettings.GetApplicationIdentifier(android);
            if (string.IsNullOrEmpty(id) || id.StartsWith("com.DefaultCompany"))
                todo.Add("Package Name đang là mặc định (" + id + "): đặt tên riêng ở Player Settings > Other Settings > " +
                         "Identification trước khi đưa lên Google Play.");

            AssetDatabase.SaveAssets();

            string msg = "Đã thiết lập:\n- " + string.Join("\n- ", done);
            if (todo.Count > 0) msg += "\n\nCòn phải làm tay:\n- " + string.Join("\n- ", todo);
            Debug.Log("[BlockPuzzle] " + msg);
            EditorUtility.DisplayDialog("Block Puzzle - Android", msg, "OK");
        }
    }
}
