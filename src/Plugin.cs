using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
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
    public static new ManualLogSource Logger;
    private readonly List<Hook> manualHooks = [];

    public void OnEnable()
    {
        Logger = base.Logger;
        On.RainWorld.OnModsInit += OnModsInit;
    }

    private void OnDisable() => Unapply();

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (init) return;
        init = true;

        try
        {
            // Primary functionality
            IL.Room.ReadyForAI += Room_ReadyForAI;
            IL.Oracle.ctor += Oracle_ctor;
            On.OverWorld.ctor += OverWorld_ctor;

            // Misc fixes
            On.SSOracleBehavior.LockShortcuts += SSOracleBehavior_LockShortcuts;
            IL.PebblesPearl.Update += PebblesPearl_Update;
            IL.SSOracleBehavior.SSOracleMeetWhite.Update += SSOracleMeetWhite_Update;
            IL.SLOracleBehaviorNoMark.Update += SLOracleBehaviorNoMark_Update;
            On.MoreSlugcats.SpearMasterPearl.NewRoom += SpearMasterPearl_NewRoom;
            IL.Oracle.ctor += Oracle_ctor1;
            manualHooks.Add(new Hook(typeof(OracleGraphics).GetProperty(nameof(OracleGraphics.IsRottedPebbles)).GetGetMethod(), OracleGraphics_IsRottedPebbles));
            On.Oracle.ctor += Oracle_ctor2;

            // Bugfix and position unhardcoding for moon revive in Hunter
            IL.SLOracleWakeUpProcedure.Update += SLOracleWakeUpProcedure_Update;

            // General position unhardcoding fixes
            On.Oracle.OracleArm.ctor += OracleArm_ctor;
            
            manualHooks.Add(new Hook(typeof(SLOracleBehavior).GetProperty(nameof(OracleBehavior.OracleGetToPos)).GetGetMethod(), SLOracleBehavior_OracleGetToPos));
            manualHooks.Add(new Hook(typeof(SSOracleRotBehavior).GetProperty(nameof(OracleBehavior.OracleGetToPos)).GetGetMethod(), OracleBehavior_OracleGetToPos));
            manualHooks.Add(new Hook(typeof(CLOracleBehavior).GetProperty(nameof(OracleBehavior.OracleGetToPos)).GetGetMethod(), OracleBehavior_OracleGetToPos));

            On.SLOracleBehavior.RandomRoomPoint += SLOracleBehavior_RandomRoomPoint;
            On.SLOracleBehavior.ClampMediaPos += SLOracleBehavior_ClampMediaPos;
            On.SLOracleBehavior.BasePosScore += SLOracleBehavior_BasePosScore;
            IL.SLOracleBehavior.Move += SLOracleBehavior_Move;
            IL.SLOracleBehavior.Update += SLOracleBehavior_Update;
            manualHooks.Add(new Hook(typeof(SLOracleBehavior).GetProperty(nameof(SLOracleBehavior.InSitPosition)).GetGetMethod(), SLOracleBehavior_InSitPosition));

            On.SSOracleBehavior.BasePosScore += SSOracleBehavior_BasePosScore;
            IL.SSOracleBehavior.Move += SSOracleBehavior_Move;
            IL.SSOracleBehavior.Update += SSOracleBehavior_Update;
            IL.SSOracleBehavior.SSOracleMeetPurple.Update += SSOracleMeetPurple_Update;
            IL.SSOracleBehavior.SSOracleMeetWhite.Update += SSOracleMeetWhite_Update1;
            IL.SSOracleBehavior.ThrowOutBehavior.Update += ThrowOutBehavior_Update;
            manualHooks.Add(new Hook(typeof(SSOracleBehavior.SSSleepoverBehavior).GetProperty(
                    nameof(SSOracleBehavior.SSSleepoverBehavior.holdPlayerPos),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .GetGetMethod(true),
                SSSleepoverBehavior_holdPlayerPos));
            manualHooks.Add(new Hook(typeof(SSOracleBehavior.SSOracleMeetPurple).GetProperty(
                    nameof(SSOracleBehavior.SSOracleMeetPurple.holdPlayerPos),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .GetGetMethod(true),
                SSOracleMeetPurple_holdPlayerPos));
            manualHooks.Add(new Hook(typeof(SSOracleBehavior.SSOracleGetGreenNeuron).GetProperty(
                    nameof(SSOracleBehavior.SSOracleGetGreenNeuron.holdPlayerPos),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .GetGetMethod(true),
                SSOracleGetGreenNeuron_holdPlayerPos));

            IL.MoreSlugcats.SSOracleRotBehavior.Update += SSOracleRotBehavior_Update;
            manualHooks.Add(new Hook(typeof(SSOracleRotBehavior).GetProperty(nameof(SSOracleRotBehavior.InSitPosition)).GetGetMethod(), SSOracleRotBehavior_InSitPosition));

            On.MoreSlugcats.CLOracleBehavior.RandomRoomPoint += CLOracleBehavior_RandomRoomPoint;

            On.MoreSlugcats.STOracleBehavior.ctor += STOracleBehavior_ctor;

            // "Compatibility" (aggressive)
            if (ModManager.ActiveMods.Any(x => x.name.Equals("iteratorkit", StringComparison.InvariantCultureIgnoreCase)))
            {
                // IteratorKitHooks.Apply();
            }

            if (ModManager.ActiveMods.Any(x => x.id.Equals("emgtx", StringComparison.InvariantCultureIgnoreCase)))
            {
                EmgTxHooks.Apply();
            }

            if (ModManager.ActiveMods.Any(x => x.id.Equals("nyctophobia", StringComparison.InvariantCultureIgnoreCase)))
            {
                NyctophobiaHooks.Apply();
            }

            // Done!
            Logger.LogDebug("Finished applying hooks :)");
        }
        catch (Exception e)
        {
            Logger.LogError(e);
            Unapply();
        }
    }

    private void Unapply()
    {
        try
        {
            foreach (var hook in manualHooks)
            {
                hook.Undo();
                hook.Dispose();
            }
            manualHooks.Clear();

            IL.Room.ReadyForAI -= Room_ReadyForAI;
            IL.Oracle.ctor -= Oracle_ctor;
            On.OverWorld.ctor -= OverWorld_ctor;

            On.SSOracleBehavior.LockShortcuts -= SSOracleBehavior_LockShortcuts;
            IL.PebblesPearl.Update -= PebblesPearl_Update;
            IL.SSOracleBehavior.SSOracleMeetWhite.Update -= SSOracleMeetWhite_Update;
            IL.SLOracleBehaviorNoMark.Update -= SLOracleBehaviorNoMark_Update;
            On.MoreSlugcats.SpearMasterPearl.NewRoom -= SpearMasterPearl_NewRoom;
            IL.Oracle.ctor -= Oracle_ctor1;
            On.Oracle.ctor -= Oracle_ctor2;

            IL.SLOracleWakeUpProcedure.Update -= SLOracleWakeUpProcedure_Update;

            On.Oracle.OracleArm.ctor -= OracleArm_ctor;
            On.SLOracleBehavior.RandomRoomPoint -= SLOracleBehavior_RandomRoomPoint;
            On.SLOracleBehavior.ClampMediaPos -= SLOracleBehavior_ClampMediaPos;
            On.SLOracleBehavior.BasePosScore -= SLOracleBehavior_BasePosScore;
            IL.SLOracleBehavior.Move -= SLOracleBehavior_Move;
            IL.SLOracleBehavior.Update -= SLOracleBehavior_Update;
            On.SSOracleBehavior.BasePosScore -= SSOracleBehavior_BasePosScore;
            IL.SSOracleBehavior.Move -= SSOracleBehavior_Move;
            IL.SSOracleBehavior.Update -= SSOracleBehavior_Update;
            IL.SSOracleBehavior.SSOracleMeetPurple.Update -= SSOracleMeetPurple_Update;
            IL.SSOracleBehavior.SSOracleMeetWhite.Update -= SSOracleMeetWhite_Update1;
            IL.SSOracleBehavior.ThrowOutBehavior.Update -= ThrowOutBehavior_Update;
            IL.MoreSlugcats.SSOracleRotBehavior.Update -= SSOracleRotBehavior_Update;
            On.MoreSlugcats.CLOracleBehavior.RandomRoomPoint -= CLOracleBehavior_RandomRoomPoint;
            On.MoreSlugcats.STOracleBehavior.ctor -= STOracleBehavior_ctor;

            if (ModManager.ActiveMods.Any(x => x.id.Equals("iteratorkit", StringComparison.InvariantCultureIgnoreCase)))
            {
                IteratorKitHooks.Unapply();
            }

            if (ModManager.ActiveMods.Any(x => x.id.Equals("emgtx", StringComparison.InvariantCultureIgnoreCase)))
            {
                EmgTxHooks.Unapply();
            }

            if (ModManager.ActiveMods.Any(x => x.id.Equals("nyctophobia", StringComparison.InvariantCultureIgnoreCase)))
            {
                NyctophobiaHooks.Unapply();
            }
        }
        catch (Exception e)
        {
            // if an IL hook didn't apply correctly, it would make sense if it reached here
            Logger.LogError("Could not unapply hooks either (note that if it's the same method erroring, that's normal and fine)");
            Logger.LogError(e);
        }
    }

    internal static readonly ConditionalWeakTable<OverWorld, Dictionary<string, Oracle.OracleID>> itercwt = new();

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
                var pos = OraclePos(self); // Util.RandomAccessiblePoint(self.room);

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
        c.EmitDelegate((bool orig, Room self) => (self?.game?.overWorld != null && itercwt.TryGetValue(self.game.overWorld, out var d)) ? d.ContainsKey(self.abstractRoom.name) : orig);
        c.Emit(OpCodes.Stloc_0);

        // Add a new condition to bool
        c.GotoNext(MoveType.AfterLabel, x => x.MatchLdloc(0));
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate((Room self) => self?.game?.overWorld == null || !itercwt.TryGetValue(self.game.overWorld, out var d) || self.abstractRoom.name == null || d.ContainsKey(self.abstractRoom.name));
        c.Emit(OpCodes.And);
    }

    private void OverWorld_ctor(On.OverWorld.orig_ctor orig, OverWorld self, RainWorldGame game)
    {
        orig(self, game);
        try
        {
            if (game.IsStorySession)
            {
                var roomList = RainWorld.roomNameToIndex.Keys.ToArray();
                Dictionary<string, Oracle.OracleID> rooms = [];
                HashSet<string> oracles = [.. Oracle.OracleID.values.entries];

                // Deal with stuff
                if (ModManager.ActiveMods.Any(x => x.id.Equals("myr.chasing_wind", StringComparison.InvariantCultureIgnoreCase)))
                {
                    oracles.Add("CW");
                }
                if (ModManager.ActiveMods.Any(x => x.id.Equals("Quaeledy.hunterexpansion", StringComparison.InvariantCultureIgnoreCase)))
                {
                    oracles.Add("NSH");
                    oracles.Remove("SRS"); // unfinished, don't bother
                }

                foreach (var oracle in oracles)
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
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

}
