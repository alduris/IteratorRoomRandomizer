﻿using Mono.Cecil.Cil;
using MonoMod.Cil;
using MoreSlugcats;

namespace OracleRooms
{
    partial class Plugin
    {
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
    }
}
