using System;
using System.Runtime.CompilerServices;
using MoreSlugcats;
using RWCustom;
using UnityEngine;
using Random = UnityEngine.Random;

namespace OracleRooms
{
    partial class Plugin
    {
        private static readonly ConditionalWeakTable<AbstractPhysicalObject, StrongBox<Vector2>> oraclePosCWT = new();
        private static Vector2 OraclePos(Oracle self) => oraclePosCWT.GetValue(self.abstractPhysicalObject, _ => new StrongBox<Vector2>(Util.RandomAccessiblePoint(self.room))).Value;

        private Vector2 CLOracleBehavior_RandomRoomPoint(On.MoreSlugcats.CLOracleBehavior.orig_RandomRoomPoint orig, CLOracleBehavior self)
        {
            var room = self.oracle.room;
            var rect = Util.FurthestEdges(room.GetTilePosition(OraclePos(self.oracle)), room);
            var width = (rect.Width - 1) * 20f;
            var height = (rect.Height - 1) * 20f;
            return room.MiddleOfTile(rect.left, rect.bottom) + new Vector2(width * Random.value, height * Random.value);
        }

        private Vector2 SLOracleBehavior_RandomRoomPoint(On.SLOracleBehavior.orig_RandomRoomPoint orig, SLOracleBehavior self)
        {
            var room = self.oracle.room;
            var rect = Util.FurthestEdges(room.GetTilePosition(OraclePos(self.oracle)), room);
            var width = (rect.Width - 1) * 20f;
            var height = (rect.Height - 1) * 20f;
            return room.MiddleOfTile(rect.left, rect.bottom) + new Vector2(width * Random.value, height * Random.value);
        }

        private Vector2 SLOracleBehavior_ClampMediaPos(On.SLOracleBehavior.orig_ClampMediaPos orig, SLOracleBehavior self, Vector2 mediaPos)
        {
            var room = self.oracle.room;
            var rect = Util.FurthestEdges(room.GetTilePosition(OraclePos(self.oracle)), room);

            var bl = room.MiddleOfTile(rect.left, rect.bottom);
            var tr = room.MiddleOfTile(rect.right, rect.top);
            return new Vector2(Mathf.Min(Mathf.Max(mediaPos.x, bl.x), tr.x), Mathf.Min(Mathf.Max(mediaPos.y, bl.y), tr.y));
        }

        private void STOracleBehavior_ctor(On.MoreSlugcats.STOracleBehavior.orig_ctor orig, STOracleBehavior self, Oracle oracle)
        {
            orig(self, oracle);

            var room = oracle.room;
            var rect = Util.FurthestEdges(room.GetTilePosition(OraclePos(oracle)), room);
            self.boxBounds = new Rect(room.MiddleOfTile(rect.left, rect.bottom), new Vector2(rect.Width - 1, rect.Height - 1) * 20f);
            // subtract 1 from width and height so half a tile padding all sides

            for (int i = 0; i < self.gridPositions.GetLength(0); i++)
            {
                float x = Custom.LerpMap(i, -1, self.gridPositions.GetLength(0), self.boxBounds.xMin, self.boxBounds.xMax);
                for (int j = 0; j < self.gridPositions.GetLength(1); j++)
                {
                    float y = Custom.LerpMap(j, -1, self.gridPositions.GetLength(0), self.boxBounds.yMin, self.boxBounds.yMax);
                    self.gridPositions[i, j] = new Vector2(x, y);
                }
            }

            self.midPoint = new Vector2((self.boxBounds.xMax + self.boxBounds.xMin) / 2f, (self.boxBounds.yMax + self.boxBounds.yMin) / 2f);
            self.SetNewDestination(self.midPoint);
        }

        private Vector2 OracleBehavior_OracleGetToPos(Func<OracleBehavior, Vector2> orig, OracleBehavior self)
        {
            return OraclePos(self.oracle);
        }

        private Vector2 SLOracleBehavior_OracleGetToPos(Func<SLOracleBehavior, Vector2> orig, SLOracleBehavior self)
        {
            return self.moonActive ? orig(self) : OraclePos(self.oracle);
        }

        private bool SLOracleBehavior_InSitPosition(Func<SLOracleBehavior, bool> orig, SLOracleBehavior self)
        {
            var oracle = self.oracle;
            return oracle.room.GetTilePosition(oracle.firstChunk.pos).x == oracle.room.GetTilePosition(OraclePos(oracle)).x && !self.moonActive;
        }

        public static Vector2 SSSleepoverBehavior_holdPlayerPos(Func<SSOracleBehavior.SSSleepoverBehavior, Vector2> orig, SSOracleBehavior.SSSleepoverBehavior self) =>
            orig(self) + OraclePos(self.oracle) - new Vector2(668f, 268f);
        public static Vector2 SSOracleMeetPurple_holdPlayerPos(Func<SSOracleBehavior.SSOracleMeetPurple, Vector2> orig, SSOracleBehavior.SSOracleMeetPurple self) =>
            orig(self) + OraclePos(self.oracle) - new Vector2(668f, 268f);
        public static Vector2 SSOracleGetGreenNeuron_holdPlayerPos(Func<SSOracleBehavior.SSOracleGetGreenNeuron, Vector2> orig, SSOracleBehavior.SSOracleGetGreenNeuron self) =>
            orig(self) + OraclePos(self.oracle) - new Vector2(668f, 268f);

        private void OracleArm_ctor(On.Oracle.OracleArm.orig_ctor orig, Oracle.OracleArm self, Oracle oracle)
        {
            orig(self, oracle);

            // Set new arm positions
            var room = oracle.room;
            var rect = Util.FurthestEdges(room.GetTilePosition(OraclePos(oracle)), room);

            self.cornerPositions[0] = room.MiddleOfTile(rect.left, rect.top);
            self.cornerPositions[1] = room.MiddleOfTile(rect.right, rect.top);
            self.cornerPositions[2] = room.MiddleOfTile(rect.right, rect.bottom);
            self.cornerPositions[3] = room.MiddleOfTile(rect.left, rect.bottom);

            // Reset joint positions
            foreach (var joint in self.joints)
            {
                joint.pos = self.BasePos(1f);
                joint.lastPos = joint.pos;
            }
        }
    }
}
