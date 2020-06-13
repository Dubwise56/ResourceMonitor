﻿using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DubsAnalyzer
{
    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.FindPath), typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode))]
    internal class H_FindPath
    {
        public static bool Active = false;

        public static bool pathing;

        public static int NodeIndex = 0;

        public static ProfileMode p = ProfileMode.Create("PathFinder", UpdateMode.Tick, null, false, typeof(H_FindPath));
        public static void ProfilePatch()
        {
            var go = new HarmonyMethod(typeof(H_FindPath), nameof(Start));
            var biff = new HarmonyMethod(typeof(H_FindPath), nameof(Stop));

            void slop(Type e, string s)
            {
                Analyzer.harmony.Patch(AccessTools.Method(e, s), go, biff);
            }

            var mad = AccessTools.Method(typeof(Reachability), nameof(Reachability.CanReach),
                new[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms) });
            Analyzer.harmony.Patch(mad, go, biff);

            // slop(typeof(PathFinder), nameof(PathFinder.CalculateDestinationRect));
            slop(typeof(PathFinder), nameof(PathFinder.GetAllowedArea));
            slop(typeof(PawnUtility), nameof(PawnUtility.ShouldCollideWithPawns));
            slop(typeof(PathFinder), nameof(PathFinder.DetermineHeuristicStrength));
            slop(typeof(PathFinder), nameof(PathFinder.CalculateAndAddDisallowedCorners));
            slop(typeof(PathFinder), nameof(PathFinder.InitStatusesAndPushStartNode));
        }

        [HarmonyPriority(Priority.Last)]
        public static void Start(MethodBase __originalMethod, ref string __state)
        {
            if (p.Active && pathing)
            {
                {
                    __state = __originalMethod.Name;
                    p.Start(__state, __originalMethod);
                }
            }
        }

        [HarmonyPriority(Priority.First)]
        public static void Stop(string __state)
        {
            if (p.Active && pathing)
            {
                p.Stop(__state);
            }
        }

        [HarmonyPriority(Priority.First)]
        public static void Prefix(MethodBase __originalMethod, ref string __state)
        {
            if (p.Active)
            {
                __state = "PathFinder.FindPath";
                p.Start(__state, __originalMethod);
                pathing = true;
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(string __state)
        {
            if (p.Active)
            {
                pathing = false;
                p.Stop(__state);
            }
        }
    }
}