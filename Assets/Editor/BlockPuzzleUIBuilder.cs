using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Tool dựng toàn bộ giao diện chỉ với 1 lần bấm: Tools > Block Puzzle > Build UI (All).
    /// Làm 2 việc:
    ///  1) Cấu hình import cho sprite trong Assets/Art/Generated theo manifest.json (PPU, viền 9-slice).
    ///  2) Dựng scene: camera, nền gỗ, khung bảng, prefab ô gạch, vị trí khay, HUD, popup Game Over/Pause
    ///     rồi gán sẵn mọi tham chiếu cho GridManager, BlockSpawner, ScoreManager, GameManager.
    /// Chạy lại nhiều lần vẫn an toàn: HUDCanvas cũ do tool tạo sẽ được thay mới, các Canvas khác không bị xóa.
    /// </summary>
    public static class BlockPuzzleUIBuilder
    {
        // ------------------------------------------------------------------ Đường dẫn
        private const string ArtDir = "Assets/Art/Generated";
        private const string MatDir = ArtDir + "/Materials";
        private const string PrefabDir = "Assets/Prefabs";
        private const string CanvasName = "HUDCanvas";

        // ------------------------------------------------------------------ Chữ hiển thị
        // LiberationSans mặc định của TextMeshPro không có đủ dấu tiếng Việt, nên để tiếng Anh.
        // Muốn tiếng Việt: tạo Font Asset (Dynamic) từ font có hỗ trợ dấu rồi đổi các chuỗi này.
        private const string TxtBest = "BEST";
        private const string TxtScore = "SCORE";
        private const string TxtGameOver = "GAME OVER";
        private const string TxtYourScore = "YOUR SCORE";
        private const string TxtBestScore = "BEST SCORE";
        private const string TxtPlayAgain = "PLAY AGAIN";
        private const string TxtPaused = "PAUSED";
        private const string TxtResume = "RESUME";
        private const string TxtRestart = "RESTART";

        // ------------------------------------------------------------------ Bố cục World (đơn vị Unity)
        // Camera luôn thấy tối thiểu 17 đơn vị chiều cao nên các số này đúng với mọi màn hình dọc.
        private const float GridCenterY = 0f;   // tâm bảng theo trục Y
        private const float SlotY = -6.4f;      // hàng khay đặt 3 khối
        private const float SlotSpacing = 3f;   // khoảng cách ngang giữa các khối trong khay

        // ------------------------------------------------------------------ Màu
        private static readonly Color Cream = new Color32(255, 248, 228, 255);
        private static readonly Color CreamDim = new Color32(255, 232, 196, 235);
        private static readonly Color Brown = new Color32(126, 66, 24, 255);
        private static readonly Color Gold = new Color32(255, 214, 90, 255);

        private static readonly Vector2 TL = new Vector2(0f, 1f);
        private static readonly Vector2 TR = new Vector2(1f, 1f);
        private static readonly Vector2 TC = new Vector2(0.5f, 1f);
        private static readonly Vector2 MC = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BC = new Vector2(0.5f, 0f);

        private static TMP_FontAsset _font;

        [System.Serializable]
        private class ManifestItem
        {
            public string name;
            public int ppu = 100;
            public int[] border = new int[4];
        }

        [System.Serializable]
        private class Manifest
        {
            public List<ManifestItem> items = new List<ManifestItem>();
        }

        // ================================================================== Menu

        [MenuItem("Tools/Block Puzzle/Build UI (All)")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Block Puzzle", "Hãy thoát Play Mode rồi chạy lại.", "OK");
                return;
            }

            if (!ApplyImportSettings()) return;
            BuildScene();
        }

        [MenuItem("Tools/Block Puzzle/Import Art Settings Only")]
        public static void ImportOnlyMenu()
        {
            if (ApplyImportSettings())
                EditorUtility.DisplayDialog("Block Puzzle", "Đã cấu hình xong import cho sprite.", "OK");
        }

        // ================================================================== 1) Import sprite

        /// <summary>Đọc manifest.json và đặt Texture Type = Sprite, PPU, viền 9-slice cho từng ảnh.</summary>
        private static bool ApplyImportSettings()
        {
            AssetDatabase.Refresh();

            var manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ArtDir + "/manifest.json");
            if (manifestAsset == null)
            {
                EditorUtility.DisplayDialog("Block Puzzle",
                    "Không thấy " + ArtDir + "/manifest.json.\nHãy copy thư mục Assets/Art vào project.", "OK");
                return false;
            }

            var manifest = JsonUtility.FromJson<Manifest>(manifestAsset.text);
            foreach (ManifestItem item in manifest.items)
            {
                string path = ArtDir + "/" + item.name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning("[BlockPuzzle] Thiếu ảnh: " + path);
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = item.ppu;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                // Manifest: left, bottom, right, top (cùng thứ tự với Vector4 của Unity)
                importer.spriteBorder = new Vector4(item.border[0], item.border[1], item.border[2], item.border[3]);

                // Sliced / Tiled cần mesh Full Rect
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
            }

            return true;
        }

        // ================================================================== 2) Dựng scene

        private static void BuildScene()
        {
            var grid = Find<GridManager>();
            var spawner = Find<BlockSpawner>();
            var score = Find<ScoreManager>();
            var gm = Find<GameManager>();

            var missing = new List<string>();
            if (grid == null) missing.Add("GridManager");
            if (spawner == null) missing.Add("BlockSpawner");
            if (score == null) missing.Add("ScoreManager");
            if (gm == null) missing.Add("GameManager");
            if (missing.Count > 0)
            {
                EditorUtility.DisplayDialog("Block Puzzle",
                    "Scene đang mở thiếu: " + string.Join(", ", missing) +
                    ".\nHãy mở SampleScene (đã có GridRoot, Spawner, ScoreManager, GameManager) rồi chạy lại.", "OK");
                return;
            }

            _font = TMP_Settings.defaultFontAsset;
            if (_font == null)
                Debug.LogWarning("[BlockPuzzle] Chưa có font TMP mặc định. Vào Window > TextMeshPro > Import TMP Essential Resources.");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Block Puzzle UI");
            int undoGroup = Undo.GetCurrentGroup();

            SetupCamera();
            SetupBackground();
            SetupGrid(grid);
            SetupPrefabs(grid, spawner);
            SetupSlots(spawner);
            SetupBoardFrame(grid);
            Canvas hud = BuildHud(score, gm);
            SetupEffects(grid, gm);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = hud.gameObject;

            Debug.Log("[BlockPuzzle] Dựng UI xong. Nhấn Ctrl+S để lưu scene rồi bấm Play.");
            EditorUtility.DisplayDialog("Block Puzzle",
                "Dựng UI xong.\n\n- Nhấn Ctrl+S để lưu scene.\n- Bấm Play để thử.\n- Có thể bấm Ctrl+Z để hoàn tác.", "OK");
        }

        // ------------------------------------------------------------------ Camera

        private static void SetupCamera()
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogWarning("[BlockPuzzle] Không tìm thấy Camera.");
                return;
            }

            Undo.RecordObject(cam, "Setup Camera");
            Undo.RecordObject(cam.transform, "Setup Camera");
            cam.orthographic = true;
            cam.orthographicSize = 8.5f; // giá trị tạm để xem trong Editor, CameraFitter tự tính lúc chạy
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(58, 32, 16, 255);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            if (cam.GetComponent<CameraFitter>() == null) Undo.AddComponent<CameraFitter>(cam.gameObject);
            if (cam.GetComponent<Physics2DRaycaster>() == null) Undo.AddComponent<Physics2DRaycaster>(cam.gameObject);
        }

        // ------------------------------------------------------------------ Nền gỗ

        private static void SetupBackground()
        {
            var fitter = Find<BackgroundFitter>();
            if (fitter == null)
            {
                var go = new GameObject("Background");
                Undo.RegisterCreatedObjectUndo(go, "Create Background");
                go.AddComponent<SpriteRenderer>();
                fitter = go.AddComponent<BackgroundFitter>();
            }

            var sr = fitter.GetComponent<SpriteRenderer>();
            Undo.RecordObject(sr, "Setup Background");
            Undo.RecordObject(fitter.transform, "Setup Background");
            sr.sprite = LoadSprite("bg_wood");
            sr.sortingOrder = -100;

            // Xem trước trong Editor: phủ vùng 9.6 x 17 đơn vị (BackgroundFitter sẽ tính lại lúc chạy)
            if (sr.sprite != null)
            {
                Vector2 size = sr.sprite.bounds.size;
                fitter.transform.localScale = Vector3.one * Mathf.Max(9.6f / size.x, 17f / size.y);
                fitter.transform.position = Vector3.zero;
            }
        }

        // ------------------------------------------------------------------ Lưới

        /// <summary>Đặt GridOrigin sao cho tâm bảng nằm ở (0, GridCenterY).</summary>
        private static void SetupGrid(GridManager grid)
        {
            var so = new SerializedObject(grid);
            int w = so.FindProperty("width").intValue;
            int h = so.FindProperty("height").intValue;
            float cs = so.FindProperty("cellSize").floatValue;

            var originProp = so.FindProperty("gridOrigin");
            var origin = originProp.objectReferenceValue as Transform;
            if (origin == null)
            {
                var go = new GameObject("GridOrigin");
                Undo.RegisterCreatedObjectUndo(go, "Create GridOrigin");
                go.transform.SetParent(grid.transform, false);
                origin = go.transform;
                originProp.objectReferenceValue = origin;
                so.ApplyModifiedProperties();
            }

            Undo.RecordObject(origin, "Position GridOrigin");
            origin.position = new Vector3(-(w - 1) * cs * 0.5f, GridCenterY - (h - 1) * cs * 0.5f, 0f);
        }

        private static void SetupBoardFrame(GridManager grid)
        {
            var frame = Find<BoardFrame>();
            if (frame == null)
            {
                var go = new GameObject("BoardFrame");
                Undo.RegisterCreatedObjectUndo(go, "Create BoardFrame");
                go.transform.SetParent(grid.transform, false);
                go.AddComponent<SpriteRenderer>();
                frame = go.AddComponent<BoardFrame>();
            }

            var sr = frame.GetComponent<SpriteRenderer>();
            Undo.RecordObject(sr, "Setup BoardFrame");
            Undo.RecordObject(frame.transform, "Setup BoardFrame");
            sr.sprite = LoadSprite("board_frame");
            sr.sortingOrder = -1;
            sr.drawMode = SpriteDrawMode.Sliced;

            // Xem trước trong Editor (BoardFrame tự đặt lại lúc chạy)
            var gridSo = new SerializedObject(grid);
            float cs = gridSo.FindProperty("cellSize").floatValue;
            float pad = new SerializedObject(frame).FindProperty("padding").floatValue;
            sr.size = new Vector2(grid.Width * cs + pad * 2f, grid.Height * cs + pad * 2f);
            frame.transform.position = grid.BoardCenter;

            SetRef(frame, "grid", grid);
        }

        // ------------------------------------------------------------------ Prefab

        /// <summary>Gán sprite cho các prefab ô gạch. Prefab nào chưa có thì tạo mới trong Assets/Prefabs.</summary>
        private static void SetupPrefabs(GridManager grid, BlockSpawner spawner)
        {
            var emptyPrefab = GetRef<SpriteRenderer>(grid, "emptyCellPrefab");
            if (emptyPrefab == null)
            {
                emptyPrefab = CreateCellPrefab("Cell_Empty");
                SetRef(grid, "emptyCellPrefab", emptyPrefab);
            }
            ApplySprite(emptyPrefab, LoadSprite("cell_empty"));

            var blockPrefab = GetRef<SpriteRenderer>(grid, "blockPrefab");
            if (blockPrefab == null)
            {
                blockPrefab = CreateCellPrefab("Cell_Block");
                SetRef(grid, "blockPrefab", blockPrefab);
            }
            ApplySprite(blockPrefab, LoadSprite("cell_block"));

            var piece = GetRef<BlockPiece>(spawner, "piecePrefab");
            if (piece == null)
            {
                EnsureFolder(PrefabDir);
                var go = new GameObject("BlockPiece");
                go.AddComponent<BoxCollider2D>();
                go.AddComponent<BlockPiece>();
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/BlockPiece.prefab");
                Object.DestroyImmediate(go);
                piece = asset.GetComponent<BlockPiece>();
                SetRef(spawner, "piecePrefab", piece);
            }

            SetRef(piece, "cellPrefab", blockPrefab);
            EditorUtility.SetDirty(piece);
            AssetDatabase.SaveAssets();
        }

        private static SpriteRenderer CreateCellPrefab(string name)
        {
            EnsureFolder(PrefabDir);
            var go = new GameObject(name);
            go.AddComponent<SpriteRenderer>();
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            return asset.GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// Gán sprite và đặt màu trắng (màu thật do shape.color nhân lên: vàng, xanh ngọc...).
        /// Dùng SerializedObject nên ghi thẳng được vào prefab asset.
        /// </summary>
        private static void ApplySprite(SpriteRenderer sr, Sprite sprite)
        {
            if (sr == null || sprite == null) return;
            var so = new SerializedObject(sr);
            so.FindProperty("m_Sprite").objectReferenceValue = sprite;
            so.FindProperty("m_Color").colorValue = Color.white;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sr);
        }

        // ------------------------------------------------------------------ Khay

        private static void SetupSlots(BlockSpawner spawner)
        {
            var so = new SerializedObject(spawner);
            SerializedProperty slots = so.FindProperty("slots");

            if (slots.arraySize == 0)
            {
                slots.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var go = new GameObject("Slot" + i);
                    Undo.RegisterCreatedObjectUndo(go, "Create Slot");
                    go.transform.SetParent(spawner.transform, false);
                    slots.GetArrayElementAtIndex(i).objectReferenceValue = go.transform;
                }
                so.ApplyModifiedProperties();
            }

            int n = slots.arraySize;
            for (int i = 0; i < n; i++)
            {
                var t = slots.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (t == null) continue;
                Undo.RecordObject(t, "Position Slot");
                t.position = new Vector3((i - (n - 1) * 0.5f) * SlotSpacing, SlotY, 0f);
            }
        }

        // ================================================================== Hiệu ứng xóa hàng/cột

        /// <summary>Tạo prefab chữ điểm, object Effects (ClearEffects), ScreenShake trên camera và nối vào GameManager.</summary>
        private static void SetupEffects(GridManager grid, GameManager gm)
        {
            Material outBrown = OutlineMaterial("TMP_Outline_Brown", new Color32(92, 46, 20, 255), 0.22f);
            TextMeshPro popup = CreatePopupPrefab(outBrown);

            var fx = Find<ClearEffects>();
            if (fx == null)
            {
                var go = new GameObject("Effects");
                Undo.RegisterCreatedObjectUndo(go, "Create Effects");
                fx = go.AddComponent<ClearEffects>();
            }

            // ScreenShake nằm trên camera để rung cả thế giới game (HUD đứng yên)
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            ScreenShake shake = null;
            if (cam != null)
            {
                shake = cam.GetComponent<ScreenShake>();
                if (shake == null) shake = Undo.AddComponent<ScreenShake>(cam.gameObject);
            }

            SetRef(fx, "grid", grid);
            SetRef(fx, "shake", shake);
            SetRef(fx, "popupPrefab", popup);
            SetRef(fx, "chipSprite", LoadSprite("fx_chip"));
            SetRef(fx, "sparkSprite", LoadSprite("fx_spark"));
            SetRef(fx, "streakSprite", LoadSprite("fx_streak"));
            SetRef(gm, "effects", fx);
        }

        /// <summary>Prefab chữ điểm bay lên: TextMeshPro 3D (nằm trong thế giới game) có viền nâu.</summary>
        private static TextMeshPro CreatePopupPrefab(Material outline)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("ScorePopup");
            var t = go.AddComponent<TextMeshPro>();
            if (_font != null) t.font = _font;
            if (outline != null) t.fontSharedMaterial = outline;
            t.text = "+10";
            t.fontSize = 6f;
            t.color = Gold;
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.sortingOrder = 60; // trên ô gạch và hạt, dưới khối đang kéo
            t.rectTransform.sizeDelta = new Vector2(8f, 2f);

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/ScorePopup.prefab");
            Object.DestroyImmediate(go);
            return asset.GetComponent<TextMeshPro>();
        }

        // ================================================================== HUD

        private static Canvas BuildHud(ScoreManager score, GameManager gm)
        {
            HandleOldCanvases();

            // Material chữ có viền (tạo bản sao riêng để không sửa font gốc)
            Material outBrown = OutlineMaterial("TMP_Outline_Brown", new Color32(92, 46, 20, 255), 0.22f);
            Material outRed = OutlineMaterial("TMP_Outline_Red", new Color32(112, 26, 20, 255), 0.24f);
            Material outGreen = OutlineMaterial("TMP_Outline_Green", new Color32(28, 86, 30, 255), 0.22f);

            // ---- Canvas gốc
            var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD");
            canvasGo.layer = LayerMask.NameToLayer("UI");

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            // Expand: canvas luôn chứa trọn 1080x1920, nên HUD không bị lệch trên màn hẹp hay tablet
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;

            // ---- Vùng an toàn chứa HUD
            RectTransform safe = NewRect("SafeArea", canvasGo.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // ---- Khung điểm cao (trái, xanh lá) + vương miện
            Image bestPanel = NewImage("BestPanel", safe, LoadSprite("hud_panel_best"));
            Place(bestPanel.rectTransform, TL, TL, new Vector2(40f, -170f), new Vector2(440f, 150f));
            NewCaption(bestPanel.transform, TxtBest);
            TextMeshProUGUI bestValue = NewValue(bestPanel.transform, "0", outBrown);

            Image crown = NewImage("Crown", safe, LoadSprite("icon_crown"));
            Place(crown.rectTransform, TL, BC, new Vector2(260f, -166f), new Vector2(96f, 64f));

            // ---- Khung điểm hiện tại (phải, gỗ tối) + mascot
            Image scorePanel = NewImage("ScorePanel", safe, LoadSprite("hud_panel_score"));
            Place(scorePanel.rectTransform, TR, TR, new Vector2(-40f, -170f), new Vector2(440f, 150f));
            NewCaption(scorePanel.transform, TxtScore);
            TextMeshProUGUI scoreValue = NewValue(scorePanel.transform, "0", outBrown);

            Image mascot = NewImage("Mascot", safe, LoadSprite("mascot"));
            Place(mascot.rectTransform, TR, BC, new Vector2(-260f, -184f), new Vector2(170f, 133f));

            // ---- Nút cài đặt (bánh răng) -> Pause
            Button gear = NewButton("GearButton", safe, LoadSprite("btn_square"), null, 0f, null);
            Place(gear.image.rectTransform, TR, TR, new Vector2(-36f, -36f), new Vector2(120f, 120f));
            Image gearIcon = NewImage("Icon", gear.transform, LoadSprite("icon_gear"));
            Place(gearIcon.rectTransform, MC, MC, new Vector2(0f, 4f), new Vector2(76f, 76f));
            UnityEventTools.AddVoidPersistentListener(gear.onClick, gm.Pause);

            // ---- Hệ số combo (x1.0, x1.1...) nằm giữa, ngay dưới hai khung điểm
            TextMeshProUGUI combo = NewText("ComboText", safe, "x1.0", 64f, Gold,
                TextAlignmentOptions.Center, outBrown);
            Place(combo.rectTransform, TC, TC, new Vector2(0f, -352f), new Vector2(320f, 80f));

            // ---- Popup (nằm trên cùng, vẽ sau HUD)
            BuildGameOverPopup(canvasGo.transform, gm, outRed, outBrown);
            BuildPausePopup(canvasGo.transform, gm, outGreen, outBrown, outRed);

            // ---- Gán tham chiếu UI cho ScoreManager
            SetRef(score, "scoreText", scoreValue);
            SetRef(score, "highScoreText", bestValue);
            SetRef(score, "comboText", combo);

            return canvas;
        }

        private static void BuildGameOverPopup(Transform parent, GameManager gm, Material outRed, Material outBrown)
        {
            (RectTransform root, RectTransform window) = NewPopupShell(parent, "GameOverPanel",
                new Vector2(760f, 880f), TxtGameOver, outRed);

            TextMeshProUGUI cap1 = NewText("YourScoreCaption", window, TxtYourScore, 44f, Brown,
                TextAlignmentOptions.Center, null);
            Place(cap1.rectTransform, TC, TC, new Vector2(0f, -190f), new Vector2(640f, 60f));

            TextMeshProUGUI finalScore = NewText("FinalScore", window, "0", 150f, Brown,
                TextAlignmentOptions.Center, null);
            finalScore.enableAutoSizing = true;
            finalScore.fontSizeMin = 70f;
            finalScore.fontSizeMax = 150f;
            Place(finalScore.rectTransform, TC, TC, new Vector2(0f, -260f), new Vector2(640f, 190f));

            TextMeshProUGUI cap2 = NewText("BestCaption", window, TxtBestScore, 40f, Brown,
                TextAlignmentOptions.Center, null);
            Place(cap2.rectTransform, TC, TC, new Vector2(0f, -480f), new Vector2(640f, 56f));

            TextMeshProUGUI finalBest = NewText("FinalBest", window, "0", 84f, new Color32(214, 126, 20, 255),
                TextAlignmentOptions.Center, null);
            finalBest.enableAutoSizing = true;
            finalBest.fontSizeMin = 40f;
            finalBest.fontSizeMax = 84f;
            Place(finalBest.rectTransform, TC, TC, new Vector2(0f, -545f), new Vector2(640f, 110f));

            Button replay = NewButton("ReplayButton", window, LoadSprite("btn_gold"), TxtPlayAgain, 56f, outBrown);
            Place(replay.image.rectTransform, BC, BC, new Vector2(0f, 70f), new Vector2(520f, 150f));
            UnityEventTools.AddVoidPersistentListener(replay.onClick, gm.Restart);

            SetRef(gm, "gameOverPanel", root.gameObject);
            SetRef(gm, "finalScoreText", finalScore);
            SetRef(gm, "finalBestText", finalBest);
        }

        private static void BuildPausePopup(Transform parent, GameManager gm, Material outGreen, Material outBrown,
            Material outTitle)
        {
            (RectTransform root, RectTransform window) = NewPopupShell(parent, "PausePanel",
                new Vector2(720f, 640f), TxtPaused, outTitle);

            Button resume = NewButton("ResumeButton", window, LoadSprite("btn_green"), TxtResume, 56f, outGreen);
            Place(resume.image.rectTransform, TC, TC, new Vector2(0f, -240f), new Vector2(500f, 150f));
            UnityEventTools.AddVoidPersistentListener(resume.onClick, gm.Resume);

            Button restart = NewButton("RestartButton", window, LoadSprite("btn_gold"), TxtRestart, 56f, outBrown);
            Place(restart.image.rectTransform, TC, TC, new Vector2(0f, -420f), new Vector2(500f, 150f));
            UnityEventTools.AddVoidPersistentListener(restart.onClick, gm.Restart);

            SetRef(gm, "pausePanel", root.gameObject);
        }

        /// <summary>Khung popup chung: nền tối + cửa sổ giấy + ruy-băng tiêu đề + hiệu ứng mở.</summary>
        private static (RectTransform root, RectTransform window) NewPopupShell(Transform parent, string name,
            Vector2 windowSize, string title, Material titleMat)
        {
            RectTransform root = NewRect(name, parent);
            Stretch(root);

            Image dim = NewImage("Dim", root, null, new Color(0f, 0f, 0f, 0.62f), true); // chặn chạm xuống dưới
            Stretch(dim.rectTransform);

            RectTransform window = NewImage("Window", root, LoadSprite("popup_panel"), null, true).rectTransform;
            Place(window, MC, MC, Vector2.zero, windowSize);

            Image ribbon = NewImage("Ribbon", window, LoadSprite("title_ribbon"));
            Place(ribbon.rectTransform, TC, MC, Vector2.zero, new Vector2(620f, 130f));
            TextMeshProUGUI titleText = NewText("Title", ribbon.transform, title, 64f, Cream,
                TextAlignmentOptions.Center, titleMat);
            Stretch(titleText.rectTransform, 0f, 10f, 0f, 0f);

            root.gameObject.AddComponent<CanvasGroup>();
            var anim = root.gameObject.AddComponent<PopupAnimator>();
            SetRef(anim, "window", window);

            root.gameObject.SetActive(false); // GameManager sẽ bật khi cần
            return (root, window);
        }

        /// <summary>Xử lý Canvas cũ: xóa HUDCanvas do tool tạo trước đó, hỏi trước khi tắt Canvas khác.</summary>
        private static void HandleOldCanvases()
        {
            var others = new List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.transform.parent != null) continue; // chỉ xét Canvas gốc
                if (c.name == CanvasName) Undo.DestroyObjectImmediate(c.gameObject);
                else if (c.gameObject.activeSelf) others.Add(c);
            }

            if (others.Count == 0) return;

            var names = new List<string>();
            foreach (Canvas c in others) names.Add(c.name);

            bool disable = EditorUtility.DisplayDialog("Block Puzzle",
                "Scene đang có Canvas cũ: " + string.Join(", ", names) +
                ".\nTắt chúng đi (không xóa) để dùng HUD mới?", "Tắt Canvas cũ", "Giữ nguyên");
            if (!disable) return;

            foreach (Canvas c in others)
            {
                Undo.RecordObject(c.gameObject, "Disable old Canvas");
                c.gameObject.SetActive(false);
            }
        }

        // ================================================================== Dựng UI: helper

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Đặt RectTransform theo neo cố định (không kéo giãn).</summary>
        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>Kéo giãn kín cha, có thể chừa lề (trái, dưới, phải, trên).</summary>
        private static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = MC;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = MC;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image NewImage(string name, Transform parent, UnityEngine.Sprite sprite,
            Color? tint = null, bool raycast = false)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = tint ?? Color.white;
            img.raycastTarget = raycast;

            if (sprite != null)
            {
                bool sliced = sprite.border != Vector4.zero;
                img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                img.preserveAspect = !sliced;
            }
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align, Material outline)
        {
            RectTransform rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            if (outline != null) t.fontSharedMaterial = outline;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Nhãn nhỏ phía trên số điểm trong khung HUD.</summary>
        private static void NewCaption(Transform panel, string text)
        {
            TextMeshProUGUI t = NewText("Caption", panel, text, 34f, CreamDim, TextAlignmentOptions.Center, null);
            SetAnchors(t.rectTransform, new Vector2(0f, 0.6f), new Vector2(1f, 0.93f));
        }

        /// <summary>Số điểm lớn trong khung HUD, tự co chữ khi số quá dài.</summary>
        private static TextMeshProUGUI NewValue(Transform panel, string text, Material outline)
        {
            TextMeshProUGUI t = NewText("Value", panel, text, 84f, Cream, TextAlignmentOptions.Center, outline);
            t.enableAutoSizing = true;
            t.fontSizeMin = 40f;
            t.fontSizeMax = 84f;
            SetAnchors(t.rectTransform, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.64f));
            return t;
        }

        private static Button NewButton(string name, Transform parent, UnityEngine.Sprite sprite, string label,
            float fontSize, Material outline)
        {
            Image img = NewImage(name, parent, sprite, null, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            ColorBlock colors = btn.colors;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.fadeDuration = 0.05f;
            btn.colors = colors;

            if (!string.IsNullOrEmpty(label))
            {
                TextMeshProUGUI t = NewText("Label", img.transform, label, fontSize, Cream,
                    TextAlignmentOptions.Center, outline);
                Stretch(t.rectTransform, 0f, 14f, 0f, 0f); // nhấc chữ lên một chút vì nút có đáy 3D
            }
            return btn;
        }

        // ================================================================== Helper chung

        private static T Find<T>() where T : Object
        {
            return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }

        private static UnityEngine.Sprite LoadSprite(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(ArtDir + "/" + name + ".png");
            if (s == null) Debug.LogWarning("[BlockPuzzle] Không load được sprite: " + name);
            return s;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError("[BlockPuzzle] Không tìm thấy field '" + field + "' trong " + target.GetType().Name);
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static T GetRef<T>(Object target, string field) where T : Object
        {
            SerializedProperty p = new SerializedObject(target).FindProperty(field);
            return p == null ? null : p.objectReferenceValue as T;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Tạo (hoặc cập nhật) material chữ có viền, sao chép từ material của font mặc định.</summary>
        private static Material OutlineMaterial(string name, Color outline, float width)
        {
            if (_font == null) return null;

            EnsureFolder(MatDir);
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(_font.material);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.EnableKeyword("OUTLINE_ON");
            mat.SetColor("_OutlineColor", outline);
            mat.SetFloat("_OutlineWidth", width);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
