using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IteratorKit.CMOracle;
// using IteratorKit.Util;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using UnityEngine;
using OracleJData = IteratorKit.CMOracle.OracleJSON; // remove this after the upcoming rewrite

namespace OracleRooms
{
    internal static class IteratorKitHooks
    {
        private static readonly List<Hook> mh = [];
        private static readonly List<ILHook> mhIL = [];
        private static object ikJData = null;

        public static void Apply()
        {
            Plugin.Logger.LogError("Patching IteratorKit");
            try
            {
                var oracleDataMethods = typeof(CMOracleModule).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Where(x => x.Name == nameof(CMOracleModule.GetOracleData)); // remove the Get after upcoming rewrite
                mh.Add(new Hook(oracleDataMethods.First(x => x.GetParameters().Length == 1), CMOracleModule_OracleData_1param));
                mh.Add(new Hook(oracleDataMethods.First(x => x.GetParameters().Length == 2), CMOracleModule_OracleData_2param));

                // The following works because while this method is run in OnModsInit, the method this hooks is in PostModsInit so is run after
                mhIL.Add(new ILHook(typeof(IteratorKit.IteratorKit).GetMethod("LoadOracleFiles", BindingFlags.NonPublic | BindingFlags.Instance), IteratorKit_LoadOracleFiles));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("IteratorKit hooks unable to be applied!");
                Plugin.Logger.LogError(ex);
                Unapply();
            }
        }

        public static void Unapply()
        {
            foreach (var hook in mh)
            {
                hook.Undo();
                hook.Dispose();
            }
            mh.Clear();
            foreach (var hook in mhIL)
            {
                hook.Undo();
                hook.Dispose();
            }
            mhIL.Clear();
        }

        internal static void AddIterators(HashSet<string> oracles)
        {
            try
            {
                if (ikJData is null) return;
                var jData = (List<OracleJData>) ikJData;

                /*foreach (var list in jData)
                {
                    foreach (var data in list)
                    {
                        oracles.Add(data.id);
                    }
                }*/
                foreach (var data in jData)
                {
                    oracles.Add(data.id);
                    Plugin.Logger.LogDebug("Found IK custom iter: " + data.id);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("IK incompatible! (add)");
                Plugin.Logger.LogError(ex);
            }
        }

        internal static void RearrangeIKData(Dictionary<string, Oracle.OracleID> roomData)
        {
            try
            {
                if (ikJData is null) return;
                // TODO: uncomment after upcoming IK rewrite
                /*var jData = (ITKMultiValueDictionary<string, OracleJData>)ikJData;

                // Save and clear out the old data
                Dictionary<string, OracleJData> holder = [];
                foreach (var list in jData.Values)
                {
                    foreach (var data in list)
                    {
                        holder.Add(data.id, data);
                    }
                    list.Clear();
                }
                jData.Clear();

                // Rearrange the room list so as to trick IK into spawning them in the correct place
                foreach (var pair in roomData)
                {
                    if (holder.ContainsKey(pair.Value.value))
                    {
                        if (jData.ContainsKey(pair.Key))
                        {
                            jData[pair.Key].Add(holder[pair.Value.value]);
                        }
                        else
                        {
                            jData[pair.Key] = [holder[pair.Value.value]];
                        }
                    }
                }*/
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("IK incompatible! (rearrange)");
                Plugin.Logger.LogError(ex);
            }
        }

        private static void IteratorKit_LoadOracleFiles(ILContext il)
        {
            // This method doesn't change any of the code, it just steals a variable
            var c = new ILCursor(il);

            // c.GotoNext(x => x.MatchStfld<IteratorKit.IteratorKit>(nameof(IteratorKit.IteratorKit.oracleJsons)));
            c.GotoNext(x => x.MatchStfld<IteratorKit.IteratorKit>(nameof(IteratorKit.IteratorKit.oracleJsonData)));
            c.EmitDelegate((List<OracleJData> data) =>
            {
                ikJData = data;
                return data;
            });
        }

        // private static OracleJDataTilePos Vec2JData(Vector2 pos, Room room) => IntVec2JData(room.GetTilePosition(pos));
        // private static OracleJDataTilePos IntVec2JData(IntVector2 pos) => new() { x = pos.x, y = pos.y };
        private static OracleJsonTilePos Vec2JData(Vector2 pos, Room room) => IntVec2JData(room.GetTilePosition(pos));
        private static OracleJsonTilePos IntVec2JData(IntVector2 pos) => new() { x = pos.x, y = pos.y };
        private static CMOracleData CMOracleModule_OracleData_1param(Func<Oracle, CMOracleData> orig, Oracle oracle) // change this and the following to CMOracle
        {
            var data = orig(oracle);
            var cornerPos = Util.GetCornerPositions(oracle);

            data.oracleJson.startPos = Plugin.OraclePos(oracle);
            // if (data.oracleJson.basePos != null) data.oracleJson.basePos = Plugin.OraclePos(oracle);

            data.oracleJson.cornerPositions = [
                Vec2JData(cornerPos[0], oracle.room),
                Vec2JData(cornerPos[1], oracle.room),
                Vec2JData(cornerPos[2], oracle.room),
                Vec2JData(cornerPos[3], oracle.room)
            ];

            return data;
        }

        delegate bool orig_CMOracleModule_OracleData_2param(Oracle oracle, out CMOracleData data);
        private static bool CMOracleModule_OracleData_2param(orig_CMOracleModule_OracleData_2param orig, Oracle oracle, out CMOracleData data)
        {
            var result = orig(oracle, out data);

            if (result)
            {
                var cornerPos = Util.GetCornerPositions(oracle);

                data.oracleJson.startPos = Plugin.OraclePos(oracle);
                // if (data.oracleJson.basePos != null) data.oracleJson.basePos = Plugin.OraclePos(oracle);

                data.oracleJson.cornerPositions = [
                    Vec2JData(cornerPos[0], oracle.room),
                    Vec2JData(cornerPos[1], oracle.room),
                    Vec2JData(cornerPos[2], oracle.room),
                    Vec2JData(cornerPos[3], oracle.room)
                ];
            }

            return result;
        }
    }
}
