using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CustomOracleTx;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using UnityEngine;
using COTx = CustomOracleTx.CustomOracleTx;

namespace OracleRooms
{
    internal static class EmgTxHooks
    {
        private static readonly List<ILHook> mh = [];

        public static void Apply()
        {
            Plugin.Logger.LogDebug("Patching EmgTx");
            try
            {
                // Graphics fix :monksilly: (why does this otherwise only happen while *my mod* is enabled and not normally)
                IL.OracleGraphics.Update += OracleGraphics_Update;
                IL.OracleGraphics.DrawSprites += OracleGraphics_DrawSprites;

                // The most convoluted IL hook in existence. I call this the IL inception hook (in reference to the movie)
                static bool MatchOracleHoox(Type y) => y.Namespace == "CustomOracleTx" && y.Name == "OracleHoox";
                var OracleHoox = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(x => x.GetTypes())
                    .First(x => x.Any(MatchOracleHoox))
                    .First(MatchOracleHoox);
                mh.Add(new ILHook(OracleHoox.GetMethod("Oracle_ctor", BindingFlags.NonPublic | BindingFlags.Static), OracleHoox_Oracle_ctor));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("EmgTx hooks unable to be applied!");
                Plugin.Logger.LogError(ex);
                Unapply();
            }
        }

        public static void Unapply()
        {
            try
            {
                foreach (var hook in mh)
                {
                    hook.Undo();
                    hook.Dispose();
                }

                IL.OracleGraphics.Update -= OracleGraphics_Update;
                IL.OracleGraphics.DrawSprites -= OracleGraphics_DrawSprites;
            }
            finally
            {
                mh.Clear();
            }
        }

        private static void OracleGraphics_DrawSprites(ILContext il)
        {
            // This might break the kill animation in the one hunter expanded scene, need to test that. Either way would rather it not show than break the game
            var c = new ILCursor(il);

            ILLabel target = null;
            c.GotoNext(
                x => x.MatchLdarg(0),
                x => x.MatchCall<OracleGraphics>(typeof(OracleGraphics).GetProperty(nameof(OracleGraphics.IsSaintPebbles)).GetGetMethod().Name), // "get_IsSaintPebbles" is for noobs
                x => x.MatchBrfalse(out target));

            c.GotoPrev(x => x.MatchLdarg(0), x => x.MatchCall<OracleGraphics>(typeof(OracleGraphics).GetProperty(nameof(OracleGraphics.IsPebbles)).GetGetMethod().Name));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((OracleGraphics self) => self.oracle.oracleBehavior is not CustomOracleBehaviour);
            c.Emit(OpCodes.Brfalse, target);
        }

        private static void OracleGraphics_Update(ILContext il)
        {
            var c = new ILCursor(il);

            // Find where it tries to access SSOracleBehavior.working by casing oracleBehavior and stop it from doing that if it isn't actually Pebbles
            // Thank you EmgTx for tricking the game into thinking NSH is Pebbles. Now why the fuck doesn't this show up normally (without my mod)
            c.GotoNext(x => x.MatchLdfld<SSOracleBehavior>(nameof(SSOracleBehavior.working)));
            
            // Find and save break target
            c.GotoNext(MoveType.After, x => x.MatchStfld<LightSource>(nameof(LightSource.setAlpha)));
            Instruction target = c.Next;
            
            // need to do after label so else statement reaches it
            c.GotoPrev(x => x.MatchLdarg(0));
            c.GotoPrev(MoveType.AfterLabel, x => x.MatchLdarg(0));
            // c.GotoPrev(MoveType.AfterLabel, x => x.MatchLdarg(0), x => x.MatchLdfld<OracleGraphics>(nameof(OracleGraphics.lightsource)));

            // Check
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((OracleGraphics self) =>
            {
                var result = self.oracle.oracleBehavior is SSOracleBehavior;
                if (!result)
                {
                    self.lightsource.setAlpha = 1f;
                }
                return result;
            });
            c.Emit(OpCodes.Brfalse, target);
        }

        private static void OracleHoox_Oracle_ctor(ILContext il)
        {
            var c = new ILCursor(il);

            if (c.TryGotoNext(x => x.MatchLdloc(1), x => x.MatchLdsfld(out _), x => x.MatchDup()))
            {
                MethodReference lambdaRef = null;
                c.GotoNext(x => x.MatchLdftn(out lambdaRef));
                mh.Add(new ILHook(lambdaRef.ResolveReflection(), OracleHoox_Oracle_ctor_lambda1));
            }
            else
            {
                Plugin.Logger.LogError("Failed to match target 1 in EmgTx OracleHoox!");
            }

            if (c.TryGotoNext(x => x.MatchLdloc(3), x => x.MatchLdsfld(out _), x => x.MatchDup()))
            {
                MethodReference lambdaRef = null;
                c.GotoNext(x => x.MatchLdftn(out lambdaRef));
                mh.Add(new ILHook(lambdaRef.ResolveReflection(), OracleHoox_Oracle_ctor_lambda2));
            }
            else
            {
                Plugin.Logger.LogError("Failed to match target 2 in EmgTx OracleHoox!");
            }
        }

        private static void OracleHoox_Oracle_ctor_lambda1(ILContext il)
        {
            var c = new ILCursor(il);

            // Modify if-condition with our own custom code
            c.GotoNext(x => x.MatchLdfld<AbstractRoom>(nameof(AbstractRoom.name)));
            c.GotoNext(x => x.MatchStloc(out _));
            c.Emit(OpCodes.Ldarg_1);
            c.Emit(OpCodes.Ldloc_1);
            c.EmitDelegate((bool _, Oracle oracle, COTx self) =>
            {
                var room = oracle.room;
                return Plugin.itercwt.TryGetValue(room.game.overWorld, out var d) && d.TryGetValue(room.abstractRoom.name, out var id) && self.OracleID == id;
            });

            // Spawn body chunks where they should be
            c.GotoNext(MoveType.After, x => x.MatchLdfld<COTx>(nameof(COTx.startPos)));
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate((Vector2 _, Oracle self) => Plugin.OraclePos(self));
        }

        private static void OracleHoox_Oracle_ctor_lambda2(ILContext il)
        {
            var c = new ILCursor(il);

            // Modify if-condition with our own custom code
            c.GotoNext(x => x.MatchLdfld<AbstractRoom>(nameof(AbstractRoom.name)));
            c.GotoNext(x => x.MatchStloc(out _));
            c.Emit(OpCodes.Ldarg_1);
            c.Emit(OpCodes.Ldloc_1);
            c.EmitDelegate((bool _, Oracle oracle, COTx self) =>
            {
                var room = oracle.room;
                return Plugin.itercwt.TryGetValue(room.game.overWorld, out var d) && d.TryGetValue(room.abstractRoom.name, out var id) && self.OracleID == id;
            });
        }
    }
}
