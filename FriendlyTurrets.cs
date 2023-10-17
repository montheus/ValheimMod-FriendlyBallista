using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace FriendlyTurrets
{
    [BepInPlugin("ByteArtificer.FriendlyTurrets", "Friendly Ballista", "1.0.3")] 
    [BepInProcess("valheim.exe")]
    public class FriendlyTurrets : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("ByteArtificer.FriendlyTurrets");
        public static ManualLogSource logger;
        public static bool debugLogging = false;

        private static ConfigEntry<bool> attackPassivesConfig;
        private static ConfigEntry<bool> attackPlayersConfig;
        private static ConfigEntry<bool> attackTamedConfig;
        private static ConfigEntry<bool> attackBabiesConfig;

        public void Awake()
        {
            logger = Logger;
            harmony.PatchAll();


            attackPassivesConfig = Config.Bind("General",      // The section under which the option is shown
                                "attackPassivesConfig",  // The key of the configuration option in the configuration file
                                false, // The default value
                                "True means passive creatures get shot at"); // Description of the option to show in the config file

                        attackPlayersConfig = Config.Bind("General",
                                                "attackPlayersConfig",
                                                false,
                                                "True means Players get shot at");
            attackTamedConfig = Config.Bind("General",
                                         "attackTamedConfig",
                                         false,
                                         "True means tamed creatures get shot at");

            attackBabiesConfig = Config.Bind("General",
                                  "attackBabiesConfig",
                                  false,
            "True means baby creatures get shot at");
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.UpdateTarget))]
        public static class Turret_UpdateTarget_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                logger.LogMessage("Friendly turrets patching...");
                var foundClosestCreatureCall = false;

                var codes = new List<CodeInstruction>(instructions);
                for (var j = 0; j < codes.Count; j++)
                {
                    var code = codes[j];

                    if (code.opcode != OpCodes.Call)
                        continue;

                    var methodInfo = code.operand as MethodInfo;

                    if (methodInfo == null)
                        continue;

                    if (methodInfo.Name == nameof(BaseAI.FindClosestCreature))
                    {
                        logger.LogMessage($"Patching Successful! {j}");
                        foundClosestCreatureCall = true;
                        codes[j] = new CodeInstruction(OpCodes.Call, typeof(FriendlyTurrets).GetMethod(nameof(FriendlyTurrets.FindClosestPlayerUnfriendlyCreature)));
                        break;
                    }
                }

                if (!foundClosestCreatureCall)
                    logger.LogWarning("Couldn't find IL call to replace");

                return codes.AsEnumerable();
            }
        }


        public static Character FindClosestPlayerUnfriendlyCreature(Transform me, Vector3 eyePoint, float hearRange, float viewRange, float viewAngle, bool alerted, bool mistVision, bool passiveAggresive, bool includePlayers, bool includeTamed, List<Character> onlyTargets = null)
        {


            List<Character> allCharacters = Character.GetAllCharacters();
            Character character = null;
            float num = 99999f;
            foreach (Character item in allCharacters)
            {
                BaseAI baseAI = item.GetBaseAI();


                if (FriendlyTurrets.debugLogging) logger.LogMessage(
                    $"{item}  FriendlyTurrets.attackPlayersConfig.Value=" + FriendlyTurrets.attackPlayersConfig.Value +
                    " FriendlyTurrets.attackPassivesConfig.Value=" + FriendlyTurrets.attackPassivesConfig.Value
                    + " FriendlyTurrets.attackTamedConfig.Value=" + FriendlyTurrets.attackTamedConfig.Value
                    + " FriendlyTurrets.attackBabiesConfig.Value=" + FriendlyTurrets.attackBabiesConfig.Value
                    + " baseAI=" + baseAI +
                    " (!attackPlayers && item is Player)=" + (!FriendlyTurrets.attackPlayersConfig.Value && item is Player) +
                    " (!FriendlyTurrets.attackTamedConfig.Value  && item.IsTamed())=" + (!FriendlyTurrets.attackTamedConfig.Value && item.IsTamed()) +
                    " item.GetBaseAI().m_passiveAggresive)=" + (baseAI ? baseAI.m_passiveAggresive : false));

                if (baseAI == null || (!FriendlyTurrets.attackPassivesConfig.Value && baseAI.m_passiveAggresive) || (!FriendlyTurrets.attackTamedConfig.Value && item.IsTamed()) || (!FriendlyTurrets.attackPlayersConfig.Value && item is Player) || (!FriendlyTurrets.attackBabiesConfig.Value && item.GetComponents<Growup>().Any()))
                {
                    continue;
                }

                if (item.IsDead()) // dont beat a dead horse
                {
                    continue;
                }

                if (onlyTargets != null && onlyTargets.Count > 0)
                {
                    bool flag = false;
                    foreach (Character onlyTarget in onlyTargets)
                    {
                        if (item.m_name == onlyTarget.m_name)
                        {
                            flag = true;
                            break;
                        }
                    }
                    if (!flag)
                    {
                        continue;
                    }
                }
                


                if ((!(baseAI != null) || !baseAI.IsSleeping()) && BaseAI.CanSenseTarget(me, eyePoint, hearRange, viewRange, viewAngle, alerted, mistVision, item, FriendlyTurrets.attackPassivesConfig.Value, FriendlyTurrets.attackTamedConfig.Value))
                {
                    float num2 = Vector3.Distance(item.transform.position, me.position);
                    if (num2 < num || character == null)
                    {
                        character = item;
                        num = num2;
                    }
                }
            }
            if (FriendlyTurrets.debugLogging)
            {
                if (character != null) logger.LogMessage($"{character} has been chosen as the target!");
            }
            return character;
        }



    }
}