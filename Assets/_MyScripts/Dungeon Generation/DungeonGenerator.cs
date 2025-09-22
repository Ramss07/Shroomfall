using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Bson;
using Unity.VisualScripting;
using UnityEngine;

public class DungeonGenerator
{
    private int dungeonWidth, dungeonLength;
    RoomNode rootNode;
    List<RoomNode> allSpaceNodes = new List<RoomNode>();

    public DungeonGenerator(int width, int length)
    {
        this.dungeonWidth = width;
        this.dungeonLength = length;
    }

    public List<Node> CalculateDungeon(int maxIterations, int roomWidthMin, int roomLengthMin, float bottomCornerModifier, float topCornerModifier, int roomOffset, int corridorWidth)
    {
        BinarySpacePartitioner bsp = new BinarySpacePartitioner(dungeonWidth, dungeonLength);

        allSpaceNodes = bsp.PrepareNodesCollection(maxIterations, roomWidthMin, roomLengthMin);

        // grab all of the children nodes with no children to form individual rooms
        List<Node> roomSpaces = StructureHelper.ExtractLowestLeaves(bsp.RootNode);

        RoomGeneration roomGenerator = new RoomGeneration(maxIterations, roomWidthMin, roomLengthMin);
        List<RoomNode> roomList = roomGenerator.GenerateRooms(roomSpaces, bottomCornerModifier, topCornerModifier, roomOffset);

        CorridorGeneration corridorGenerator = new CorridorGeneration();
        var corridorList = corridorGenerator.GenerateCorridors(allSpaceNodes, corridorWidth);

        return new List<Node>(roomList).Concat(corridorList).ToList();
    }
}
