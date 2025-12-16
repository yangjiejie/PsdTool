
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
using UnityEditor.UI;

namespace Assets.Editor
{
    public static class PsdUtil
    {
      

   

        [UnityEditor.MenuItem("Assets/psd转prefab")]
        public  async static  UniTask  PsdToPrefab()
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

            var exporter = ScriptableObject.CreateInstance<PSDUnity.Data.Exporter>();
            exporter.name = Path.GetFileNameWithoutExtension(path);
            exporter.psdFile = path;
            exporter.ruleObj = RuleHelper.CreateRuleObject("rule.asset");
            exporter._exportPath = path.ToFolderParent();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = exporter.ruleObj;


            RuleHelper.LoadImageImports(exporter.ruleObj, () => {
                RuleHelper.LoadLayerImports(exporter.ruleObj);
            }, folder + "/rule.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = null;

            var time = DateTime.Now;

            
            await UniTask.WaitUntil(() =>
            {
                return exporter.ruleObj.imageImports.Count > 0;
            });

            Debug.Log("await了多久" + (DateTime.Now - time).TotalMilliseconds);


            // 确保路径唯一

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
                        psd.Dispose();
                    }

                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();



            if(exporter == null || exporter.ruleObj == null)
            {
                Debug.LogError("exporter == null");
            }

            var canvasObj = Array.Find(Selection.objects, x => x is GameObject && (x as GameObject).GetComponent<Canvas>() != null);
            var ctrl = PSDImporterUtility.CreatePsdImportCtrlSafty(exporter.ruleObj, exporter.ruleObj.defultUISize, canvasObj == null ? UnityEngine.Object.FindObjectOfType<Canvas>() : (canvasObj as GameObject).GetComponent<Canvas>());
            ctrl.Import(rootNode.data);
            AssetDatabase.Refresh();
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
        public static void CreateDir(string path)
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
                Directory.CreateDirectory(father.FullName);
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
