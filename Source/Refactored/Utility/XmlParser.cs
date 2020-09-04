﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Verse;

namespace Analyzer
{
    public static class XmlParser
    {
        public static void CollectXmlData()
        {
            foreach (DirectoryInfo dir in ModLister.AllActiveModDirs)
            {
                var xmlFiles = dir.GetFiles("Analyzer.xml");
                if (xmlFiles.Length != 0)
                {
                    foreach (var file in xmlFiles)
                    {
                        // load our xml file
                        var doc = new XmlDocument();
                        doc.Load(file.OpenRead());

                        // parse our doc
                        Parse(doc);
                    }
                }
            }

        }
        private static void Parse(XmlDocument doc)
        {
            foreach (XmlNode node in doc.DocumentElement.ChildNodes) // entries should be 
            {
                var meths = new HashSet<MethodInfo>();
                foreach (XmlNode child in node.ChildNodes)
                {
                    switch (child.Name.ToLower())
                    {
                        case "methods": case "method": 
                            foreach(XmlNode method in child.ChildNodes)
                                meths.Add(ParseMethod(method.InnerText)); break;
                        case "types": case "type": 
                            foreach(XmlNode type in child.ChildNodes)
                                meths.AddRange(ParseTypeMethods(type.InnerText)); break;
                        default:
                            Log.Error($"[Analyzer] Attempting to read unknown value from an Analyzer.xml, the given input was {child.Name}, it should have been either '(M/m)ethods' or '(T/t)ypes'");
                            break;
                    }
                }

                Type myType = DynamicTypeBuilder.CreateType(node.Name, meths);

                foreach (ProfileTab profileTab in AnalyzerState.SideTabCategories)
                {
                    if (profileTab.UpdateMode == UpdateMode.ModderAdded)
                    {
                        Entry mode = Entry.Create(myType.Name, UpdateMode.ModderAdded, null, false, myType);
                        profileTab.Modes.Add(mode, null);
                        break;
                    }
                }
            }
        }

        private static MethodInfo ParseMethod(string str)
        {
            return AccessTools.Method(str);
        }
        private static IEnumerable<MethodInfo> ParseTypeMethods(string str)
        {
            return Utility.GetTypeMethods(str);
        }
    }
}
