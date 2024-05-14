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

        public static IntVector2 FirstShortcut(Room room)
        {
            foreach (var shortcut in room.shortcuts)
            {
                if (shortcut.shortCutType == ShortcutData.Type.RoomExit)
                {
                    return shortcut.StartTile;
                }
            }
            throw new Exception("No entrances in room somehow");
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

        public static IntRect FurthestEdges(IntVector2 startPoint, Room room)
        {
            var tiles = room.Tiles;
            int x, y;

            // Up
            var up = room.Height - 1;
            x = startPoint.x;
            y = startPoint.y + 1;
            while (y < room.Height)
            {
                if (tiles[x, y].Solid && !tiles[x, y - 1].Solid)
                {
                    up = y - 1;
                }
                y++;
            }

            // Down
            var down = 0;
            x = startPoint.x;
            y = startPoint.y - 1;
            while (y >= 0)
            {
                if (tiles[x, y].Solid && !tiles[x, y + 1].Solid)
                {
                    down = y + 1;
                }
                y--;
            }

            // Right
            var right = room.Width - 1;
            x = startPoint.x + 1;
            y = startPoint.y;
            while (x < room.Width)
            {
                if (tiles[x, y].Solid && !tiles[x - 1, y].Solid)
                {
                    right = x - 1;
                }
                x++;
            }

            // Left
            var left = 0;
            x = startPoint.x - 1;
            y = startPoint.y;
            while (x >= 0)
            {
                if (tiles[x, y].Solid && !tiles[x + 1, y].Solid)
                {
                    left = x + 1;
                }
                x--;
            }

            return new IntRect(left, down, right, up);
        }

        public static Rect FurthestEdges(Vector2 pos, Room room)
        {
            var rect = FurthestEdges(room.GetTilePosition(pos), room);
            var bl = room.MiddleOfTile(rect.left, rect.bottom);
            var tr = room.MiddleOfTile(rect.right, rect.top);

            return new Rect(bl.x, bl.y, tr.x - bl.x, tr.y - bl.y);
        }

        public static void SetCornerPositions(Vector2[] cornerPositions, Oracle oracle)
        {
            var corners = GetCornerPositions(oracle);
            cornerPositions[0] = corners[0];
            cornerPositions[1] = corners[1];
            cornerPositions[2] = corners[2];
            cornerPositions[3] = corners[3];
        }
        public static Vector2[] GetCornerPositions(Oracle oracle)
        {
            var room = oracle.room;
            var rect = FurthestEdges(room.GetTilePosition(Plugin.OraclePos(oracle)), room);
            return [
                room.MiddleOfTile(rect.left, rect.top),
                room.MiddleOfTile(rect.right, rect.top),
                room.MiddleOfTile(rect.right, rect.bottom),
                room.MiddleOfTile(rect.left, rect.bottom)
            ];
        }
    }
}
