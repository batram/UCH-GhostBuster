using System.Linq;
using HarmonyLib;

namespace GhostBuster.Patches
{
    [HarmonyPatch(typeof(DancingMoverCharacterTrigger), nameof(DancingMoverCharacterTrigger.ProcessLocalCharacterState))]
    static class DancingMoverIgnoreReplayCharacterPatch
    {
        static bool Prefix(Character character)
        {
            return character == null || !character.isReplay;
        }
    }

    [HarmonyPatch(typeof(DancingMoverCharacterTrigger), nameof(DancingMoverCharacterTrigger.ProcessServerMessage))]
    static class DancingMoverIgnoreAggregateServerMessagePatch
    {
        static bool Prefix(MsgPlatformDancing message)
        {
            if (message == null || message.PlayerNumber != 0)
            {
                return true;
            }

            // UpdatePlatformStateAndBroadcast sends an aggregate state without a
            // PlayerNumber, leaving it at the reserved value 0. On a host, that
            // broadcast loops back through ProcessServerMessage and looks like a
            // player update. GhostBuster's replay character also uses network 0,
            // which prevents the game's disconnected-player cleanup from removing
            // the resulting stale dancing state.
            return false;
        }
    }

    [HarmonyPatch(typeof(DancingMoverCharacterTrigger), nameof(DancingMoverCharacterTrigger.CleanUpDisconnectedCharacters))]
    static class DancingMoverReplayAwareCleanupPatch
    {
        static void Postfix(DancingMoverCharacterTrigger __instance)
        {
            int[] staleNetworkNumbers = __instance.activeDancerNetworkNumbers
                .Concat(__instance.activeOnPlatformNetworkNumbers)
                .Distinct()
                .Where(networkNumber => !Character.AllCharacters.Any(character =>
                    character != null && !character.isReplay && character.networkNumber == networkNumber))
                .ToArray();

            bool changed = false;
            foreach (int networkNumber in staleNetworkNumbers)
            {
                changed |= __instance.activeDancerNetworkNumbers.Remove(networkNumber);
                changed |= __instance.activeOnPlatformNetworkNumbers.Remove(networkNumber);
            }

            if (!changed)
            {
                return;
            }

            __instance.UpdatePlatformStateAndBroadcast();
        }
    }
}
