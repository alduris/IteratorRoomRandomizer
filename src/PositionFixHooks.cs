using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil.Cil;
using MonoMod.Cil;
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

        ///////////////////////////////////////////////////////////////////////
        // Misc hooks
        #region misc

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

        private Vector2 OracleBehavior_OracleGetToPos(Func<OracleBehavior, Vector2> orig, OracleBehavior self)
        {
            return OraclePos(self.oracle);
        }
        private Vector2 SLOracleBehavior_OracleGetToPos(Func<SLOracleBehavior, Vector2> orig, SLOracleBehavior self)
        {
            return self.moonActive ? orig(self) : OraclePos(self.oracle);
        }

        #endregion misc


        ///////////////////////////////////////////////////////////////////////
        // Moon's hooks
        #region moon

        private Vector2 SLOracleBehavior_RandomRoomPoint(On.SLOracleBehavior.orig_RandomRoomPoint orig, SLOracleBehavior self)
        {
            var rect = Util.FurthestEdges(OraclePos(self.oracle), self.oracle.room);
            return new Vector2(rect.xMin, rect.yMin) + new Vector2(rect.width * Random.value, rect.height * Random.value);
        }

        private Vector2 SLOracleBehavior_ClampMediaPos(On.SLOracleBehavior.orig_ClampMediaPos orig, SLOracleBehavior self, Vector2 mediaPos)
        {
            var rect = Util.FurthestEdges(OraclePos(self.oracle), self.oracle.room);
            return new Vector2(Mathf.Min(Mathf.Max(mediaPos.x, rect.xMin), rect.xMax), Mathf.Min(Mathf.Max(mediaPos.y, rect.yMin), rect.yMax));
        }

        private bool SLOracleBehavior_InSitPosition(Func<SLOracleBehavior, bool> orig, SLOracleBehavior self)
        {
            var oracle = self.oracle;
            return oracle.room.GetTilePosition(oracle.firstChunk.pos).x == oracle.room.GetTilePosition(OraclePos(oracle)).x && !self.moonActive;
        }

        private double SLOracleBehavior_BasePosScore(On.SLOracleBehavior.orig_BasePosScore orig, SLOracleBehavior self, Vector2 tryPos)
        {
            if (self.movementBehavior == SLOracleBehavior.MovementBehavior.Meditate || self.player == null)
            {
                return (double)Vector2.Distance(tryPos, OraclePos(self.oracle));
            }
            return orig(self, tryPos);
        }

        private void SLOracleBehavior_Move(ILContext il)
        {
            var c = new ILCursor(il);

            for (int i = 0; i < 2; i++)
            {
                c.GotoNext(MoveType.After, x => x.MatchLdcI4(77), x => x.MatchLdcI4(18));
                c.Emit(OpCodes.Ldarg_0);
                // consume the previous numbers (they were delicious) (actually we just don't want to waste stack resources)
                c.EmitDelegate((int a, int b, SLOracleBehavior self) => self.oracle.room.GetTilePosition(OraclePos(self.oracle)).x);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((SLOracleBehavior self) => self.oracle.room.GetTilePosition(OraclePos(self.oracle)).y);
            }
        }

        private void SLOracleBehavior_Update(ILContext il)
        {
            var c = new ILCursor(il);

            // Some sitting conditions
            for (int i = 0; i < 2; i++)
            {
                c.GotoNext(MoveType.After, x => x.MatchLdcR4(1430f));
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((float old, SLOracleBehavior self) => OraclePos(self.oracle).x - 200f);

                c.GotoNext(MoveType.After, x => x.MatchLdcR4(1560f) || x.MatchLdcR4(1660f));
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((float old, SLOracleBehavior self) => OraclePos(self.oracle).x + 200f);
            }

            // For seeing the player(s)
            var matchRef = new Func<IEnumerable<Player>, Func<Player, bool>, IEnumerable<Player>>(Enumerable.Where).Method; // thank you @SlimeCubed
            c.GotoNext(x => x.MatchCall(matchRef));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Func<Player, bool> orig, SLOracleBehavior self) => ((Player p) => p.firstChunk.pos.x >= self.oracle.arm.cornerPositions[0].x && p.firstChunk.pos.x <= self.oracle.arm.cornerPositions[1].x));

            c.GotoNext(MoveType.After, x => x.MatchLdcR4(1160f));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((float old, SLOracleBehavior self) => self.oracle.arm.cornerPositions[0].x);
        }

        #endregion moon

        ///////////////////////////////////////////////////////////////////////
        // Pebbles hooks
        #region pebbles

        private float SSOracleBehavior_BasePosScore(On.SSOracleBehavior.orig_BasePosScore orig, SSOracleBehavior self, Vector2 tryPos)
        {
            if (self.movementBehavior == SSOracleBehavior.MovementBehavior.Meditate || self.player == null)
            {
                return Vector2.Distance(tryPos, OraclePos(self.oracle));
            }
            return orig(self, tryPos);
        }

        private void SSOracleBehavior_Move(ILContext il)
        {
            var c = new ILCursor(il);

            for (int i = 0; i < 2; i++)
            {
                c.GotoNext(MoveType.After, x => x.MatchLdcI4(24), x => x.MatchLdcI4(17));
                c.Emit(OpCodes.Ldarg_0);
                // consume the previous numbers (they were delicious) (actually we just don't want to waste stack resources)
                c.EmitDelegate((int a, int b, SSOracleBehavior self) => self.oracle.room.GetTilePosition(OraclePos(self.oracle)).x);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((SSOracleBehavior self) => self.oracle.room.GetTilePosition(OraclePos(self.oracle)).y);
            }
        }

        private void SSOracleBehavior_Update(ILContext il)
        {
            var c = new ILCursor(il);

            c.GotoNext(MoveType.After, x => x.MatchLdcI4(24), x => x.MatchLdcI4(14));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((int a, int b, SSOracleBehavior self) => self.oracle.room.GetTilePosition(OraclePos(self.oracle)).x);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((SSOracleBehavior self) => self.oracle.room.GetTilePosition(OraclePos(self.oracle)).y);
        }

        private void SSOracleMeetPurple_Update(ILContext il)
        {
            var c = new ILCursor(il);

            for (int i = 0; i < 2; i++)
            {
                c.GotoNext(MoveType.After, x => x.MatchLdcI4(28), x => x.MatchLdcI4(32));
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((int a, int b, SSOracleBehavior.SSOracleMeetPurple self) => Util.FirstShortcut(self.oracle.room).x);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((SSOracleBehavior.SSOracleMeetPurple self) => Util.FirstShortcut(self.oracle.room).y);
            }
        }

        private void SSOracleMeetWhite_Update1(ILContext il)
        {
            var c = new ILCursor(il);

            for (int i = 0; i < 4; i++)
            {
                c.GotoNext(MoveType.After, x => x.MatchLdcI4(24), x => x.MatchLdcI4(14));
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((int a, int b, SSOracleBehavior.SSOracleMeetWhite self) => self.oracle.room.GetTilePosition(OraclePos(self.oracle)).x);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((SSOracleBehavior.SSOracleMeetWhite self) => self.oracle.room.GetTilePosition(OraclePos(self.oracle)).y);
            }
        }

        private void ThrowOutBehavior_Update(ILContext il)
        {
            var c = new ILCursor(il);

            while (c.TryGotoNext(MoveType.After, x => x.MatchLdcI4(24) || x.MatchLdcI4(28), x => x.MatchLdcI4(33) || x.MatchLdcI4(32)))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((int a, int b, SSOracleBehavior.ThrowOutBehavior self) => Util.FirstShortcut(self.oracle.room).x);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((SSOracleBehavior.ThrowOutBehavior self) => Util.FirstShortcut(self.oracle.room).y);
            }
        }

        public static Vector2 SSSleepoverBehavior_holdPlayerPos(Func<SSOracleBehavior.SSSleepoverBehavior, Vector2> orig, SSOracleBehavior.SSSleepoverBehavior self) =>
            orig(self) - new Vector2(668f, 268f) + OraclePos(self.oracle);
        public static Vector2 SSOracleMeetPurple_holdPlayerPos(Func<SSOracleBehavior.SSOracleMeetPurple, Vector2> orig, SSOracleBehavior.SSOracleMeetPurple self) =>
            orig(self) - new Vector2(668f, 268f) + OraclePos(self.oracle);
        public static Vector2 SSOracleGetGreenNeuron_holdPlayerPos(Func<SSOracleBehavior.SSOracleGetGreenNeuron, Vector2> orig, SSOracleBehavior.SSOracleGetGreenNeuron self) =>
            orig(self) - new Vector2(668f, 268f) + OraclePos(self.oracle);

        #endregion pebbles

        ///////////////////////////////////////////////////////////////////////
        // Rotted Pebbles hooks

        private void SSOracleRotBehavior_Update(ILContext il)
        {
            // Check 2 tiles off of "floor"
            var c = new ILCursor(il);

            c.GotoNext(MoveType.After, x => x.MatchLdcR4(845f));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((float old, SSOracleRotBehavior self) => self.oracle.room.abstractRoom.name == "RM_AI" ? old : self.oracle.arm.cornerPositions[0].y + 40f);
        }

        private bool SSOracleRotBehavior_InSitPosition(Func<SSOracleRotBehavior, bool> orig, SSOracleRotBehavior self)
        {
            var room = self.oracle.room;
            var wantedTile = room.GetTilePosition(OraclePos(self.oracle));
            var oracleTile = room.GetTilePosition(self.oracle.firstChunk.pos);
            return Math.Abs(oracleTile.x - wantedTile.x) < 2 && room.GetTile(oracleTile - new IntVector2(0, 1)).Solid;
        }


        ///////////////////////////////////////////////////////////////////////
        // Collapsed Pebbles hooks

        private Vector2 CLOracleBehavior_RandomRoomPoint(On.MoreSlugcats.CLOracleBehavior.orig_RandomRoomPoint orig, CLOracleBehavior self)
        {
            var rect = Util.FurthestEdges(OraclePos(self.oracle), self.oracle.room);
            return new Vector2(rect.xMin, rect.yMin) + new Vector2(rect.width * Random.value, rect.height * Random.value);
        }


        ///////////////////////////////////////////////////////////////////////
        // Sliver of Straw hooks

        private void STOracleBehavior_ctor(On.MoreSlugcats.STOracleBehavior.orig_ctor orig, STOracleBehavior self, Oracle oracle)
        {
            orig(self, oracle);

            var room = oracle.room;
            self.boxBounds = Util.FurthestEdges(OraclePos(oracle), room);

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
    }
}
