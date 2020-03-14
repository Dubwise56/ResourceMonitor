﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace DubsAnalyzer
{
    [ProfileMode("JobGiver_Work", UpdateMode.Tick, "The scanners which issue jobs to pawns", true)]
    public static class H_TryIssueJobPackageTrans
    {
        [Setting("By Work Type")] public static bool ByWorkType = false;

        [Setting("Request Types")] public static bool RequestTypes = false;

        [Setting("Per Pawn")] public static bool PerPawn = false;

        public static bool Active = false;

        private static string pawnname;
        private static WorkGiver wg;

        public static void ProfilePatch()
        {
            Log.Message("Patching workgiver");
            var pre = new HarmonyMethod(typeof(H_TryIssueJobPackageTrans), nameof(Prefix));
            var post = new HarmonyMethod(typeof(H_TryIssueJobPackageTrans), nameof(Postfix));
            var t = new HarmonyMethod(typeof(H_TryIssueJobPackageTrans), nameof(piler));
            var o = AccessTools.Method(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage));
            Analyzer.harmony.Patch(o, pre, post, t);
        }

        public static void Prefix(Pawn pawn)
        {
            pawnname = pawn.Name.ToStringShort;
        }
        public static void Postfix()
        {
            Stop(wg);
        }

        private static string namer()
        {
            var daffy = string.Empty;
            if (ByWorkType)
            {
                daffy = wg.def?.workType?.defName;
            }
            else
            {
                daffy = $"{wg.def?.defName} - {wg.def?.workType?.defName} - {wg.def?.modContentPack?.Name}";

                if (RequestTypes && wg is WorkGiver_Scanner scan)
                {
                    daffy += $" - {scan.PotentialWorkThingRequest}";
                    if (scan.PotentialWorkThingRequest.group == ThingRequestGroup.BuildingArtificial)
                    {
                        daffy += " VERY BAD!";
                    }
                }
            }

            if (PerPawn)
            {
                daffy += $" - {pawnname}";
            }

            return daffy;
        }

        private static string CurrentKey = string.Empty;
        public static void Start(WorkGiver giver)
        {
            if (!Active)
            {
                return;
            }

          //  Log.Warning("start ");

            wg = giver;

            CurrentKey = string.Empty;

            if (ByWorkType)
            {
                CurrentKey = giver.def.workType.defName;
            }
            else
            {
                CurrentKey = giver.def.defName;
            }
            if (PerPawn)
            {
                CurrentKey = string.Intern(CurrentKey + pawnname);
            }

            Analyzer.Start(CurrentKey, namer);
        }

        public static void Stop(WorkGiver giver)
        {
            if (Active)
            {
              //  Log.Warning("stop");
                Analyzer.Stop(CurrentKey);
            }
        }


        static IEnumerable<CodeInstruction> piler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();

            bool start = false;
            bool endloop = false;

            for (var i = 0; i < instructionsList.Count; i++)
            {
                var instruction = instructionsList[i];

                if (start ==false && instruction.opcode == OpCodes.Nop)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)8);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(H_TryIssueJobPackageTrans), nameof(Start)));
                    yield return instruction;
                    start = true;
                     // Log.Warning("prefixed start loop at " + instruction.opcode);
                }
                else if (endloop == false &&
                    instruction.opcode == OpCodes.Ldflda &&
                    instructionsList[i - 1].opcode == OpCodes.Ldloc_0 &&
                    instructionsList[i - 2].opcode == OpCodes.Endfinally && 
                    instructionsList[i - 3].opcode == OpCodes.Leave_S
                )
                {
                    
                    yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)8);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(H_TryIssueJobPackageTrans), nameof(Stop)));
                    yield return instruction;
                    endloop = true;
                    //   Log.Warning("postfixed stop loop " + instruction.opcode);
                }
                else
                {
                    yield return instruction;
                }
            }

            if (start && endloop)
            {
               // Log.Message("workgiver patched");
            }
            else
            {
                Log.Error("Failed to patch workgiver for profiling");
            }
        }
    }


    [ProfileMode("WorkGiverDetoured", UpdateMode.Tick,
        "This version detours the whole method, just used to confirm the other WorkGiver is accurate or do custom stuff")]
    internal class H_TryIssueJobPackage
    {
        [Setting("By Work Type")] public static bool ByWorkType = false;

        [Setting("Request Types")] public static bool RequestTypes = false;

        [Setting("Per Pawn")] public static bool PerPawn = false;

        public static bool Active = false;
        public static WorkGiver giver;
        public static string key;

        public static void ProfilePatch()
        {
            Log.Message("Patching workgiver");
            var pre = new HarmonyMethod(typeof(H_TryIssueJobPackage), nameof(Prefix));
            var o = AccessTools.Method(typeof(JobGiver_Work), "TryIssueJobPackage", new Type[] { typeof(Pawn), typeof(JobIssueParams) });
            Analyzer.harmony.Patch(o, pre);
        }

        private static bool Prefix(JobGiver_Work __instance, Pawn pawn, ref ThinkResult __result)
        {
            if (!Active)
            {
                return true;
            }
            __result = Detour(__instance, pawn);
            return false;
        }

        private static ThinkResult Detour(JobGiver_Work __instance, Pawn pawn)
        {
            if (__instance.emergency && pawn.mindState.priorityWork.IsPrioritized)

            {
                var workGiversByPriority = pawn.mindState.priorityWork.WorkGiver.workType.workGiversByPriority;
                for (var i = 0; i < workGiversByPriority.Count; i++)
                {
                    var worker = workGiversByPriority[i].Worker;
                    var job = __instance.GiverTryGiveJobPrioritized(pawn, worker, pawn.mindState.priorityWork.Cell);
                    if (job != null)
                    {
                        job.playerForced = true;
                        return new ThinkResult(job, __instance, workGiversByPriority[i].tagToGive);
                    }
                }

                pawn.mindState.priorityWork.Clear();
            }

            var list = __instance.emergency
                ? pawn.workSettings.WorkGiversInOrderEmergency
                : pawn.workSettings.WorkGiversInOrderNormal;

            var num = -999;
            var bestTargetOfLastPriority = TargetInfo.Invalid;
            WorkGiver_Scanner scannerWhoProvidedTarget = null;
            var coo = list.Count;
            for (var j = 0; j < coo; j++)
            {
                var workGiver = list[j];

                string namer()
                {
                    var daffy = string.Empty;
                    if (ByWorkType)
                    {
                        daffy = workGiver.def?.workType?.defName;
                    }
                    else
                    {
                        daffy =
                            $"{workGiver.def?.defName} - {workGiver.def?.workType.defName} - {workGiver.def?.modContentPack?.Name}";
                    }

                    //if (true)
                    //{
                    //    daffy += $" - { TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)).ToString()} - {pawn.Name.ToStringShort}";
                    //}

                    if (RequestTypes && workGiver is WorkGiver_Scanner scan)
                    {
                        daffy += $" - {scan.PotentialWorkThingRequest}";
                        if (scan.PotentialWorkThingRequest.group ==
                            ThingRequestGroup.BuildingArtificial)
                        {
                            daffy += " VERY BAD!";
                        }
                    }

                    return daffy;
                }

                if (workGiver.def.priorityInType != num && bestTargetOfLastPriority.IsValid)
                {
                    break;
                }

                if (__instance.PawnCanUseWorkGiver(pawn, workGiver))
                {
                    var name = string.Empty;

                    if (ByWorkType)
                    {
                        name = workGiver.def.workType.defName;
                    }
                    else
                    {
                        name = workGiver.def.defName;
                    }

                    //if (true)
                    //{
                    //    name = string.Intern(name + pawn.Name.ToStringShort);
                    //}
                    if (workGiver is WorkGiver_Scanner scanny)
                    {
                        name += $"{scanny.PotentialWorkThingRequest}";
                    }


                    try
                    {
                        var job2 = workGiver.NonScanJob(pawn);
                        if (job2 != null)
                        {
                            return new ThinkResult(job2, __instance, list[j].def.tagToGive);
                        }

                       

                        if (workGiver is WorkGiver_Scanner scanner)
                        {

                            Analyzer.Start(name, namer, workGiver.GetType(), workGiver.def, pawn);

                            if (scanner.def.scanThings)
                            {
                                bool Predicate(Thing t)
                                {
                                    return !t.IsForbidden(pawn) && scanner.HasJobOnThing(pawn, t);
                                }

                                var enumerable = scanner.PotentialWorkThingsGlobal(pawn);
                                Thing thing;
                                if (scanner.Prioritized)
                                {
                                    var enumerable2 = enumerable;
                                    if (enumerable2 == null)
                                    {
                                        enumerable2 = pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
                                    }

                                    if (scanner.AllowUnreachable)
                                    {
                                        thing = GenClosest.ClosestThing_Global(pawn.Position, enumerable2, 99999f, Predicate, x => scanner.GetPriority(pawn, x));
                                    }
                                    else
                                    {
                                        var traverseParams = TraverseParms.For(pawn, scanner.MaxPathDanger(pawn));
                                        var validator = (Predicate<Thing>)Predicate;
                                        thing = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map,
                                            enumerable2,
                                            scanner.PathEndMode, traverseParams, 9999f, validator,
                                            x => scanner.GetPriority(pawn, x));
                                    }
                                }
                                else if (scanner.AllowUnreachable)
                                {
                                    var enumerable3 = enumerable;
                                    if (enumerable3 == null)
                                    {
                                        enumerable3 = pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
                                    }

                                    thing = GenClosest.ClosestThing_Global(pawn.Position, enumerable3, 99999f, Predicate);
                                }
                                else
                                {


                                    giver = workGiver;
                                    key = name;
                                    Analyzer.Start(name, namer, workGiver.GetType(), workGiver.def, pawn);
                                    thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                                        scanner.PotentialWorkThingRequest,
                                        scanner.PathEndMode, TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)),
                                        9999f, Predicate, enumerable, 0,
                                        scanner.MaxRegionsToScanBeforeGlobalSearch, enumerable != null);
                                   
                                    giver = null;
                                }

                                if (thing != null)
                                {
                                    bestTargetOfLastPriority = thing;
                                    scannerWhoProvidedTarget = scanner;
                                }
                            }




                            if (scanner.def.scanCells)
                            {
                                var closestDistSquared = 99999f;
                                var bestPriority = float.MinValue;
                                var prioritized = scanner.Prioritized;
                                var allowUnreachable = scanner.AllowUnreachable;
                                var maxDanger = scanner.MaxPathDanger(pawn);
                                foreach (var intVec in scanner.PotentialWorkCellsGlobal(pawn))
                                {
                                    var flag = false;
                                    var num4 = (intVec - pawn.Position).LengthHorizontalSquared;
                                    var num5 = 0f;
                                    if (prioritized)
                                    {
                                        if (!intVec.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, intVec))
                                        {
                                            if (!allowUnreachable &&
                                                !pawn.CanReach(intVec, scanner.PathEndMode, maxDanger))
                                            {
                                                continue;
                                            }

                                            num5 = scanner.GetPriority(pawn, intVec);
                                            if (num5 > bestPriority || num5 == bestPriority && num4 < closestDistSquared)
                                            {
                                                flag = true;
                                            }
                                        }
                                    }
                                    else if (num4 < closestDistSquared && !intVec.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, intVec))
                                    {
                                        if (!allowUnreachable && !pawn.CanReach(intVec, scanner.PathEndMode, maxDanger))
                                        {
                                            continue;
                                        }

                                        flag = true;
                                    }

                                    if (flag)
                                    {
                                        bestTargetOfLastPriority = new TargetInfo(intVec, pawn.Map);
                                        scannerWhoProvidedTarget = scanner;
                                        closestDistSquared = num4;
                                        bestPriority = num5;
                                    }
                                }
                            }


                            Analyzer.Stop(name);
                        }


                        

                    }
                    catch (Exception ex)
                    {
                        Log.Error(string.Concat(pawn, " threw exception in WorkGiver ", workGiver.def.defName, ": ",
                            ex.ToString()));
                    }

                    if (bestTargetOfLastPriority.IsValid)
                    {
                        //  pawn.mindState.lastGivenWorkType = workGiver.def.workType;
                        Job job3;
                        if (bestTargetOfLastPriority.HasThing)
                        {
                            job3 = scannerWhoProvidedTarget.JobOnThing(pawn, bestTargetOfLastPriority.Thing);
                        }
                        else
                        {
                            job3 = scannerWhoProvidedTarget.JobOnCell(pawn, bestTargetOfLastPriority.Cell);
                        }

                        if (job3 != null)
                        {
                            job3.workGiverDef = scannerWhoProvidedTarget.def;
                            return new ThinkResult(job3, __instance, list[j].def.tagToGive);
                        }

                        Log.ErrorOnce(
                            string.Concat(scannerWhoProvidedTarget, " provided target ", bestTargetOfLastPriority,
                                " but yielded no actual job for pawn ", pawn,
                                ". The CanGiveJob and JobOnX methods may not be synchronized."), 6112651);
                    }

                    num = workGiver.def.priorityInType;
                }
            }

            return ThinkResult.NoJob;
        }
    }



    //[HarmonyPatch(typeof(Region), nameof(Region.DangerFor))]
    //static class H_DangerFor
    //{
    //    public static bool Prefix(Region __instance, Pawn p, ref Danger __result, ref string __state)
    //    {
    //        return true;
    //        if (!Analyzer.running)
    //        {
    //            return true;
    //        }

    //        if (TryIssueJobPackage.giver == null)
    //        {
    //            return true;
    //        }


    //        if (Current.ProgramState == ProgramState.Playing)
    //        {
    //            if (__instance.cachedDangersForFrame != Time.frameCount)
    //            {
    //                __instance.cachedDangers.Clear();
    //                __instance.cachedDangersForFrame = Time.frameCount;
    //            }
    //            else
    //            {
    //                for (int i = 0; i < __instance.cachedDangers.Count; i++)
    //                {
    //                    if (__instance.cachedDangers[i].Key == p)
    //                    {
    //                        __result = __instance.cachedDangers[i].Value;
    //                        return false;
    //                    }
    //                }
    //            }
    //        }
    //        Room room = __instance.Room;
    //        float temperature = room.Temperature;

    //        __state = TryIssueJobPackage.key + ": SafeTemperatureRange";
    //        Analyzer.Start(__state);
    //        FloatRange floatRange = p.SafeTemperatureRange();
    //        Analyzer.Stop(__state);

    //        Danger danger;
    //        if (floatRange.Includes(temperature))
    //        {
    //            danger = Danger.None;
    //        }
    //        else if (floatRange.ExpandedBy(80f).Includes(temperature))
    //        {
    //            danger = Danger.Some;
    //        }
    //        else
    //        {
    //            danger = Danger.Deadly;
    //        }
    //        if (Current.ProgramState == ProgramState.Playing)
    //        {
    //            __instance.cachedDangers.Add(new KeyValuePair<Pawn, Danger>(p, danger));
    //        }
    //        __result = danger;

    //        return false;
    //    }

    //    public static void Postfix(string __state)
    //    {
    //        if (!string.IsNullOrEmpty(__state))
    //        {

    //        }
    //    }
    //}

    //[HarmonyPatch(typeof(Region), nameof(Region.Allows))]
    //static class H_Allows
    //{
    //    public static bool Prefix(Region __instance, TraverseParms tp, bool isDestination, ref bool __result, ref string __state)
    //    {
    //        return true;
    //        if (!Analyzer.running)
    //        {
    //            return true;
    //        }

    //        if (TryIssueJobPackage.giver == null)
    //        {
    //            return true;
    //        }

    //        __state = TryIssueJobPackage.key + ": Allows";


    //        if (tp.mode != TraverseMode.PassAllDestroyableThings && tp.mode != TraverseMode.PassAllDestroyableThingsNotWater && !__instance.type.Passable())
    //        {
    //            __result = false;
    //            return false;
    //        }
    //        if (tp.maxDanger < Danger.Deadly && tp.pawn != null)
    //        {
    //            Analyzer.Start(__state);
    //            Danger danger = __instance.DangerFor(tp.pawn);
    //            Analyzer.Stop(__state);
    //            if (isDestination || danger == Danger.Deadly)
    //            {
    //                Region region = tp.pawn.GetRegion(RegionType.Set_All);
    //                if ((region == null || danger > region.DangerFor(tp.pawn)) && danger > tp.maxDanger)
    //                {
    //                    __result = false;
    //                    return false;
    //                }
    //            }
    //        }
    //        switch (tp.mode)
    //        {
    //            case TraverseMode.ByPawn:
    //                {
    //                    if (__instance.door == null)
    //                    {
    //                        __result = true;
    //                        return false;
    //                    }
    //                    ByteGrid avoidGrid = tp.pawn.GetAvoidGrid(true);
    //                    if (avoidGrid != null && avoidGrid[__instance.door.Position] == 255)
    //                    {
    //                        __result = false;
    //                        return false;
    //                    }
    //                    if (tp.pawn.HostileTo(__instance.door))
    //                    {
    //                        __result = __instance.door.CanPhysicallyPass(tp.pawn) || tp.canBash;
    //                        return false;
    //                    }
    //                    __result = __instance.door.CanPhysicallyPass(tp.pawn) && !__instance.door.IsForbiddenToPass(tp.pawn);
    //                    return false;
    //                }
    //            case TraverseMode.PassDoors:
    //                __result = true;
    //                return false;
    //            case TraverseMode.NoPassClosedDoors:
    //                __result = __instance.door == null || __instance.door.FreePassage;
    //                return false;
    //            case TraverseMode.PassAllDestroyableThings:
    //                __result = true;
    //                return false;
    //            case TraverseMode.NoPassClosedDoorsOrWater:
    //                __result = __instance.door == null || __instance.door.FreePassage;
    //                return false;
    //            case TraverseMode.PassAllDestroyableThingsNotWater:
    //                __result = true;
    //                return false;
    //            default:
    //                return false;
    //        }
    //    }

    //    public static void Postfix(string __state)
    //    {
    //        if (!string.IsNullOrEmpty(__state))
    //        {

    //        }
    //    }
    //}
    //[HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.PawnCanUseWorkGiver))]
    //static class PawnCanUseWorkGiver
    //{
    //    public static void Prefix(WorkGiver giver, Pawn pawn, ref string __state)
    //    {
    //        if (!Analyzer.running)
    //        {
    //            return;
    //        }
    //        if (Analyzer.loggingMode != LoggingMode.Work && Analyzer.loggingMode != LoggingMode.WorkType) return;

    //        string namer()
    //        {
    //            var daffy = string.Empty;
    //            if (Analyzer.loggingMode == LoggingMode.WorkType)
    //            {
    //                daffy = $"{giver.def?.workType?.defName} PawnCanUseWorkGiver";
    //            }
    //            else
    //            {
    //                daffy = $"{giver.def?.defName} - {giver.def.workType.defName} - {giver.def?.modContentPack?.Name} PawnCanUseWorkGiver";
    //            }

    //            if (H_TryIssueJobPackageTrans.LogPerPawn)
    //            {
    //                daffy += " - " + pawn.Name.ToStringShort;
    //            }

    //            return daffy;
    //        }

    //        if (Analyzer.loggingMode == LoggingMode.WorkType)
    //        {
    //            __state = giver.def.workType.defName;
    //        }
    //        else
    //        {
    //            __state = giver.def.defName;
    //        }
    //        if (H_TryIssueJobPackageTrans.LogPerPawn)
    //        {
    //            __state = string.Intern(__state + pawn.Name.ToStringShort);
    //        }

    //        H_TryIssueJobPackageTrans.CurrentScan = __state;
    //        Analyzer.Start(__state, namer, giver.GetType(), giver.def, pawn);
    //    }

    //    public static void Postfix(string __state)
    //    {
    //        if (!string.IsNullOrEmpty(__state))
    //        {
    //            Analyzer.Stop(__state);
    //        }
    //    }
    //}

    //[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable))]
    //static class ClosestThingReachable
    //{
    //    public static void Prefix(ref string __state)
    //    {
    //        if (!Analyzer.running)
    //        {
    //            return;
    //        }
    //        if (Analyzer.loggingMode != LoggingMode.Work && Analyzer.loggingMode != LoggingMode.WorkType) return;

    //        __state = H_TryIssueJobPackageTrans.CurrentScan;
    //        Analyzer.Start(H_TryIssueJobPackageTrans.CurrentScan);
    //    }

    //    public static void Postfix(string __state)
    //    {
    //        if (!string.IsNullOrEmpty(__state))
    //        {
    //            Analyzer.Stop(__state);
    //        }
    //    }
    //}

    //[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable))]
    //static class ClosestThing_Global_Reachable
    //{
    //    public static void Prefix(ref string __state)
    //    {
    //        if (!Analyzer.running)
    //        {
    //            return;
    //        }
    //        if (Analyzer.loggingMode != LoggingMode.Work && Analyzer.loggingMode != LoggingMode.WorkType) return;

    //        __state = H_TryIssueJobPackageTrans.CurrentScan;
    //        Analyzer.Start(H_TryIssueJobPackageTrans.CurrentScan);
    //    }

    //    public static void Postfix(string __state)
    //    {
    //        if (!string.IsNullOrEmpty(__state))
    //        {
    //            Analyzer.Stop(__state);
    //        }
    //    }
    //}

    //public static class H_TryIssueJobPackageTrans
    //{
    //    public static string CurrentScan;
    //    public static bool LogPerPawn = true;
    //    public static void Patch(HarmonyInstance h)
    //    {
    //        var go = new HarmonyMethod(typeof(H_TryIssueJobPackageTrans), nameof(Prefix));
    //        var biff = new HarmonyMethod(typeof(H_TryIssueJobPackageTrans), nameof(Postfix));

    //        foreach (var allLeafSubclass in typeof(WorkGiver_Scanner).AllSubclassesNonAbstract())
    //        {
    //            h.Patch(AccessTools.Method(allLeafSubclass, nameof(WorkGiver_Scanner.JobOnThing)), go, biff);
    //            h.Patch(AccessTools.Method(allLeafSubclass, nameof(WorkGiver_Scanner.JobOnCell)), go, biff);
    //            h.Patch(AccessTools.Method(allLeafSubclass, nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal)), go, biff);
    //            h.Patch(AccessTools.Method(allLeafSubclass, nameof(WorkGiver_Scanner.NonScanJob)), go, biff);
    //            h.Patch(AccessTools.Method(allLeafSubclass, nameof(WorkGiver_Scanner.HasJobOnCell)), go, biff);
    //            h.Patch(AccessTools.Method(allLeafSubclass, nameof(WorkGiver_Scanner.HasJobOnThing)), go, biff);
    //        }

    //    }
    //    public static void Prefix(WorkGiver __instance, MethodBase __originalMethod, ref string __state, Pawn pawn)
    //    {
    //        if (!Analyzer.running)
    //        {
    //            return;
    //        }
    //        if (Analyzer.loggingMode != LoggingMode.Work && Analyzer.loggingMode != LoggingMode.WorkType) return;

    //        string namer()
    //        {
    //            var daffy = String.Empty;
    //            if (Analyzer.loggingMode == LoggingMode.WorkType)
    //            {
    //                daffy = __instance.def?.workType?.defName;
    //            }
    //            else
    //            {
    //                daffy = $"{__instance.def?.defName} - {__instance.def.workType.defName} - {__instance.def?.modContentPack?.Name}";
    //            }

    //            if (LogPerPawn)
    //            {
    //                daffy += " - " + pawn.Name.ToStringShort;
    //            }

    //            return daffy;
    //        }

    //        if (Analyzer.loggingMode == LoggingMode.WorkType)
    //        {
    //            __state = __instance.def.workType.defName;
    //        }
    //        else
    //        {
    //            __state = __instance.def.defName;
    //        }
    //        if (LogPerPawn)
    //        {
    //            __state = String.Intern(__state + pawn.Name.ToStringShort);
    //        }

    //        Analyzer.Start(__state, namer, __instance.GetType(), __instance.def, pawn);
    //    }

    //    public static void Postfix(string __state)
    //    {
    //        if (!String.IsNullOrEmpty(__state))
    //        {
    //            Analyzer.Stop(__state);
    //        }
    //    }
    //}
}