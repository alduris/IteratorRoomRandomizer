/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Nyctophobia;
using orig_ctor = On.Oracle.orig_ctor;

namespace OracleRooms
{
    internal static class NyctophobiaHooks
    {
        private static readonly List<Hook> mh = [];

        public static void Apply()
        {
            Plugin.Logger.LogDebug("Patching Nyctophobia");
            try
            {
                mh.Add(new Hook(typeof(ESPHooks).GetMethod("Oracle_ctor", BindingFlags.NonPublic | BindingFlags.Static), ESPHooks_Oracle_ctor));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Nyctophobia hooks unable to be applied!");
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

        private static void ESPHooks_Oracle_ctor(Action<orig_ctor, Oracle, AbstractPhysicalObject, Room> realOrig, orig_ctor orig, Oracle self, AbstractPhysicalObject abstractPhysicalObject, Room room)
        {
            bool isESP = Plugin.itercwt.TryGetValue(room.game.overWorld, out var d) && d.TryGetValue(room.abstractRoom.name, out var id) && id == NTEnums.Iterator.ESP;
            string origName = room.abstractRoom.name;

            if (isESP) room.abstractRoom.name = "DD_AI";
            realOrig(orig, self, abstractPhysicalObject, room);
            if (isESP) room.abstractRoom.name = origName;
        }
    }
}
*/