using System.Runtime.CompilerServices;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using UnityEngine;
using Random = UnityEngine.Random;

namespace OracleRooms
{
    partial class Plugin
    {

        private static readonly ConditionalWeakTable<SLOracleWakeUpProcedure, StrongBox<Vector2>> moonWakeupPosCWT = new();
        private static Vector2 MoonWakeupPos(SLOracleWakeUpProcedure self) => moonWakeupPosCWT.GetValue(self, _ =>
        {
            var room = self.room;
            var tilePos = room.GetTilePosition(new(1511f, 448f));

            if (room.Width < tilePos.x || room.Height < tilePos.y || room.Tiles[tilePos.x, tilePos.y].Solid)
            {
                tilePos = room.GetTilePosition(self.SLOracle.bodyChunks[0].pos);
                for (int i = 0; i < 100; i++)
                {
                    var testPos = room.GetTilePosition(Util.RandomAccessiblePoint(room));
                    if (CanPathfindToMoon(self, testPos))
                    {
                        tilePos = testPos;
                        break;
                    }
                }
                return new StrongBox<Vector2>(room.MiddleOfTile(tilePos));
            }
            return new StrongBox<Vector2>(new(1511, 448));
        }).Value;

        private static bool CanPathfindToMoon(SLOracleWakeUpProcedure self, IntVector2 testPos) =>
            Util.PointsCanReach(testPos, self.room.GetTilePosition(self.SLOracle.firstChunk.pos), self.room);

        private void SLOracleWakeUpProcedure_Update(ILContext il)
        {
            // Fixes a number of things:
            // 1. A big lagspike if water doesn't exist in the room (repeated errors yet somehow it manages to continue??)
            // 2. Unhardcodes some positions to make it so the neuron can actually fly to where it's supposed to and moon to not teleport into another solid object

            var c = new ILCursor(il);

            // UNHARDCODE *SOME* POSITIONS (all of the following are where the green neuron flies)
            int loc = 0;
            c.GotoNext(MoveType.After, x => x.MatchLdloca(out loc), x => x.MatchLdcR4(1511f), x => x.MatchLdcR4(448f), x => x.MatchCall<Vector2>(".ctor"));
            c.Emit(OpCodes.Ldloc, loc);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Vector2 orig, SLOracleWakeUpProcedure self) =>
            {
                if (itercwt.TryGetValue(self.room.game.overWorld, out var dict) && dict.Count > 0)
                {
                    return MoonWakeupPos(self);
                }
                return orig;
            });
            c.Emit(OpCodes.Stloc, loc);

            c.GotoNext(MoveType.After, x => x.MatchLdcR4(1511f), x => x.MatchLdcR4(448f), x => x.MatchNewobj<Vector2>());
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Vector2 orig, SLOracleWakeUpProcedure self) =>
            {
                if (itercwt.TryGetValue(self.room.game.overWorld, out var dict) && dict.Count > 0)
                {
                    return MoonWakeupPos(self);
                }
                return orig;
            });


            // FIX BIG LAGSPIKE (do this by wrapping an if-statement that checks if self.room.waterObject is null and breaking around if true)
            c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt<Water>(nameof(Water.GeneralUpsetSurface)));
            var brTo = c.Next;

            c.GotoPrev(MoveType.Before, x => x.MatchLdarg(0));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((SLOracleWakeUpProcedure self) => self.room.waterObject is null);
            c.Emit(OpCodes.Brtrue, brTo);


            // UNHARDCODE 2 ELECTRIC BOOGALOO
            // Moon float to position
            for (int i = 0; i < 3; i++)
            {
                c.GotoNext(MoveType.After, x => x.MatchLdcR4(1649f), x => x.MatchLdcR4(323f), x => x.MatchNewobj<Vector2>());
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((Vector2 orig, SLOracleWakeUpProcedure self) =>
                {
                    if (MoonWakeupPos(self) == new Vector2(1511f, 448f) && !self.room.GetTile(orig).Solid) return orig;
                    return MoonWakeupPos(self);
                });
            }

            // Symbols
            c.GotoNext(MoveType.After, x => x.MatchLdcR4(1653f), x => x.MatchLdcR4(450f), x => x.MatchNewobj<Vector2>());
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Vector2 orig, SLOracleWakeUpProcedure self) =>
            {
                if (MoonWakeupPos(self) == new Vector2(1511f, 448f) && !self.room.GetTile(orig).Solid) return orig;
                return self.room.MiddleOfTile(MoonWakeupPos(self) + new Vector2(0, 130f));
            });

            // Neuron tile spawn positions
            for (int i = 0; i < 4; i++)
            {
                c.GotoNext(MoveType.After, x => x.MatchNewobj<IntVector2>());
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(NeuronSpawnPos);
            }
        }

        private IntVector2 NeuronSpawnPos(IntVector2 origPos, SLOracleWakeUpProcedure self)
        {
            var room = self.room;
            var oraclePos = room.GetTilePosition(self.SLOracle.firstChunk.pos);
            for (int i = 0; i < 30; i++)
            {
                int offset = Random.Range(-10, 10);
                int x = oraclePos.x + offset;
                if (x < 0 || x >= room.TileWidth) continue;

                int y = oraclePos.y;
                if (!CanPathfindToMoon(self, new IntVector2(x, oraclePos.y)))
                {
                    // We can't pathfind to moon from here, find an edge tile to spawn from

                    while (y > 0 && (room.GetTile(x, y).Solid || !room.GetTile(x, y + 1).Solid || !CanPathfindToMoon(self, new IntVector2(x, y))))
                    {
                        y--;
                    }
                    if (y == 0)
                    {
                        while (y < room.TileHeight - 1 && (room.GetTile(x, y).Solid || !room.GetTile(x, y - 1).Solid || !CanPathfindToMoon(self, new IntVector2(x, y))))
                        {
                            y++;
                        }
                        if (y != room.TileHeight)
                        {
                            return new IntVector2(x, y);
                        }
                    }
                    else
                    {
                        return new IntVector2(x, y);
                    }
                }
                else
                {
                    // We're in a non-solid tile, find a solid tile to spawn from
                    if (Random.value > 0.1f)
                    {
                        // Come from floor
                        while (y > 0 && !room.GetTile(x, y).Solid)
                        {
                            y--;
                        }
                    }
                    else
                    {
                        // Come from ceiling
                        while (y < room.TileHeight - 1 && !room.GetTile(x, y).Solid)
                        {
                            y++;
                        }
                    }
                }

                return new IntVector2(x, y);
            }
            return origPos;
        }
    }
}
