using HarmonyLib;
using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;

namespace GhostBuster.Patches
{
    [HarmonyPatch(typeof(GameState), nameof(GameState.Update))]
    static class GameStateUpdatePatch
    {
        static void Prefix(GameState __instance)
        {
            if (GameSettings.GetInstance().GameMode == GameState.GameMode.CHALLENGE && !GameState.ChatSystem.ChatMode)
            {
                if (Input.GetKeyDown(GhostBusterMod.ToggleGhostsKey.Value))
                {
                    ToggleGhosts();
                }
                if (Input.GetKeyDown(GhostBusterMod.SwitchGhostModeKey.Value))
                {
                    SwitchGhostMode();
                }
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(GhostBusterMod.StoreGhostDataKey.Value))
                {
                    StoreGhostData();
                }
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(GhostBusterMod.LoadGhostDataKey.Value))
                {
                    LoadGhostData();
                }
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(GhostBusterMod.ToggleGhostTextKey.Value))
                {
                    ToggleGhostText();
                }
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(GhostBusterMod.ClearAllGhostReplaysKey.Value))
                {
                    ClearAllGhostReplays();
                }
            }
        }

        static void ToggleGhosts()
        {
            GhostBusterMod.ShowGhosts.Value = !GhostBusterMod.ShowGhosts.Value;
            DisplayMessage("Show Replay Ghosts: " + (GhostBusterMod.ShowGhosts.Value ? "Enabled" : "Disabled"));
        }

        static void ToggleGhostText()
        {
            GhostBusterMod.ShowGhostText.Value = !GhostBusterMod.ShowGhostText.Value;
            DisplayMessage("Show Ghost text: " + (GhostBusterMod.ShowGhostText.Value ? "Enabled" : "Disabled"));
        }
        static void ClearAllGhostReplays()
        {
            GhostBusterMod.ClearAllReplayGhostsData();
            DisplayMessage("Cleared all GhostReplays!");
        }

        public static void StoreGhostData()
        {
            XmlDocument xmlExport = new XmlDocument();
            XmlElement xmlElement = xmlExport.CreateElement("GhostDataExport");

            XmlAttribute xmlAttribute = xmlExport.CreateAttribute("level");
            xmlAttribute.Value = GhostBusterMod.GetCurrentLevelName();
            xmlElement.Attributes.Append(xmlAttribute);

            xmlExport.AppendChild(xmlElement);

            foreach (Character c in GhostBusterMod.GhostFraid.Values)
            {
                GhostRecorder gr = new GhostRecorder();
                gr.ghostData = c.replayData;
                gr.trackedChar = c;
                //TODO: Implement own serialize, so we don't need to compress and uncompress or patch SerializeData
                byte[] bytes = gr.SerializeData(); // SevenZipHelper.Decompress(gr.SerializeData());
                XmlDocument xmlDocument = XMLFromBytes(bytes);
                if (xmlDocument != null)
                {
                    XmlNode importNode = xmlExport.ImportNode(xmlDocument.FirstChild, true);
                    xmlElement.AppendChild(importNode);
                }
                /*
                EnhanceXMLData(ref xmlDocument, c);
                */
            }

            Debug.Log(xmlExport.OuterXml);
            GUIUtility.systemCopyBuffer = xmlExport.OuterXml;
            DisplayMessage("Ghost Data stored to clipboard!");
        }

        public static XmlDocument XMLFromBytes(byte[] bytes)
        {
            string @string = Encoding.UTF8.GetString(bytes);

            Debug.Log("@string " + @string);

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.XmlResolver = null;
            try
            {
                xmlDocument.LoadXml(@string);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            return xmlDocument;
        }

        public static void EnhanceXMLData(ref XmlDocument xmlDocument, GhostData gdata)
        {
            if (xmlDocument != null && gdata != null)
            {
                XmlNodeList list = xmlDocument.GetElementsByTagName("CharacterInfo");
                if (list.Count > 0)
                {
                    XmlAttribute xmlAttribute = xmlDocument.CreateAttribute("level");
                    xmlAttribute.Value = GhostBusterMod.GetCurrentLevelName();
                    list[0].Attributes.Append(xmlAttribute);


                    XmlAttribute xmlAttribute2 = xmlDocument.CreateAttribute("animal");
                    xmlAttribute2.Value = gdata.animal.ToString();
                    list[0].Attributes.Append(xmlAttribute2);

                    XmlAttribute xmlAttribute3 = xmlDocument.CreateAttribute("lastTime");
                    xmlAttribute3.Value = gdata.lastTime.ToString();
                    list[0].Attributes.Append(xmlAttribute3);
                }
            }
            else
            {
                Debug.LogError("EnhanceXMLData missing xmlDocument " + xmlDocument + " or c " + gdata);
            }

        }
        static void LoadGhostData()
        {
            DisplayMessage("Loading Ghost Data from clipboard!");

            if (GhostBusterMod.ClearStoredGhosts.Value)
            {
                GhostBusterMod.StoredReplays = new Dictionary<string, List<GhostData>>();
            }

            try
            {

                XmlDocument xmlImport = new XmlDocument();
                xmlImport.XmlResolver = null;
                xmlImport.LoadXml(GUIUtility.systemCopyBuffer);
                XmlElement documentElement = xmlImport.DocumentElement;
                string levelname = QuickSaver.ParseAttrStr(documentElement, "level", string.Empty);
                Debug.Log("add replay for level: " + levelname);
                foreach (XmlElement GhostInfo in documentElement.GetElementsByTagName("GhostInfo"))
                {
                    GhostData gd = new GhostData();

                    XmlNodeList CharacterInfos = GhostInfo.GetElementsByTagName("CharacterInfo");
                    if (CharacterInfos.Count > 0)
                    {
                        ParseCharacterInfo(ref gd, CharacterInfos[0]);
                    }

                    XmlNodeList GhostDataElement = GhostInfo.GetElementsByTagName("GhostData");
                    if (GhostDataElement.Count == 1)
                    {
                        ParseGhostDataPoints(ref gd, GhostDataElement[0]);
                        if (!GhostBusterMod.StoredReplays.ContainsKey(levelname))
                        {
                            GhostBusterMod.StoredReplays.Add(levelname, new List<GhostData> { gd });
                        }
                        else
                        {
                            GhostBusterMod.StoredReplays[levelname].Add(gd);
                        }

                    }
                    GhostBusterMod.ClearReplayGhosts();

                }
            }
            catch (Exception e)
            {
                DisplayMessage("Failed to parse Ghost Data: " + e.GetType());
                Debug.LogError("Failed to parse Ghost Data: " + e);
            }
        }

        public static void ParseCharacterInfo(ref GhostData gd, XmlNode CharacterInfo)
        {


            gd.playerName = QuickSaver.ParseAttrStr(CharacterInfo, "PlayerName", string.Empty);


            Array values = Enum.GetValues(typeof(Outfit.OutfitType));
            gd.outfits = new int[values.Length];

            foreach (Outfit.OutfitType ot in values)
            {
                gd.outfits[((int)ot)] = QuickSaver.ParseAttrInt(CharacterInfo, ot.ToString(), -1);
            }

            string animal = QuickSaver.ParseAttrStr(CharacterInfo, "animal", string.Empty);
            gd.animal = (Character.Animals)Enum.Parse(typeof(Character.Animals), animal);

            float lastTime = QuickSaver.ParseAttrFloat(CharacterInfo, "lastTime", float.PositiveInfinity);
            gd.lastTime = lastTime;
        }

        public static void ParseGhostDataPoints(ref GhostData gd, XmlNode ghostdataelement)
        {
            foreach (XmlElement datapoint in ghostdataelement.SelectNodes("datapoint"))
            {
                GhostData.GhostDataPoint ghostDataPoint = new GhostData.GhostDataPoint();
                ghostDataPoint.valid = true;
                ghostDataPoint.timestamp = QuickSaver.ParseAttrFloat(datapoint, "timestamp", 0);
                ghostDataPoint.frameTimestamp = QuickSaver.ParseAttrFloat(datapoint, "frameTimestamp", 0);
                ghostDataPoint.position.x = QuickSaver.ParseAttrFloat(datapoint, "posX", 0);
                ghostDataPoint.position.y = QuickSaver.ParseAttrFloat(datapoint, "posY", 0);
                ghostDataPoint.position.z = QuickSaver.ParseAttrFloat(datapoint, "posZ", 0);
                ghostDataPoint.framePosition.x = QuickSaver.ParseAttrFloat(datapoint, "framePosX", 0);
                ghostDataPoint.framePosition.y = QuickSaver.ParseAttrFloat(datapoint, "framePosY", 0);
                ghostDataPoint.framePosition.z = QuickSaver.ParseAttrFloat(datapoint, "framePosZ", 0);
                ghostDataPoint.interpolated = QuickSaver.ParseAttrBool(datapoint, "interpolated", true);

                ParseGhostEvents(ref ghostDataPoint, datapoint);

                gd.AddGhostData(ghostDataPoint);
            }
        }

        public static void ParseGhostEvents(ref GhostData.GhostDataPoint ghostDataPoint, XmlNode datapoint)
        {
            ghostDataPoint.eventVals = new Dictionary<GhostData.GhostEvent, object>();

            Array values = Enum.GetValues(typeof(GhostData.GhostEvent));

            foreach (GhostData.GhostEvent gevent in values)
            {
                if (datapoint.Attributes[gevent.ToString()] != null)
                {
                    if (gevent == GhostData.GhostEvent.Invalid)
                    {
                        ghostDataPoint.eventVals.Add(gevent, datapoint.Attributes[GhostData.GhostEvent.Invalid.ToString()].Value);
                    }
                    else if (gevent == GhostData.GhostEvent.AnimState)
                    {
                        ghostDataPoint.eventVals.Add(gevent, QuickSaver.ParseAttrEnum<Character.AnimState>(datapoint, gevent.ToString(), Character.AnimState.IDLE));
                    }
                    else if (gevent == GhostData.GhostEvent.SecondaryAnim)
                    {
                        ghostDataPoint.eventVals.Add(gevent, QuickSaver.ParseAttrEnum<Character.SecondaryAnimState>(datapoint, gevent.ToString(), Character.SecondaryAnimState.NONE));
                    }
                    else if (GhostData.GetEventDataType(gevent) == typeof(bool))
                    {
                        ghostDataPoint.eventVals.Add(gevent, QuickSaver.ParseAttrBool(datapoint, gevent.ToString(), false));
                    }
                    else if (GhostData.GetEventDataType(gevent) == typeof(int))
                    {
                        ghostDataPoint.eventVals.Add(gevent, QuickSaver.ParseAttrInt(datapoint, gevent.ToString(), 0));
                    }
                    else if (GhostData.GetEventDataType(gevent) == typeof(Vector3))
                    {
                        float x = QuickSaver.ParseAttrFloat(datapoint, "X", 0);
                        float y = QuickSaver.ParseAttrFloat(datapoint, "Y", 0);
                        float z = QuickSaver.ParseAttrFloat(datapoint, "Z", 0);

                        ghostDataPoint.eventVals.Add(gevent, new Vector3(x, y, z));
                    }
                }
            }
        }

        static void SwitchGhostMode(bool reverse = false)
        {
            int GhostModeLength = Enum.GetNames(typeof(GhostBusterMod.GhostMode)).Length;

            GhostBusterMod.SelectedGhostMode.Value = (GhostBusterMod.GhostMode)((((int)GhostBusterMod.SelectedGhostMode.Value) + (reverse ? -1 : 1)) % GhostModeLength);
            if (GhostBusterMod.SelectedGhostMode.Value < 0)
            {
                GhostBusterMod.SelectedGhostMode.Value = (GhostBusterMod.GhostMode)(GhostModeLength - 1);
            }

            DisplayMessage("Replay Ghost Mode: " + GhostBusterMod.SelectedGhostMode.Value);
        }

        static void DisplayMessage(string message)
        {
            UserMessageManager.Instance.UserMessage(message, 2, GhostBusterMod.MsgPriority.Value, false);
        }
    }

    [HarmonyPatch(typeof(GhostRecorder), nameof(GhostRecorder.SerializeData))]
    static class GhostRecorderSerializeDataPatch
    {
        static void Postfix(GhostRecorder __instance, ref byte[] __result)
        {
            System.Diagnostics.StackFrame caller = (new System.Diagnostics.StackTrace()).GetFrame(2);
            string methodName = caller.GetMethod().Name;

            Debug.Log("methodName2 " + methodName);

            XmlDocument xmlDocument = GameStateUpdatePatch.XMLFromBytes(__result);
            GameStateUpdatePatch.EnhanceXMLData(ref xmlDocument, __instance.ghostData);
            if (methodName == "StoreGhostData")
            {
                __result = Encoding.UTF8.GetBytes(xmlDocument.OuterXml);
            }
            else
            {
                __result = QuickSaver.GetCompressedBytesFromXmlString(xmlDocument.OuterXml);
            }
        }
    }


    [HarmonyPatch(typeof(QuickSaver), nameof(QuickSaver.GetCompressedBytesFromXmlDoc))]
    static class QuickSaverGetCompressedBytesFromXmlDocPatch
    {
        static bool Prefix(QuickSaver __instance, ref XmlDocument doc, out byte[] __result)
        {
            // GameStateUpdatePatch.EnhanceXMLData(doc);

            System.Diagnostics.StackFrame caller = (new System.Diagnostics.StackTrace()).GetFrame(2);
            string methodName = caller.GetMethod().Name;

            Debug.Log("methodName " + methodName);

            if (methodName == "SerializeData" || methodName == "DMD<GhostRecorder::SerializeData>")
            {
                __result = Encoding.UTF8.GetBytes(doc.OuterXml);
                return false;
            }

            __result = null;

            return true;
        }
    }
}