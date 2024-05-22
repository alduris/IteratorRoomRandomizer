using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

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
                IL.OracleGraphics.DrawSprites += Util.DebugHook;

                // The most convoluted IL hook in existence
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
                IL.OracleGraphics.Update -= OracleGraphics_Update;

                foreach (var hook in mh)
                {
                    hook.Undo();
                    hook.Dispose();
                }
            }
            finally
            {
                mh.Clear();
            }
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
                Plugin.Logger.LogDebug(result);
                return result;
            });
            c.Emit(OpCodes.Brfalse, target);
        }

        private static void OracleHoox_Oracle_ctor(ILContext il)
        {
            var c = new ILCursor(il);

            if (c.TryGotoNext(x => x.MatchLdloc(3), x => x.MatchLdsfld(out _), x => x.MatchDup()))
            {
                MethodReference lambdaRef = null;
                c.GotoNext(x => x.MatchLdftn(out lambdaRef));
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var hook = new ILHook(lambdaRef.DeclaringType.ResolveReflection().GetMethod(lambdaRef.Name, flags), OracleHoox_Oracle_ctor_lambda);
                mh.Add(hook);
                if (!hook.IsApplied) hook.Apply(); // idk if I need this
            }
            else
            {
                Plugin.Logger.LogError("Failed to match target in EmgTx OracleHoox!");
            }
        }

        private static void OracleHoox_Oracle_ctor_lambda(ILContext il)
        {
            var c = new ILCursor(il);

            c.GotoNext(x => x.MatchLdfld<AbstractRoom>(nameof(AbstractRoom.name)));
            c.GotoNext(x => x.MatchStloc(out _));
            c.Emit(OpCodes.Ldarg_1);
            c.Emit(OpCodes.Ldarg_2);
            c.EmitDelegate((bool _, Oracle oracle, Room room) =>
            {
                return Plugin.itercwt.TryGetValue(room.game.overWorld, out var d) && d.ContainsKey(room.abstractRoom.name) && d[room.abstractRoom.name] == oracle.ID;
            });
        }
    }
}
