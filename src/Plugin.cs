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
sealed partial class Plugin : BaseUnityPlugin
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

            // Misc fixes
            On.SSOracleBehavior.LockShortcuts += SSOracleBehavior_LockShortcuts;
            IL.PebblesPearl.Update += PebblesPearl_Update;
            IL.SSOracleBehavior.SSOracleMeetWhite.Update += SSOracleMeetWhite_Update;
            IL.SLOracleBehaviorNoMark.Update += SLOracleBehaviorNoMark_Update;

            // Moon revive
            IL.SLOracleWakeUpProcedure.Update += SLOracleWakeUpProcedure_Update;

            // Apply this one last so no rooms get assigned if any previous hooks fail
            On.OverWorld.ctor += OverWorld_ctor;

            Logger.LogDebug("Finished applying hooks :)");
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
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
        foreach (var shortcut in room.shortcuts)
        {
            if (shortcut.LeadingSomewhere)
            {
                entrances.Add(shortcut.StartTile);
            }
        }
        if (entrances.Count == 0)
        {
            throw new Exception("No entrances in room somehow");
        }

        var flyTemplate = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Fly);
        for (int i = 0; i < room.Tiles.Length / 2; i++)
        {
            int x = Random.Range(1, room.Width - 1);
            int y = Random.Range(1, room.Height - 1);
            if (!room.Tiles[x, y].Solid)
            {
                for (int j = 0; j < entrances.Count; j++)
                {
                    var qpc = new QuickPathFinder(new IntVector2(x, y), entrances[j], room.aimap, flyTemplate);
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
        return new Vector2(room.PixelWidth / 2f, room.PixelHeight / 2f);
    }
}
