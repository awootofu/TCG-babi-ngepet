#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TcgBattle.Data;

namespace TcgBattle.EditorTools
{
    /// <summary>
    /// One-shot scene bootstrap: creates the card database asset (with the stats
    /// transcribed from the artist's artwork) and a minimal battle scene containing
    /// a camera and the BattleStarter root. BattleStarter builds the whole UI at runtime.
    /// </summary>
    public static class BattleSceneBuilder
    {
        private const string DatabasePath = "Assets/Resources/CardDatabase.asset";
        private const string ScenePath = "Assets/Scenes/BattleScene.unity";

        [MenuItem("TCG Battle/Build Battle Scene")]
        public static void Build()
        {
            EnsureTmpEssentials();
            var db = EnsureDatabase();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.orthographic = true;
            cam.orthographicSize = 9.6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x2A, 0x23, 0x40, 0xFF);

            // Battle root
            var root = new GameObject("BattleRoot", typeof(BattleStarter));
            var starter = root.GetComponent<BattleStarter>();
            var so = new SerializedObject(starter);
            var prop = so.FindProperty("database");
            if (prop != null)
            {
                prop.objectReferenceValue = db;
                so.ApplyModifiedProperties();
            }

            // Audio listener so the scene has no missing-listener warnings.
            camGo.AddComponent<AudioListener>();

            EnsureSceneFolder();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            if (TMPro.TMP_Settings.instance != null)
            {
                var defaultFont = TMPTryGetDefaultFont();
                if (defaultFont == null)
                    Debug.LogWarning("[BattleSceneBuilder] TMP essentials not imported - the game will " +
                                     "use a runtime fallback font. For nicer text: Window > TextMeshPro > " +
                                     "Import TMP Essential Resources.");
            }

            Debug.Log($"[BattleSceneBuilder] Built '{ScenePath}' with database '{DatabasePath}'. Press Play to battle.");
        }

        private static TMPro.TMP_FontAsset TMPTryGetDefaultFont()
        {
            try
            {
                return TMPro.TMP_Settings.defaultFontAsset;
            }
            catch
            {
                return null;
            }
        }

        [MenuItem("TCG Battle/Refresh Card Database Asset")]
        public static void RefreshDatabase()
        {
            EnsureDatabase();
        }

        private static void EnsureTmpEssentials()
        {
            try
            {
                if (TMPro.TMP_Settings.instance != null)
                    return;
            }
            catch
            {
                // TMP_Settings null => essentials missing.
            }

            try
            {
                string packageFullPath = TMPro.EditorUtilities.TMP_EditorUtility.packageFullPath;
                string essentials = packageFullPath + "/Package Resources/TMP Essential Resources.unitypackage";
                if (System.IO.File.Exists(essentials))
                {
                    AssetDatabase.ImportPackage(essentials, false); // silent import
                    Debug.Log("[BattleSceneBuilder] Imported TMP Essential Resources (silent).");
                }
                else
                {
                    Debug.LogWarning("[BattleSceneBuilder] TMP essentials package not found at " + essentials);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleSceneBuilder] Could not import TMP essentials: " + e.Message);
            }
        }

        private static CardDatabase EnsureDatabase()
        {
            EnsureFolder("Assets", "Resources");
            var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CardDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[BattleSceneBuilder] Created CardDatabase asset with art-transcribed stats.");
            }
            return db;
        }

        private static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
