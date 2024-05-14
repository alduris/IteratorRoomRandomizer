using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IteratorKit.CMOracle;
using MonoMod.RuntimeDetour;
using RWCustom;
using UnityEngine;

namespace OracleRooms
{
    internal static class IteratorKitHooks
    {
        private static readonly List<Hook> mh = [];
        public static void Apply()
        {
            var oracleDataMethods = typeof(CMOracle).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(x => x.Name == nameof(CMOracleModule.OracleData));
            mh.Add(new Hook(oracleDataMethods.First(x => x.GetParameters().Length == 1), CMOracleModule_OracleData));
        }

        public static void Unapply()
        {
            foreach (var hook in mh)
            {
                hook.Undo();
                hook.Dispose();
            }
            mh.Clear();
        }

        private static OracleJDataTilePos Vec2JData(Vector2 pos, Room room) => IntVec2JData(room.GetTilePosition(pos));
        private static OracleJDataTilePos IntVec2JData(IntVector2 pos) => new OracleJDataTilePos { x = pos.x, y = pos.y };
        private static CMOracleData CMOracleModule_OracleData(Func<CMOracle, CMOracleData> orig, CMOracle oracle)
        {
            var data = orig(oracle);
            var cornerPos = Util.GetCornerPositions(oracle);

            data.oracleJson.startPos = Plugin.OraclePos(oracle);
            if (data.oracleJson.basePos != null) data.oracleJson.basePos = Plugin.OraclePos(oracle);

            data.oracleJson.cornerPositions = [
               Vec2JData(cornerPos[0], oracle.room),
               Vec2JData(cornerPos[1], oracle.room),
               Vec2JData(cornerPos[2], oracle.room),
               Vec2JData(cornerPos[3], oracle.room)
            ];

            return data;
        }
    }
}
