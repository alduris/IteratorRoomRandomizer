using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MoreSlugcats;
using RWCustom;
using UnityEngine;
using Random = UnityEngine.Random;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace OracleRooms;

[BepInPlugin("alduris.oraclerooms", "Iterator Room Randomizer", "1.0")]
sealed class Plugin : BaseUnityPlugin
{
    bool init;

    public void OnEnable()
    {
        On.RainWorld.OnModsInit += OnModsInit;
    }

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (init) return;
        init = true;

        try
        {
            IL.Room.ReadyForAI += Room_ReadyForAI;
            IL.Oracle.ctor += Oracle_ctor;
            On.SSOracleBehavior.LockShortcuts += SSOracleBehavior_LockShortcuts;
            IL.PebblesPearl.Update += PebblesPearl_Update;
            IL.SSOracleBehavior.SSOracleMeetWhite.Update += SSOracleMeetWhite_Update;
            IL.SLOracleBehaviorNoMark.Update += SLOracleBehaviorNoMark_Update;
            IL.SLOracleWakeUpProcedure.Update += SLOracleWakeUpProcedure_Update;
            On.OverWorld.ctor += OverWorld_ctor; // apply this one last so that no rooms get assigned if anything fails
            Logger.LogDebug("Finished applying hooks :)");
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

    private static readonly ConditionalWeakTable<SLOracleWakeUpProcedure, StrongBox<Vector2>> moonWakeupPosCWT = new();
    private static Vector2 MoonWakeupPos(SLOracleWakeUpProcedure self) => moonWakeupPosCWT.GetValue(self, _ =>
    {
        var room = self.room;
        var tilePos = room.GetTilePosition(new(1511f, 448f));

        var flyTemplate = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Fly);
        if (room.Width < tilePos.x || room.Height < tilePos.y || room.Tiles[tilePos.x, tilePos.y].Solid)
        {
            tilePos = room.GetTilePosition(self.SLOracle.bodyChunks[0].pos);
            for (int i = 0; i < 100; i++)
            {
                var testPoint = room.GetTilePosition(RandomAccessiblePoint(room));
                var qpc = new QuickPathFinder(testPoint, tilePos, room.aimap, flyTemplate);
                while (qpc.status == 0)
                {
                    qpc.Update();
                }
                if (qpc.status != -1)
                {
                    tilePos = testPoint;
                    break;
                }
            }
            return new StrongBox<Vector2>(room.MiddleOfTile(tilePos));
        }
        return new StrongBox<Vector2>(new(1511, 448));
    }).Value;

    private void SLOracleWakeUpProcedure_Update(ILContext il)
    {
        // Fixes a number of things:
        // 1. A big lagspike if water doesn't exist in the room (repeated errors yet somehow it manages to continue??)
        // 2. Unhardcodes some positions to make it so the neuron can actually fly to where it's supposed to and moon to not teleport into another solid object

        var c = new ILCursor(il);

        // UNHARDCODE *SOME* POSITIONS
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
    }

    private void SLOracleBehaviorNoMark_Update(ILContext il)
    {
        // Stops a crash when meeting Moon without the mark
        var c = new ILCursor(il);

        // SL_AI doesn't exist outside of shoreline
        c.GotoNext(MoveType.After, x => x.MatchLdstr("SL_AI"), x => x.MatchCall<string>("op_Inequality"));
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate((SLOracleBehaviorNoMark self) =>
        {
            return self.lockedOverseer.parent.Room.name.ToUpperInvariant().StartsWith("SL");
        });
        c.Emit(OpCodes.And);

        // SL_A15 also doesn't exist outside of shoreline
        ILLabel brTo = null;
        c.GotoNext(MoveType.Before, x => x.MatchLdarg(0), x => x.MatchLdfld<SLOracleBehaviorNoMark>(nameof(SLOracleBehaviorNoMark.lockedOverseer)), x => x.MatchBrtrue(out brTo));
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate((SLOracleBehaviorNoMark self) =>
        {
            return self.oracle.room.abstractRoom.name.ToUpperInvariant().StartsWith("SL");
        });
        c.Emit(OpCodes.Brfalse, brTo);
    }

    private void SSOracleMeetWhite_Update(ILContext il)
    {
        // Fixes a crash for spearmaster moon
        var c = new ILCursor(il);

        while (c.TryGotoNext(x => x.MatchLdfld<PlayerGraphics>(nameof(PlayerGraphics.bodyPearl))))
        {
            c.GotoNext(MoveType.After, x => x.MatchStfld(out _));
            var next = c.Next;
            c.GotoPrev(x => x.MatchLdarg(0), x => x.MatchCall<SSOracleBehavior.SubBehavior>("get_player"));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((SSOracleBehavior.SSOracleMeetWhite self) => ModManager.MSC && self.player.slugcatStats.name == MoreSlugcatsEnums.SlugcatStatsName.Spear);
            c.Emit(OpCodes.Brfalse, next);
            c.GotoNext(x => x.MatchStfld(out _));
        }
    }

    private void PebblesPearl_Update(ILContext il)
    {
        // Fixes a crash if iterator doesn't have a halo (aka Looks to the Moon)
        var c = new ILCursor(il);

        c.GotoNext(x => x.MatchLdfld<OracleGraphics>(nameof(OracleGraphics.halo)));
        ILLabel brto = null;
        c.GotoPrev(MoveType.After, x => x.MatchBrfalse(out brto));
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate((PebblesPearl self) => (self.orbitObj.graphicsModule as OracleGraphics).halo != null);
        c.Emit(OpCodes.Brfalse, brto);
    }

    private void SSOracleBehavior_LockShortcuts(On.SSOracleBehavior.orig_LockShortcuts orig, SSOracleBehavior self)
    {
        // Stops Pebbles and Spear's LTTM from trapping us in the room while they whine about our repeated visits
        orig(self);
        self.UnlockShortcuts();
    }

    private void Oracle_ctor(ILContext il)
    {
        var c = new ILCursor(il);

        // Add our own logic to override hardcoded stuff
        c.GotoNext(x => x.MatchStfld<PhysicalObject>(nameof(PhysicalObject.bodyChunkConnections)));
        c.GotoPrev(x => x.MatchLdarg(0));
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate((Oracle self) =>
        {
            if (itercwt.TryGetValue(self.room.game.overWorld, out var d) && d.ContainsKey(self.room.abstractRoom.name))
            {
                // Set our id
                self.ID = d[self.room.abstractRoom.name];

                //Set position to some random point in the room that isn't a solid
                var pos = RandomAccessiblePoint(self.room);
                /*var pos = self.room.MiddleOfTile(self.room.Tiles.GetLength(0) / 2, self.room.Tiles.GetLength(1) / 2);
                for (int i = 0; i < (int)Math.Sqrt(self.room.Tiles.Length * 2); i++)
                {
                    int x = Random.Range(1, self.room.Tiles.GetLength(0) - 1);
                    int y = Random.Range(1, self.room.Tiles.GetLength(1) - 1);
                    if (!self.room.Tiles[x, y].Solid)
                    {
                        pos = self.room.MiddleOfTile(x, y);
                        break;
                    }
                }*/

                // Set all body chunks to this point
                foreach (var chunk in self.bodyChunks)
                {
                    chunk.pos = pos;
                    chunk.lastPos = pos;
                    chunk.lastLastPos = pos;
                }
            }
        });
    }

    private void Room_ReadyForAI(ILContext il)
    {
        var c = new ILCursor(il);

        // Override Room.ReadyForAI if iterator can spawn with whether or not an iterator is assigned to this room
        c.GotoNext(MoveType.After, x => x.MatchStloc(0));
        c.Emit(OpCodes.Ldloc_0);
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate((bool temp, Room self) => itercwt.TryGetValue(self.game.overWorld, out var d) ? d.ContainsKey(self.abstractRoom.name) : temp);
        c.Emit(OpCodes.Stloc_0);

        // Add a new condition to bool
        c.GotoNext(MoveType.AfterLabel, x => x.MatchLdloc(0));
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate((Room self) => !itercwt.TryGetValue(self.game.overWorld, out var d) || d.ContainsKey(self.abstractRoom.name));
        c.Emit(OpCodes.And);
    }

    private static readonly ConditionalWeakTable<OverWorld, Dictionary<string, Oracle.OracleID>> itercwt = new();
    private void OverWorld_ctor(On.OverWorld.orig_ctor orig, OverWorld self, RainWorldGame game)
    {
        orig(self, game);
        if (game.IsStorySession)
        {
            var roomList = RainWorld.roomNameToIndex.Keys.ToArray();
            Dictionary<string, Oracle.OracleID> rooms = [];
            foreach (var oracle in Oracle.OracleID.values.entries)
            {
                if (oracle.ToLower().Contains("cutscene")) continue;

                // Get a random room in a story region (so that we can actually access the iterator)
                string room;
                int i = 0;
                do
                {
                    room = roomList[Random.Range(0, roomList.Length)];
                }
                while (
                    // No offscreen dens
                    room.ToUpperInvariant().Contains("OFFSCREEN") ||
                    // No non-story regions (though if we run out of options, just accept whatever we have)
                    (i++ < roomList.Length / 4 && !SlugcatStats.SlugcatStoryRegions(self.game.StoryCharacter).Any(x => room.ToUpperInvariant().StartsWith(x.ToUpperInvariant())))
                );
                rooms[room] = new Oracle.OracleID(oracle, false);

                // Log it for debugging purposes
                Logger.LogDebug(oracle + ": " + room);
            }
            itercwt.Add(self, rooms);
        }
    }
    private static Vector2 RandomAccessiblePoint(Room room)
    {
        var entrances = new List<IntVector2>();
        for (int i = 1; i < room.Width - 1; i++)
        {
            for (int j = 1; j < room.Height - 1; j++)
            {
                if (room.Tiles[i, j].Terrain == Room.Tile.TerrainType.ShortcutEntrance)
                {
                    entrances.Add(new IntVector2(i, j));
                }
            }
        }
        if (entrances.Count == 0)
        {
            throw new Exception("No entrances in room somehow");
        }

        var flyTemplate = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Fly);
        while (true)
        {
            int x = Random.Range(1, room.Width - 1);
            int y = Random.Range(1, room.Height - 1);
            if (!room.Tiles[x, y].Solid)
            {
                for (int i = 0; i < entrances.Count; i++)
                {
                    var qpc = new QuickPathFinder(new IntVector2(x, y), entrances[i], room.aimap, flyTemplate);
                    while (qpc.status == 0)
                    {
                        qpc.Update();
                    }
                    if (qpc.status != -1)
                    {
                        return room.MiddleOfTile(x, y);
                    }
                }
            }
        }
    }
}
