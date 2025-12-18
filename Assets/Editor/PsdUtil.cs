using Assets.Editor;
using Ntreev.Library.Psd;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PSDUnity;
using PSDUnity.Analysis;
using PSDUnity.UGUI;
using Cysharp.Threading.Tasks;
using System.Collections;
using log4net.Core;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.SceneManagement;

namespace Assets.Editor
{
    public static class PsdUtil
    {
        private static string _exportPath;
        public static string exportPath
        {
            get => _exportPath;          
            set
            {
                _exportPath = value;
            }
        }
        public static string ToUnityPath(this string s, bool withAsset = true)
        {
            return GetUnityAssetPath(s, withAsset);
        }
        /// <summary>
        /// 获取unity资源路径 
        /// </summary>
        /// <param name="fullPath全路径"></param>
        /// <returns></returns>
        public static string GetUnityAssetPath(string fullPath, bool withAsset = true)
        {
            fullPath = GetLinuxPath(fullPath);
            var index = fullPath.IndexOf("Assets");
            if (index >= 0)
            {
                if (withAsset)
                    return fullPath.Substring(index);
                int idx = index + "Assets/".Length;
                if (idx > fullPath.Length - 1)
                {
                    return "";
                }
                else
                {
                    return fullPath.Substring(index + "Assets/".Length);
                }

            }
            return fullPath;
        }

        /// <summary>
        /// 删除无效的文件夹 
        /// </summary>
        /// <param name="rootPath"></param>
        public static void CleanEmptyDirectories(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                Console.WriteLine($"目录不存在: {rootPath}");
                return;
            }

            try
            {
                // 1. 先递归处理所有子目录
                foreach (string subDir in Directory.GetDirectories(rootPath))
                {
                    CleanEmptyDirectories(subDir);
                }

                // 2. 检查当前目录是否为空
                if (IsDirectoryEmpty(rootPath))
                {
                    try
                    {
                        if (File.Exists(rootPath + ".meta"))
                        {
                            File.Delete(rootPath + ".meta");
                        }
                        Directory.Delete(rootPath);
                        Console.WriteLine($"已删除空目录: {rootPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"删除目录失败 {rootPath}: {ex.Message}");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"无权限访问目录: {rootPath}");
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine($"目录不存在: {rootPath}");
            }
        }
        private static bool IsDirectoryEmpty(string path)
        {
            try
            {
                return Directory.GetFiles(path).Length == 0 &&
                       Directory.GetDirectories(path).Length == 0;
            }
            catch
            {
                return false;
            }
        }


        private static void FocusWindows()
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                Debug.LogError("未找到 GameView 类型");
                return;
            }

            // 获取 GameView 实例
            var gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView != null)
            {
                gameView.Focus();
                Debug.Log("Game 视图已激活");
            }
        }




        [MenuItem("GameObject/右键菜单/节点标准化", priority = -4)]
        static void HierarchyStandarded()
        {

            var filePath = (Application.dataPath + "/Editor/EditorExpand/translate/config.txt").ToLinuxPath();
            var fileContent = File.ReadAllText(filePath);
            var mapReplace = new Dictionary<string, string>();

            // 匹配：{ "任意内容","任意内容" }   支持空格、制表符、换行
            var m = Regex.Matches(fileContent, @"\{\s*""([^""]+)""\s*,\s*""([^""]+)""\s*\}");

            foreach (Match item in m)
            {
                string key = item.Groups[1].Value;
                string val = item.Groups[2].Value;
                // 重复 key 直接覆盖，可按需改成累加或抛异常
                mapReplace[key] = val;
            }


            var current = PrefabStageUtility.GetCurrentPrefabStage();
            if (current == null) Debug.LogError("请在预设编辑模式进行");
            var go = current.prefabContentsRoot;

            var pattern = @"[\u4e00-\u9fff]"; // 匹配中文
            var patternReg = new Regex(pattern);
            HashSet<string> unknown = new HashSet<string>();
            var all = go.GetComponentsInChildren<Transform>(true).Where((x) => x != go.transform).ToList();
            foreach (var sub in all)
            {
                foreach (Match tt in Regex.Matches(sub.name, pattern))
                {
                    string ch = tt.Value;
                    if (!mapReplace.ContainsKey(ch))
                        unknown.Add(ch);
                }
            }
            if (unknown.Count > 0)
            {
                StringBuilder sb = new StringBuilder("以下中文未被 mapReplace 覆盖，请补充：");
                foreach (var ch in unknown.OrderBy(x => x))
                    sb.Append(ch);
                Debug.LogError($"请配置{filePath}中英文映射" + sb.ToString());
            }

            int id = 0;
            foreach (var sub in all)
            {
                string newName = mapReplace.Keys.Aggregate(sub.name, (current, key) => current.Replace(key, mapReplace[key]));
                if (patternReg.IsMatch(newName))
                {
                    id++;
                    newName = Regex.Replace(newName, pattern, id.ToString());
                }

                if (newName != sub.name)
                {
                    sub.name = newName;
                }
            }
            EditorUtility.SetDirty(go);
        }
        [MenuItem("Assets/资源名标准化", false, 100000-1)]
        public static void SetStandardVarient()
        {

            var filePath = (Application.dataPath + "/Editor/EditorExpand/translate/config.txt").ToLinuxPath();
            var fileContent = File.ReadAllText(filePath);
            var mapReplace = new Dictionary<string, string>();

            // 匹配：{ "任意内容","任意内容" }   支持空格、制表符、换行
            var matchList = Regex.Matches(fileContent, @"\{\s*""([^""]+)""\s*,\s*""([^""]+)""\s*\}");

            foreach (Match item in matchList)
            {
                string key = item.Groups[1].Value;
                string val = item.Groups[2].Value;
                // 重复 key 直接覆盖，可按需改成累加或抛异常
                mapReplace[key] = val;
            }


            var pattern = @"[\u4e00-\u9fff]"; // 匹配中文
            var patternReg = new Regex(pattern);

            var fileList = new List<string>();

            var selected = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);
            foreach (var obj in selected)
            {
                string folderPath = AssetDatabase.GetAssetPath(obj);
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    fileList.Add(folderPath);
                }
                else
                {
                    fileList.AddRange(AssetDatabase.FindAssets("", new string[] { folderPath }).Select(AssetDatabase.GUIDToAssetPath));
                }

            }
            fileList = fileList.Where((x) => patternReg.IsMatch(x) && !AssetDatabase.IsValidFolder(x)).Distinct().ToList();



            // ---------- 新增：收集未映射的中文 ----------
            HashSet<string> unknown = new HashSet<string>();
            foreach (var file in fileList)
            {
                foreach (Match m in Regex.Matches(file, pattern))
                {
                    string ch = m.Value;
                    if (!mapReplace.ContainsKey(ch))
                        unknown.Add(ch);
                }
            }

            if (unknown.Count > 0)
            {
                StringBuilder sb = new StringBuilder("以下中文未被 mapReplace 覆盖，请补充：");
                foreach (var ch in unknown.OrderBy(x => x))
                    sb.Append(ch);
                Debug.LogError("请配置中英文映射" + sb.ToString());
            }


            foreach (var file in fileList)
            {                
                
                var folderPath = file.ToFolderParent();
                
                string newFile = mapReplace.Keys.Aggregate(file, (current, key) => current.Replace(key, mapReplace[key]));



                newFile = Regex.Replace(newFile, pattern, "image");
                var file2 = newFile;
                if (File.Exists(newFile))
                {
                    Debug.LogError("需要清理资源" + newFile);
                    AssetDatabase.DeleteAsset(newFile.Substring(newFile.IndexOf("Assets/")));
                }
                else
                {
                    CreateDir(file2,true);
                    var value = AssetDatabase.MoveAsset(file, file2);
                    if (!string.IsNullOrEmpty(value))
                    {
                        Debug.LogError(value + "重命名错误" + file);
                    }
                }
            }
            AssetDatabase.Refresh();


            //删除空目录 
            var selected2 = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);
            foreach (var obj in selected2)
            {
                string folderPath = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(folderPath))
                {
                    CleanEmptyDirectories(folderPath);
                    
                }
            }
            AssetDatabase.Refresh();


        }

        [UnityEditor.MenuItem("Assets/psd转prefab",priority = 100000)]
        public async static UniTask PsdToPrefab()
        {
            var path = AssetDatabase.GetAssetPath(Selection.objects[0]);
            if (!path.EndsWith(".psd"))
            {
                Debug.LogError("请选中psd进行操作");
            }
            var fileName = Path.GetFileNameWithoutExtension(path);
            var folder = path.ToFolderParent();

            var needDel = new string[] { "rule.asset", fileName + ".asset" };

            foreach (var delName in needDel)
            {
                var delPath = Path.Combine(folder, delName);
                if (File.Exists(delPath))
                {
                    AssetDatabase.DeleteAsset(delPath);
                }
            }
            AssetDatabase.Refresh();
            ExportUtility.exportPath = (folder + "/Image").ToLinuxPath();

            PSDImporterUtility.exportPath = ExportUtility.exportPath;

            exportPath = (folder + "/Image").ToLinuxPath();

            var exporter = ScriptableObject.CreateInstance<PSDUnity.Data.Exporter>();
            exporter.name = Path.GetFileNameWithoutExtension(path);
            exporter.psdFile = path;
            exporter.ruleObj = RuleHelper.CreateRuleObject("rule.asset");
            exporter._exportPath = path.ToFolderParent();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();



            RuleHelper.LoadImageImports(exporter.ruleObj, () => {
                RuleHelper.LoadLayerImports(exporter.ruleObj);
            }, folder + "/rule.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var time = DateTime.Now;

            //这里目前不清楚是什么机制导致 ，点击psd2ugui之后需要再次点击unity使unity在此获得焦点才会触发后续逻辑
            // 可能是unity触发了编译 导致update事件丢了
            //所以下面这段代码 我们手动设置一下unity编辑器的焦点 

            // 通过反射强制 设置焦点 
            FocusWindows();
            await  UniTask.WaitUntil(() =>
            {
                return exporter.ruleObj.layerImports.Count > 0;
            });
            Debug.Log("await了多久" + (DateTime.Now - time).TotalMilliseconds);
            // 写入资源文件
            AssetDatabase.CreateAsset(exporter, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset"));


            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GroupNodeItem rootNode = null;

            var psd = PsdDocument.Create(exporter.psdFile);
            if (!string.IsNullOrEmpty(exporter.psdFile))
            {


                if (psd != null)
                {
                    try
                    {
                        var rootSize = new Vector2(psd.Width, psd.Height);
                        ExportUtility.InitPsdExportEnvrioment(exporter, rootSize);
                        rootNode = new GroupNodeItem(new Rect(Vector2.zero, rootSize), 0, -1);
                        rootNode.displayName = exporter.name;
                        var groupDatas = ExportUtility.CreatePictures(psd.Childs, rootSize, exporter.ruleObj.defultUISize, exporter.ruleObj.forceSprite);
                        if (groupDatas != null)
                        {
                            foreach (var groupData in groupDatas)
                            {
                                rootNode.AddChild(groupData);
                                ExportUtility.ChargeTextures(exporter, groupData);
                            }
                        }
                        var list = new List<GroupNodeItem>();
                        TreeViewUtility.TreeToList<GroupNodeItem>(rootNode, list, true);
                        exporter.groups = list.ConvertAll(x => x.data);
                        EditorUtility.SetDirty(exporter);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                        psd.Dispose();
                    }

                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Canvas gameCanvas = GameObject.Find("Root")?.GetComponent<Canvas>();

            var canvasObj = Array.Find(Selection.objects, x => x is GameObject && (x as GameObject).GetComponent<Canvas>() != null);

            gameCanvas = gameCanvas ?? (canvasObj == null ? UnityEngine.Object.FindObjectOfType<Canvas>() : (canvasObj as GameObject).GetComponent<Canvas>());
            var ctrl = PSDImporterUtility.CreatePsdImportCtrlSafty(exporter.ruleObj, exporter.ruleObj.defultUISize, gameCanvas);
            ctrl.Import(rootNode.data);
            AssetDatabase.Refresh();
            GameObject prefabRoot = null;
            if (gameCanvas == null)
            {
                var allCanvas = Resources.FindObjectsOfTypeAll(typeof(Canvas));
                foreach(var canvas in allCanvas)
                {
                    prefabRoot = (canvas as Canvas)?.transform?.Find(rootNode.displayName)?.gameObject;
                    if(prefabRoot != null)
                        break;
                }
            }
            else
            {
                prefabRoot = gameCanvas?.transform?.Find(rootNode.displayName)?.gameObject;
            }

            if (PrefabUtility.SaveAsPrefabAsset(prefabRoot.gameObject, exportPath.ToFolderParent() + $"/{rootNode.displayName}.prefab"))
            {
                GameObject.DestroyImmediate(prefabRoot.gameObject);
                foreach (var delName in needDel)
                {
                    var delPath = Path.Combine(folder, delName);
                    if (File.Exists(delPath))
                    {
                        AssetDatabase.DeleteAsset(delPath);
                    }
                }
                AssetDatabase.Refresh();
            }
            psd.Dispose();
        }

        public static string ToFolderParent(this string s)
        {
            s = s.ToLinuxPath();
            if (s.EndsWith("/"))
            {
                s = s.Remove(s.Length - 1);
            }
            if (s.LastIndexOf("/") > 0)
            {
                return s.Substring(0, s.LastIndexOf("/"));
            }
            return s;
        }
        public static void CreateDir(string path,bool createUnityDir = false)
        {
            if (File.Exists(path))
            {
                return;
            }
            if (Directory.Exists(path))
            {
                return;
            }

            var father = Directory.GetParent(path);

            while (!father.Exists)
            {
                if(createUnityDir)
                {

                    AssetDatabase.CreateFolder(father.FullName.ToFolderParent().ToUnityPath(), father.Name);
                    AssetDatabase.Refresh();
                }
                else
                {
                    Directory.CreateDirectory(father.FullName);
                }                    
                father = Directory.GetParent(father.FullName);
            }
        }
        public static string GetLinuxPath(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }
            return s.Replace("\\", "/");
        }

        public static string ToLinuxPath(this string s)
        {
            return GetLinuxPath(s);
        }
    }
}