using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Configuration;

[assembly: AssemblyVersion("0.0.0.4")]
[assembly: AssemblyInformationalVersion("0.0.0.4")]

namespace GhostBuster
{
    [BepInPlugin("GhostBuster", "GhostBuster", "0.0.0.4")]
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
        public static ConfigEntry<bool> GhostOutfis;
        public static ConfigEntry<float> GhostAlpha;
        public static ConfigEntry<bool> InputReplay;

        public static ConfigEntry<UserMessageManager.UserMsgPriority> MsgPriority;

        public static ConfigEntry<KeyCode> ToggleGhostsKey;
        public static ConfigEntry<KeyCode> SwitchGhostModeKey;
        public static ConfigEntry<KeyCode> StoreGhostDataKey;
        public static ConfigEntry<KeyCode> LoadGhostDataKey;
        public static ConfigEntry<KeyCode> ToggleGhostTextKey;
        public static ConfigEntry<KeyCode> ClearAllGhostReplaysKey;



        public static Dictionary<int, Character> GhostFraid = new Dictionary<int, Character>();
        public static Dictionary<string, List<GhostData>> Replays = new Dictionary<string, List<GhostData>>();
        public static Dictionary<string, List<GhostData>> StoredReplays = new Dictionary<string, List<GhostData>>();

        void Awake()
        {
            Debug.Log("Let the Ghosts replay");
            GameSettings.GetInstance().DebugChallengeGhosts = true;
            new Harmony("GhostBuster").PatchAll();

            ShowGhosts = Config.Bind("General", "ShowGhosts", false);
            MaxGhostNumber = Config.Bind("General", "MaxGhostNumber", 10, "Maximum number of ghost replays that are kept and shown in Ghost Mode ALL");
            SelectedGhostMode = Config.Bind("General", "SelectedGhostMode", GhostMode.Fastest, "The selected Replay Ghost Mode");
            ClearStoredGhosts = Config.Bind("General", "ClearStoredGhosts", false, "Remove stored ghost replays when new one is loaded");
            ShowGhostText = Config.Bind("General", "ShowGhostText", true, "Display text box above ghosts with name and time");
            GhostOutfis = Config.Bind("General", "GhostOutfis", true, "Show outfits for ghost replays");
            GhostAlpha = Config.Bind("General", "GhostAlpha", 0.45f, "Set the alpha/transparency of ghosts. [1: solid, ..., 0: invisible]");
            InputReplay = Config.Bind("General", "InputReplay", false, "Record and replay from user input");
            GameSettings.GetInstance().ghostAlpha = GhostAlpha.Value;

            MsgPriority = Config.Bind("GUI", "MsgPriority", UserMessageManager.UserMsgPriority.hi, "Display GUI messages: hi = show in middle, lo = show bottom right");

            ToggleGhostsKey = Config.Bind("INPUT", "ToggleGhostsKey", KeyCode.G, "Keybinding: Toggle Ghosts on or off");
            LoadGhostDataKey = Config.Bind("INPUT", "LoadGhostDataKey", KeyCode.L, "Keybinding: Toggle Ghost Modes");
            StoreGhostDataKey = Config.Bind("INPUT", "StoreGhostDataKey", KeyCode.K, "Keybinding: Load stored data from clipboard");
            SwitchGhostModeKey = Config.Bind("INPUT", "SwitchGhostModeKey", KeyCode.H, "Keybinding: Store ghost data in clipboard");
            ToggleGhostTextKey = Config.Bind("INPUT", "ToggleGhostTextKey", KeyCode.N, "Keybinding: Toggle text above ghosts");
            ClearAllGhostReplaysKey = Config.Bind("INPUT", "ClearAllGhostReplays", KeyCode.X, "Keybinding: Clear all GhostReplay data");
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
            character.SetSprites(data.animal);
            // outfits potentially contamintated by EvenMorePlayers mod clean them up
            var clean_outfits = new int[6];
            for (int i = 0; i < clean_outfits.Length; i++)
            {
                if (i < data.outfits.Length)
                {
                    clean_outfits[i] = data.outfits[i];
                }
                else
                {
                    clean_outfits[i] = -1;
                }

                Debug.Log("clean_outfits[i] " + clean_outfits[i]);

            }
            data.outfits = clean_outfits;
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

        public static void ClearAllReplayGhostsData()
        {
            ClearReplayGhosts();
            Replays = new Dictionary<string, List<GhostData>>();
            StoredReplays = new Dictionary<string, List<GhostData>>();
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
                if (InputReplay.Value)
                {
                    __instance.Enable(false);
                    __instance.replayData = data;
                    __instance.isReplay = true;
                    __instance.transform.position = data.GetDataForTime(0f, false).position;
                    __instance.SetOutfitsFromArray(data.Outfits);
                    __instance.nameTag.setNameBoxText(data.PlayerName, __instance);
                    return false;
                }
                else
                {
                    __instance.SetPlayerPlayerColliders(false, Character.ColliderStates.Dead);
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.SetNonLocalColliderMode))]
        static class CharacterReplaySetNonLocalColliderModePatch
        {
            static void Prefix(Character __instance, ref Character.NonLocalColliderMode mode)
            {
                if (InputReplay.Value && __instance.replayData != null)
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

        static void ReplayDataPoint(Character __instance, GhostData.GhostDataPoint datapoint)
        {
            foreach (KeyValuePair<GhostData.GhostEvent, object> keyValuePair in datapoint.eventVals)
            {
                if (keyValuePair.Key == GhostData.GhostEvent.Invalid)
                {
                    string inputs_string = (keyValuePair.Value as string);

                    if (inputs_string.Contains(":"))
                    {
                        var split_input = inputs_string.Split(':');
                        //int player, InputKey key, float valuef, bool valueb, bool changed OrthoUp:1:True:False
                        __instance.ReceiveEvent(new InputEvent(-1, (InputEvent.InputKey)System.Enum.Parse(typeof(InputEvent.InputKey), split_input[0]), float.Parse(split_input[1]), bool.Parse(split_input[2]), bool.Parse(split_input[3])));
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.replayUpdate))]
        static class CharacterReplayUpdatePatch
        {
            static bool Prefix(Character __instance)
            {
                if (InputReplay.Value)
                {
                    if (__instance.replayData != null && !__instance.replayPaused)
                    {
                        float num = Time.realtimeSinceStartup - __instance.replayStartTime;

                        for (int i = __instance.replayData.lastIndex; i < __instance.replayData.dataPoints.Count; i++)
                        {
                            if (__instance.replayData.dataPoints[i].timestamp > num)
                            {
                                __instance.replayData.lastIndex = i;
                                break;
                            }
                            ReplayDataPoint(__instance, __instance.replayData.dataPoints[i]);
                        }
                        /*

foreach (KeyValuePair<GhostData.GhostEvent, object> keyValuePair in __instance.currentReplayFrame)
{
if (keyValuePair.Key == GhostData.GhostEvent.Invalid)
{
string inputs_string = (keyValuePair.Value as string);
if (inputs_string.Contains("|"))

var split_input = inputs_string.Split('|');
__instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Up, float.Parse(split_input[0]), true));
__instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Down, float.Parse(split_input[1]), true));
__instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Left, float.Parse(split_input[2]), true));
__instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Right, float.Parse(split_input[3]), true));
__instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Jump, float.Parse(split_input[4]), split_input[4] == "1"));
__instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Suicide, float.Parse(split_input[5]), split_input[5] == "1"));
__instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Sprint, float.Parse(split_input[6]), split_input[6] == "1"));
__instance.ReceiveEvent(new InputEvent(-1, InputEvent.InputKey.Inventory, float.Parse(split_input[7]), split_input[7] == "1"));
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

__instance.idleTimer = 0f;


}
}
} */
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

                if (GhostOutfis.Value)
                {
                    __instance.SetOutfitsFromArray(__instance.replayData.outfits);
                }
                else
                {
                    __instance.SetOutfitsFromArray(new int[] { -1, -1, -1, -1, -1, -1 });
                }

                if (InputReplay.Value)
                {
                    __instance.replayData.lastIndex = 0;
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

        [HarmonyPatch(typeof(ChallengeControl), nameof(ChallengeControl.SetupStart))]
        static class ChallengeControlSetupStartPatch
        {
            static void Prefix(ChallengeControl __instance)
            {
                GhostFraid = new Dictionary<int, Character>();
            }

            static void Postfix(ChallengeControl __instance)
            {
                foreach (GamePlayer gamePlayer2 in __instance.PlayerQueue)
                {
                    if (!gamePlayer2.CharacterInstance.ReplayRecorder)
                    {
                        GhostRecorder component = new GameObject("Ghost recorder - " + gamePlayer2.PickedAnimal.ToString(), new System.Type[] { typeof(GhostRecorder) }).GetComponent<GhostRecorder>();
                        component.TrackCharacter(gamePlayer2.CharacterInstance);
                        gamePlayer2.CharacterInstance.ReplayRecorder = component;
                        Character character = UnityEngine.Object.Instantiate<Character>(__instance.CharacterPrefab);
                        character.gameObject.name = gamePlayer2.PickedAnimal.ToString() + " (ghost)";
                        character.NetworkCharacterSprite = gamePlayer2.PickedAnimal;
                        character.SetOutfitsFromArray(gamePlayer2.characterOutfitsList);
                        character.NetworknetworkNumber = 0;
                        character.NetworklocalNumber = 0;
                        character.isReplay = true;
                        character.Disable(true);
                        character.NetworkFindPlayerOnSpawn = false;
                        character.Networkpicked = true;
                        gamePlayer2.CharacterInstance.ReplayCharacter = character;
                    }
                }
            }
        }


        [HarmonyPatch(typeof(ChallengeControl), nameof(ChallengeControl.DoPlayMode))]
        static class ChallengeControlDoPlayModePatch
        {
            static void Postfix(ChallengeControl __instance)
            {
                if (__instance.runStarted)
                {

                    if (GameSettings.GetInstance().DebugChallengeGhosts)
                    {
                        foreach (GamePlayer gamePlayer4 in __instance.PlayerQueue)
                        {
                            GhostRecorder replayRecorder = gamePlayer4.CharacterInstance.ReplayRecorder;
                            replayRecorder.PauseTracking();
                            replayRecorder.UpdateCharacter();
                            gamePlayer4.CharacterInstance.ReplayCharacter.SetupReplay(replayRecorder.CurrentGhostData.GetCopy());
                        }
                    }
                }
            }
        }


        [HarmonyPatch(typeof(ChallengeControl), nameof(ChallengeControl.startRun))]
        static class ChallengeControlStartRunPatch
        {
            static void Postfix(ChallengeControl __instance)
            {
                if (GameSettings.GetInstance().DebugChallengeGhosts)
                {
                    foreach (GamePlayer gamePlayer2 in __instance.PlayerQueue)
                    {
                        GhostRecorder replayRecorder = gamePlayer2.CharacterInstance.ReplayRecorder;
                        Character replayCharacter = gamePlayer2.CharacterInstance.ReplayCharacter;
                        if (!replayRecorder.IsTracking)
                        {
                            replayRecorder.StartTracking(gamePlayer2.CharacterInstance);
                        }
                        if (replayRecorder.IsPaused)
                        {
                            replayRecorder.ResumeTracking();
                            replayRecorder.Reset();
                        }
                        if (replayCharacter.HasReplayData)
                        {
                            replayCharacter.Enable(false);
                            replayCharacter.StartReplay();
                        }
                    }
                }

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

        [HarmonyPatch(typeof(Character), nameof(Character.ReceiveEvent))]
        static class CharacterReceiveEventPatch
        {
            static void Prefix(Character __instance, InputEvent e)
            {
                if (__instance.ReplayRecorder && __instance.ReplayRecorder.IsTracking)
                {
                    GhostData.GhostDataPoint newDataPoint = new GhostData.GhostDataPoint(Time.realtimeSinceStartup - __instance.ReplayRecorder.timeOffset, __instance.transform.position);
                    newDataPoint.eventVals.Add(GhostData.GhostEvent.Invalid, e.Key + ":" + e.Valuef + ":" + e.Valueb + ":" + e.Changed);
                    newDataPoint.eventVals.Add(GhostData.GhostEvent.AnimState, __instance.CurrentAnim);
                    newDataPoint.eventVals.Add(GhostData.GhostEvent.SecondaryAnim, __instance.SecondaryAnim);
                    newDataPoint.eventVals.Add(GhostData.GhostEvent.Flipped, __instance.FlipSpriteX);
                    __instance.ReplayRecorder.ghostData.AddGhostData(newDataPoint);
                }
            }
        }

        [HarmonyPatch(typeof(ChallengeControl), nameof(ChallengeControl.handleEvent))]
        static class ChallengeControlHandleEventPatch
        {
            static void PostFix(ChallengeControl __instance, GameEvent.GameEvent e)
            {
                if (e.GetType() == typeof(GameEvent.PauseEvent))
                {
                    GameEvent.PauseEvent pvent = (e as GameEvent.PauseEvent);
                    if (GameSettings.GetInstance().DebugChallengeGhosts)
                    {
                        if ((e as GameEvent.PauseEvent).Paused)
                        {
                            using (Queue<GamePlayer>.Enumerator enumerator = __instance.PlayerQueue.GetEnumerator())
                            {
                                while (enumerator.MoveNext())
                                {
                                    GamePlayer gamePlayer3 = enumerator.Current;
                                    Character characterInstance = gamePlayer3.CharacterInstance;
                                    if (characterInstance.ReplayRecorder.IsTracking)
                                    {
                                        characterInstance.ReplayRecorder.PauseTracking();
                                    }
                                    if (characterInstance.isReplaying)
                                    {
                                        characterInstance.PauseReplay();
                                    }
                                }
                                return;
                            }
                        }
                        foreach (GamePlayer gamePlayer4 in __instance.PlayerQueue)
                        {
                            Character characterInstance2 = gamePlayer4.CharacterInstance;
                            if (characterInstance2.ReplayRecorder.IsTracking && characterInstance2.ReplayRecorder.IsPaused)
                            {
                                characterInstance2.ReplayRecorder.ResumeTracking();
                            }
                            if (characterInstance2.isReplaying && characterInstance2.replayPaused)
                            {
                                characterInstance2.ResumeReplay();
                            }
                        }
                    }
                }
            }
        }
    }
}