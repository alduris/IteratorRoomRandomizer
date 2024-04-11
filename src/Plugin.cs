using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MoreSlugcats;
using Random = UnityEngine.Random;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace TestMod;

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

        On.OverWorld.ctor += OverWorld_ctor;
        IL.Room.ReadyForAI += Room_ReadyForAI;
        IL.Oracle.ctor += Oracle_ctor;
        On.SSOracleBehavior.LockShortcuts += SSOracleBehavior_LockShortcuts;
        IL.PebblesPearl.Update += PebblesPearl_Update;
        IL.SSOracleBehavior.SSOracleMeetWhite.Update += SSOracleMeetWhite_Update;
        IL.SLOracleBehaviorNoMark.Update += SLOracleBehaviorNoMark_Update;
    }

    private void SLOracleBehaviorNoMark_Update(ILContext il)
    {
        // Stops a crash when meeting Moon for the first time with the mark
        var c = new ILCursor(il);

        while (c.TryGotoNext(x => x.MatchCallvirt<World>(nameof(World.GetAbstractRoom))))
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((string room, SLOracleBehaviorNoMark self) =>
            {
                var name = self.oracle.room.abstractRoom.name;
                if (name.ToLower().StartsWith("sl"))
                {
                    return room;
                }
                else
                {
                    return name;
                }
            });
            c.Index++;
        }
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
                var pos = self.room.MiddleOfTile(self.room.Tiles.GetLength(0) / 2, self.room.Tiles.GetLength(1) / 2);
                for (int i = 0; i < (int)Math.Sqrt(self.room.Tiles.Length * 2); i++)
                {
                    int x = Random.Range(1, self.room.Tiles.GetLength(0) - 1);
                    int y = Random.Range(1, self.room.Tiles.GetLength(1) - 1);
                    if (!self.room.Tiles[x, y].Solid)
                    {
                        pos = self.room.MiddleOfTile(x, y);
                        break;
                    }
                }

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
                do
                {
                    room = roomList[Random.Range(0, roomList.Length)];
                } while (SlugcatStats.SlugcatStoryRegions(self.game.StoryCharacter).Contains(room.Split(['_'], 1)[0]));
                rooms[room] = new Oracle.OracleID(oracle, false);

                // Log it for debugging purposes
                Logger.LogDebug(oracle + ": " + room);
            }
            itercwt.Add(self, rooms);
        }
    }
}
