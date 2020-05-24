﻿using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace DubsAnalyzer
{
    [ProfileMode("GameComponent", UpdateMode.Tick)]
    public static class H_GameComponent
    {
        public static bool Active = false;

        public static void ProfilePatch()
        {
            Analyzer.harmony.Patch(AccessTools.Method(typeof(GameComponentUtility), nameof(GameComponentUtility.GameComponentTick)), new HarmonyMethod(typeof(H_GameComponent), nameof(GameComponentTick)));
        }

        public static bool GameComponentTick()
        {
            if (!Active) return true;

            List<GameComponent> components = Current.Game.components;
            for (int i = 0; i < components.Count; i++)
            {
                try
                {
                    var trash = components[i].GetType().Name;
                    Analyzer.Start(trash);
                    components[i].GameComponentTick();
                    Analyzer.Stop(trash);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString(), false);
                }
            }
            return false;
        }
    }

    [ProfileMode("Game Component", UpdateMode.Update)]
    public static class H_GameComponentUpdate
    {
        public static bool Active = false;

        public static void ProfilePatch()
        {
            Analyzer.harmony.Patch(AccessTools.Method(typeof(GameComponentUtility), nameof(GameComponentUtility.GameComponentUpdate)), new HarmonyMethod(typeof(H_GameComponentUpdate), nameof(GameComponentTick)));
        }

        public static bool GameComponentTick()
        {
            if (!Active) return true;

            List<GameComponent> components = Current.Game.components;
            for (int i = 0; i < components.Count; i++)
            {
                try
                {
                    var trash = components[i].GetType().Name;
                    Analyzer.Start(trash);
                    components[i].GameComponentUpdate();
                    Analyzer.Stop(trash);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString(), false);
                }
            }
            return false;
        }
    }

    [ProfileMode("Game Component", UpdateMode.GUI)]
    public static class H_GameComponentUpdateGUI
    {
        public static bool Active = false;

        public static void ProfilePatch()
        {
            Analyzer.harmony.Patch(AccessTools.Method(typeof(GameComponentUtility), nameof(GameComponentUtility.GameComponentOnGUI)), new HarmonyMethod(typeof(H_GameComponentUpdateGUI), nameof(GameComponentTick)));
        }

        public static bool GameComponentTick()
        {
            if (!Active) return true;

            List<GameComponent> components = Current.Game.components;
            for (int i = 0; i < components.Count; i++)
            {
                try
                {
                    var trash = components[i].GetType().Name;
                    Analyzer.Start(trash);
                    components[i].GameComponentOnGUI();
                    Analyzer.Stop(trash);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString(), false);
                }
            }
            return false;
        }
    }
}