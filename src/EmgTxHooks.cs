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
