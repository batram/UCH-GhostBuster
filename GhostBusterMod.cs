using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Configuration;

[assembly: AssemblyVersion("0.0.0.2")]
[assembly: AssemblyInformationalVersion("0.0.0.2")]

namespace GhostBuster
{
    [BepInPlugin("GhostBuster", "GhostBuster", "0.0.0.2")]
    public class GhostBusterMod : BaseUnityPlugin
    {
        public enum GhostMode
        {
            Fastest,
            All,
            Last,
            Stored
        }


        public static ConfigEntry<bool> ShowGhosts;
        public static ConfigEntry<GhostMode> SelectedGhostMode;
        public static ConfigEntry<int> MaxGhostNumber;
        public static ConfigEntry<bool> ClearStoredGhosts;
        public static ConfigEntry<bool> ShowGhostText;

        public static ConfigEntry<UserMessageManager.UserMsgPriority> MsgPriority;

        public static ConfigEntry<KeyCode> ToggleGhostsKey;
        public static ConfigEntry<KeyCode> SwitchGhostModeKey;
        public static ConfigEntry<KeyCode> StoreGhostDataKey;
        public static ConfigEntry<KeyCode> LoadGhostDataKey;
        public static ConfigEntry<KeyCode> ToggleGhostTextKey;


        public static bool InputReplay = false;

        public static Dictionary<int, Character> GhostFraid = new Dictionary<int, Character>();
        public static Dictionary<string, List<GhostData>> Replays = new Dictionary<string, List<GhostData>>();
        public static Dictionary<string, List<GhostData>> StoredReplays = new Dictionary<string, List<GhostData>>();

        void Awake()
        {
            Debug.Log("Let the Ghosts replay");
            GameSettings.GetInstance().DebugChallengeGhosts = true;
            new Harmony("GhostBuster").PatchAll();
            GameSettings.GetInstance().DefaultGameMode = GameState.GameMode.CHALLENGE;

            ShowGhosts = Config.Bind("General", "ShowGhosts", false);
            MaxGhostNumber = Config.Bind("General", "MaxGhostNumber", 10, "Maximum number of ghost replays that are kept and shown in Ghost Mode ALL");
            SelectedGhostMode = Config.Bind("General", "SelectedGhostMode", GhostMode.Fastest, "The selected Replay Ghost Mode");
            ClearStoredGhosts = Config.Bind("General", "ClearStoredGhosts", false, "Remove stored ghost replays when new one is loaded");
            ShowGhostText = Config.Bind("General", "ShowGhostText", true, "Display text box above ghosts with name and time");

            MsgPriority = Config.Bind("GUI", "MsgPriority", UserMessageManager.UserMsgPriority.hi, "Display GUI messages: hi = show in middle, lo = show bottom right");

            ToggleGhostsKey = Config.Bind("INPUT", "ToggleGhostsKey", KeyCode.G, "Keybinding: Toggle Ghosts on or off");
            LoadGhostDataKey = Config.Bind("INPUT", "LoadGhostDataKey", KeyCode.L, "Keybinding: Toggle Ghost Modes");
            StoreGhostDataKey = Config.Bind("INPUT", "StoreGhostDataKey", KeyCode.K, "Keybinding: Load stored data from clipboard");
            SwitchGhostModeKey = Config.Bind("INPUT", "SwitchGhostModeKey", KeyCode.H, "Keybinding: Store ghost data in clipboard");
            ToggleGhostTextKey = Config.Bind("INPUT", "ToggleGhostTextKey", KeyCode.N, "Keybinding: Toggle text above ghosts");
        }


        static float GetWinTime(GhostData data)
        {
            foreach (var datap in data.dataPoints)
            {
                if (datap.eventVals.ContainsKey(GhostData.GhostEvent.AnimState))
                {
                    var anim = datap.eventVals[GhostData.GhostEvent.AnimState];
                    if (anim != null && ((Character.AnimState)anim) == Character.AnimState.WIN)
                    {
                        return datap.timestamp;
                    }
                }
            }
            return -1;
        }

        static public T FindObject<T>(string name) where T : Object
        {
            T pla = null;
            foreach (var o in Resources.FindObjectsOfTypeAll<T>())
            {
                if (o.name == name)
                {
                    pla = o;
                    break;
                }
            }
            return pla;
        }

        static Character AddReplayGhost(GhostData data)
        {
            Character character = Object.Instantiate<Character>(FindObject<Character>("NotAMeatboy"));
            character.CharacterSprite = data.animal;
            int hash = Animator.StringToHash(data.printData());
            GhostFraid.Add(hash, character);
            character.gameObject.name = data.animal.ToString() + " (ghost)";
            character.NetworkCharacterSprite = data.animal;
            character.SetOutfitsFromArray(data.outfits);
            character.NetworknetworkNumber = 0;
            character.NetworklocalNumber = 0;
            character.isReplay = true;
            character.NetworkFindPlayerOnSpawn = false;
            character.Networkpicked = true;
            character.SetupReplay(data);
            character.Enable(false);
            character.StartReplay();

            return character;
        }

        public static void ClearReplayGhosts()
        {
            foreach (Character c in GhostFraid.Values)
            {
                if (c && c.gameObject)
                {
                    Destroy(c.gameObject);
                }
            }
            GhostFraid = new Dictionary<int, Character>();
        }

        public static void LookUpGhostId(string ghostId)
        {
            /*
            Action<GameSparks.Api.Responses.GetUploadedResponse> callback = delegate (GameSparks.Api.Responses.GetUploadedResponse response)
            {
                Debug.Log("Upload?: " + response.Url);
                Debug.Log("Upload? size: " + response.Size);
            };

            new GameSparks.Api.Requests.GetUploadedRequest().SetUploadId(ghostId).Send(callback);*/
        }

        public static string GetCurrentLevelName()
        {
            string name = GameState.GetInstance().currentSnapshotInfo.snapshotCode;
            if (name == "")
            {
                name = "local_" + GameState.GetInstance().currentSnapshotInfo.snapshotName;
            }

            return name;
        }
        
        public static IOrderedEnumerable<GhostData> SortGhostByTime(List<GhostData> replays)
        {
            return replays.OrderBy(d => d.lastTime != 0 ? d.lastTime : float.PositiveInfinity);
        }

        [HarmonyPatch(typeof(Character), nameof(Character.SetupReplay))]
        static class CharacterSetupReplayPatch
        {
            static bool Prefix(Character __instance, GhostData data)
            {
                if (!GhostBusterMod.GhostFraid.ContainsValue(__instance))
                {
                    return false;
                }
                if (InputReplay)
                {
                    __instance.Enable(false);
                    __instance.replayData = data;
                    __instance.isReplay = true;
                    __instance.transform.position = data.GetDataForTime(0f, false).position;
                    __instance.SetOutfitsFromArray(data.Outfits);
                    __instance.nameTag.setNameBoxText(data.PlayerName, __instance);

                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.SetNonLocalColliderMode))]
        static class CharacterReplaySetNonLocalColliderModePatch
        {
            static void Prefix(Character __instance, ref Character.NonLocalColliderMode mode)
            {
                if (InputReplay && __instance.replayData != null)
                {
                    mode = Character.NonLocalColliderMode.PickedLocal;
                }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.Update))]
        static class CharacterUpdatePatch
        {
            static void Postfix(Character __instance)
            {
                if (__instance.replayData != null)
                {
                    string time = "notime";
                    if (__instance.replayData.lastTime != float.PositiveInfinity && __instance.replayData.lastTime > 0)
                    {
                        time = HighscoreDisplayEntry.GetTimeString(__instance.replayData.lastTime);
                        if (time.StartsWith("00:"))
                        {
                            time = time.Substring(3);
                        }
                    }
                    else
                    {
                        float parsedTime = GetWinTime(__instance.replayData);
                        if (parsedTime > 0)
                        {
                            time = HighscoreDisplayEntry.GetTimeString(parsedTime);
                            if (time.StartsWith("00:"))
                            {
                                time = time.Substring(3);
                            }
                            time = "~" + time;
                        }
                    }
                    __instance.nameTag.setNameBoxText(__instance.replayData.PlayerName + " (" + time + ")", __instance);
                    __instance.nameTag.currentAlpha = GhostBusterMod.ShowGhostText.Value ? 1f : 0f;
                    __instance.nameTag.nameBox.color = Color.white;
                }
            }
        }

        [HarmonyPatch]
        static class CharacterReplayUpdateInvalidNowValid
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Character), nameof(Character.replayUpdate));
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> e)
            {
                var done = false;
                var start = false;

                foreach (var inst in e)
                {
                    if (inst.opcode == OpCodes.Ldstr)
                    {
                        Debug.Log("OpCodes.Ldstr " + inst.operand.ToString());
                    }
                    if (!done && inst.opcode == OpCodes.Ldstr && inst.operand.ToString() == "Playing back an invalid keyframe!")
                    {
                        start = true;
                        inst.opcode = OpCodes.Nop;
                        inst.operand = null;
                    }
                    else if (start)
                    {
                        start = false;
                        // NOP out LogError
                        inst.opcode = OpCodes.Nop;
                        done = true;
                    }
                    yield return inst;
                }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.replayUpdate))]
        static class CharacterReplayUpdatePatch
        {
            static bool Prefix(Character __instance)
            {
                if (InputReplay)
                {
                    if (__instance.replayData != null && !__instance.replayPaused)
                    {
                        float num = Time.realtimeSinceStartup - __instance.replayStartTime;

                        __instance.currentReplayFrame = __instance.replayData.GetDataForTime(num, true);

                        foreach (KeyValuePair<GhostData.GhostEvent, object> keyValuePair in __instance.currentReplayFrame)
                        {
                            if (keyValuePair.Key == GhostData.GhostEvent.Invalid)
                            {
                                string inputs_string = (keyValuePair.Value as string);
                                if (inputs_string.Contains("|"))
                                {
                                    var split_input = inputs_string.Split('|');

                                    __instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Up, float.Parse(split_input[0]), true));
                                    __instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Down, float.Parse(split_input[1]), true));
                                    __instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Left, float.Parse(split_input[2]), true));
                                    __instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Right, float.Parse(split_input[3]), true));
                                    __instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Jump, float.Parse(split_input[4]), split_input[4] == "1"));
                                    __instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Suicide, float.Parse(split_input[5]), split_input[5] == "1"));
                                    __instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Sprint, float.Parse(split_input[6]), split_input[6] == "1"));
                                    __instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Inventory, float.Parse(split_input[7]), split_input[7] == "1"));
                                    /*
                                    __instance.up = float.Parse(split_input[0]);
                                    __instance.down = float.Parse(split_input[1]);
                                    __instance.left = float.Parse(split_input[2]);
                                    __instance.leftInput = float.Parse(split_input[2]);
                                    __instance.right = float.Parse(split_input[3]);
                                    __instance.rightInput = float.Parse(split_input[3]);
                                    __instance.jump = split_input[4] == "1";
                                    __instance.suicide = split_input[5] == "1";
                                    __instance.sprint = split_input[6] == "1";
                                    __instance.dance = split_input[7] == "1";
                                    */
                                    __instance.idleTimer = 0f;

                                }
                            }
                        }
                        __instance.fullUpdate();
                    }
                    return false;
                }
                /*
                else if (__instance.replayData != null && !__instance.replayPaused)
                {
                    foreach (var dp in __instance.replayData.dataPoints)
                    {
                        if (dp.eventVals.ContainsKey(GhostData.GhostEvent.Invalid))
                        {
                            dp.eventVals.Remove(GhostData.GhostEvent.Invalid);
                        }
                    }
                }*/
                return true;
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.StartReplay))]
        static class CharacterStartReplayPatch
        {
            static bool Prefix(Character __instance)
            {
                __instance.isGhost = true;
                if (InputReplay)
                {
                    __instance.transform.position = __instance.replayData.GetDataForTime(0f, false).position;
                    __instance.isReplaying = false;
                    __instance.replayStartTime = Time.realtimeSinceStartup;
                    return false;
                }
                return true;
            }
        }

        //[HarmonyPatch(typeof(GhostRecorder), nameof(GhostRecorder.FixedUpdate))]
        [HarmonyPatch(typeof(GhostRecorder), nameof(GhostRecorder.Update))]
        static class GhostRecorderUpdatePatch
        {
            static bool Prefix(GhostRecorder __instance, out object[] __state)
            {
                if (__instance.previousValues != null && __instance.ghostData != null)
                {
                    if (!__instance.previousValues.ContainsKey(GhostData.GhostEvent.Invalid))
                    {
                        __instance.previousValues[GhostData.GhostEvent.Invalid] = "";
                    }
                    __state = new object[] { __instance.ghostData.dataPoints.Count, __instance.previousValues[GhostData.GhostEvent.Invalid] };
                    __instance.previousValues.Remove(GhostData.GhostEvent.Invalid);

                    var anim = __instance.previousValues[GhostData.GhostEvent.AnimState];

                    if (anim != null && ((Character.AnimState)anim) == Character.AnimState.WIN && __instance.ghostData.lastTime == 0)
                    {
                        __instance.ghostData.lastTime = __instance.ghostData.dataPoints[__instance.ghostData.dataPoints.Count - 1].timestamp;
                        //__instance.tracking = false;
                        // return false;
                    }
                }
                else
                {
                    __state = new object[] { 0, "" };
                }
                return true;
            }


            static void Postfix(GhostRecorder __instance, object[] __state)
            {
                if (__instance.tracking && !__instance.paused && __instance.trackedChar != null && __instance.ghostData.lastTime == 0)
                {
                    Character tc = __instance.trackedChar;
                    string inputs = tc.up + "|" +
                                    tc.down + "|" +
                                    tc.leftInput + "|" +
                                    tc.rightInput + "|" +
                                    (tc.jump ? 1 : 0) + "|" +
                                    (tc.suicide ? 1 : 0) + "|" +
                                    (tc.sprint ? 1 : 0) + "|" +
                                    (tc.dance ? 1 : 0);

                    //Debug.Log("__state[1] as string) != inputs || " + (__state[1] as string) + "!=" + inputs);

                    if ((__state[1] as string) != inputs)
                    {
                        if (((int)__state[0]) != __instance.ghostData.dataPoints.Count)
                        {
                            //Edit entry if already set
                            __instance.ghostData.dataPoints[__instance.ghostData.dataPoints.Count - 1].AddData(GhostData.GhostEvent.Invalid, inputs);

                        }
                        else
                        {
                            //Add new entry if none was added previously
                            __instance.nextDataPoint.AddData(GhostData.GhostEvent.Invalid, inputs);
                            __instance.ghostData.AddGhostData(new GhostData.GhostDataPoint(__instance.nextDataPoint));
                        }
                    }
                    __instance.previousValues[GhostData.GhostEvent.Invalid] = inputs;
                }

            }
        }

        [HarmonyPatch(typeof(ChallengeScoreboard), nameof(ChallengeScoreboard.ShowNewResult))]
        static class ChallengeScoreboardShowNewResultPatch
        {
            static void Prefix(ChallengeScoreboard __instance)
            {
                foreach (GamePlayer player in __instance.challengeController.PlayerQueue)
                {
                    GhostRecorder ghostRecorder = player?.CharacterInstance?.ReplayRecorder;
                    if (ghostRecorder != null)
                    {
                        string name = GetCurrentLevelName();

                        GhostData gd_copy = ghostRecorder.ghostData.GetCopy();
                        if (__instance.challengeController.playerEndTimes.ContainsKey(player))
                        {
                            gd_copy.lastTime = __instance.challengeController.playerEndTimes[player];
                        }

                        Debug.Log("add replay for level: " + name);
                        if (!Replays.ContainsKey(name))
                        {
                            Replays.Add(name, new List<GhostData> { gd_copy });
                        }
                        else
                        {
                            Replays[name].Add(gd_copy);
                        }

                        var sortedDict = Replays[name].ToList();

                        if (Replays[name].Count > MaxGhostNumber.Value)
                        {
                            // Remove slowest replay
                            var sorted = SortGhostByTime(Replays[name]);
                            var ByeByeGhost = sorted.Last();
                            if (SelectedGhostMode.Value == GhostMode.Last)
                            {
                                // Remove oldest replay instead
                                ByeByeGhost = Replays[name][0];
                            }

                            int hash = Animator.StringToHash(ByeByeGhost.printData());
                            if (GhostFraid.ContainsKey(hash))
                            {
                                Destroy(GhostFraid[hash].gameObject);
                                GhostFraid.Remove(hash);
                            }
                            Replays[name].Remove(ByeByeGhost);
                        }
                    }
                }
            }
        }



        [HarmonyPatch(typeof(ChallengeScoreboard), nameof(ChallengeScoreboard.uploadRoundTime))]
        static class ChallengeScoreboarduploadRoundTimePatch
        {
            static void Prefix(List<string> playerIds, List<string> ghostUploadIDs)
            {
                Debug.Log("ghostUploadIDs " + playerIds.Join<string>() + " " + ghostUploadIDs.Join<string>());
            }
        }


        [HarmonyPatch(typeof(ChallengeControl), nameof(ChallengeControl.SetupStart))]
        static class ChallengeControlSetupStartPatch
        {
            static void Prefix(ChallengeControl __instance)
            {
                GhostFraid = new Dictionary<int, Character>();
            }
        }


        [HarmonyPatch(typeof(ChallengeControl), nameof(ChallengeControl.startRun))]
        static class ChallengeControlDoPlayModePatch
        {
            static void Postfix(ChallengeControl __instance)
            {
                string name = GetCurrentLevelName();

                if (ShowGhosts.Value &&
                    ((SelectedGhostMode.Value != GhostMode.Stored && Replays.ContainsKey(name))
                      || (SelectedGhostMode.Value == GhostMode.Stored && StoredReplays.ContainsKey(name))))
                {
                    switch (SelectedGhostMode.Value)
                    {

                        case GhostMode.All:
                            foreach (GhostData gd in Replays[name])
                            {
                                int hash = Animator.StringToHash(gd.printData());
                                if (GhostFraid.ContainsKey(hash))
                                {
                                    Character ghost = GhostFraid[hash];
                                    ghost.StartReplay();
                                }
                                else
                                {
                                    AddReplayGhost(gd);
                                }
                            }
                            break;
                        case GhostMode.Fastest:
                            ClearReplayGhosts();
                            AddReplayGhost(SortGhostByTime(Replays[name]).First());
                            break;
                        case GhostMode.Last:
                            ClearReplayGhosts();
                            AddReplayGhost(Replays[name][Replays[name].Count - 1]);
                            break;
                        case GhostMode.Stored:
                            if (StoredReplays.Count != 0)
                            {
                                foreach (GhostData gd in StoredReplays[name])
                                {
                                    int hash = Animator.StringToHash(gd.printData());
                                    if (GhostFraid.ContainsKey(hash) && GhostFraid[hash])
                                    {
                                        Character ghost = GhostFraid[hash];
                                        ghost.StartReplay();
                                    }
                                    else
                                    {
                                        AddReplayGhost(gd);
                                    }
                                }
                            }
                            else
                            {
                                ClearReplayGhosts();
                                return;
                            }
                            break;
                    }
                }
                else
                {
                    ClearReplayGhosts();
                }
            }
        }
    }
}