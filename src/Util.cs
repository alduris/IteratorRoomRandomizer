using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;
using Random = UnityEngine.Random;

namespace OracleRooms
{
    sealed internal class Util
    {
        public static Vector2 RandomAccessiblePoint(Room room)
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

            for (int i = 0; i < room.Tiles.Length / 2; i++)
            {
                int x = Random.Range(1, room.Width - 1);
                int y = Random.Range(1, room.Height - 1);
                if (!room.Tiles[x, y].Solid)
                {
                    for (int j = 0; j < entrances.Count; j++)
                    {
                        if (PointsCanReach(new(x, y), entrances[j], room))
                        {
                            return room.MiddleOfTile(x, y);
                        }
                    }
                }
            }
            return new Vector2(room.PixelWidth / 2f, room.PixelHeight / 2f);
        }

        public static bool PointsCanReach(IntVector2 A, IntVector2 B, Room room)
        {
            var flyTemplate = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Fly);
            var qpc = new QuickPathFinder(A, B, room.aimap, flyTemplate);
            while (qpc.status == 0)
            {
                qpc.Update();
            }
            return qpc.status == 1;
        }
    }
}
