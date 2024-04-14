using System;
using System.Runtime.CompilerServices;
using RWCustom;
using UnityEngine;

namespace OracleRooms
{
    partial class Plugin
    {
        private static readonly ConditionalWeakTable<AbstractPhysicalObject, StrongBox<Vector2>> oraclePosCWT = new();
        private static Vector2 OraclePos(Oracle self) => oraclePosCWT.GetValue(self.abstractPhysicalObject, _ => new StrongBox<Vector2>(Util.RandomAccessiblePoint(self.room))).Value;

        private Vector2 OracleBehavior_OracleGetToPos(Func<OracleBehavior, Vector2> orig, OracleBehavior self)
        {
            return OraclePos(self.oracle);
        }

        private Vector2 SLOracleBehavior_OracleGetToPos(Func<SLOracleBehavior, Vector2> orig, SLOracleBehavior self)
        {
            return self.moonActive ? orig(self) : OraclePos(self.oracle);
        }

        private void OracleArm_ctor(On.Oracle.OracleArm.orig_ctor orig, Oracle.OracleArm self, Oracle oracle)
        {
            orig(self, oracle);

            // Set new arm positions
            var room = oracle.room;
            var rect = Util.FarthestEdges(room.GetTilePosition(OraclePos(oracle)), room);

            int up = rect[0], down = rect[1], right = rect[2], left = rect[3];
            self.cornerPositions[0] = room.MiddleOfTile(new IntVector2(left, up));
            self.cornerPositions[1] = room.MiddleOfTile(new IntVector2(right, up));
            self.cornerPositions[2] = room.MiddleOfTile(new IntVector2(right, down));
            self.cornerPositions[3] = room.MiddleOfTile(new IntVector2(left, down));

            // Reset joint positions
            foreach (var joint in self.joints)
            {
                joint.pos = self.BasePos(1f);
                joint.lastPos = joint.pos;
            }
        }
    }
}
