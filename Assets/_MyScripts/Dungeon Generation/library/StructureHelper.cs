using System;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public enum RelativePosition {
    Up,
    Down,
    Right,
    Left
}

public class StructureHelper
{

    // node traversal
    public static List<Node> ExtractLowestLeaves(Node parentNode)
    {
        Queue<Node> nodesToCheck = new Queue<Node>();
        List<Node> listToReturn = new List<Node>();

        if (parentNode.ChildrenNodeList.Count == 0)
        {
            return new List<Node> { parentNode }; // if parent node has no children
        }

        foreach (var child in parentNode.ChildrenNodeList)
        {
            nodesToCheck.Enqueue(child); // enqueue all nodes into queue
        }

        while (nodesToCheck.Count > 0)
        {
            var currentNode = nodesToCheck.Dequeue();

            if (currentNode.ChildrenNodeList.Count == 0)
            {
                listToReturn.Add(currentNode); // if the current dequeued node has no children, we add it to list
            }
            else // if not
            {
                foreach (var child in currentNode.ChildrenNodeList)
                {
                    nodesToCheck.Enqueue(child); // we loop for each child again in our current node
                }
            }
        }

        return listToReturn;
    }

    public static Vector2Int GenerateBottomLeftCornerBetween(Vector2Int boundaryLeftPoint, Vector2Int boundaryRightPoint, float pointModifier, int offset)
    {
        /* 
            pointModifier: we do not want to take the whole space of the available space, we want rooms with different size
                            so we tell our algorithm to take our boundaryLeftPoint's (bottomLeftAreaCorner) x value and add to it pointModifier (0.1f) * the width of our room
                            and then randomly choose the value between (0, (pointModifier * the width of our room)) and add it to our x value

                            for y values of boundaryLeftPoint (bottomLeftAreaCorner) and boundaryRightPoint (topRightAreaCorner), its the same thing but length of room instead of width
            
            offset: will tell us, from the start our width is subtracted 2 * offset (1)
                    so we dont have the whole space to use, so we have distance between walls
        */

        int minX = boundaryLeftPoint.x + offset;
        int maxX = boundaryRightPoint.x - offset;
        int minY = boundaryLeftPoint.y + offset;
        int maxY = boundaryRightPoint.y - offset;

        return new Vector2Int(Random.Range(minX, (int)(minX + (maxX - minX) * pointModifier)),
                            Random.Range(minY, (int)(minY + (maxY - minY) * pointModifier)));

    }

    public static Vector2Int GenerateTopRightCornerBetween(Vector2Int boundaryLeftPoint, Vector2Int boundaryRightPoint, float pointModifier, int offset)
    {
        /* 
            same as GenerateBottomLeftCornerBetween but vecter ranges are swapped
        */

        int minX = boundaryLeftPoint.x + offset;
        int maxX = boundaryRightPoint.x - offset;
        int minY = boundaryLeftPoint.y + offset;
        int maxY = boundaryRightPoint.y - offset;

        return new Vector2Int(Random.Range((int)(minX + (maxX - minX) * pointModifier), maxX),
                            Random.Range((int)(minY + (maxY - minY) * pointModifier), maxY));

    }

    public static Vector2Int CalculateMiddlePoint(Vector2Int v1, Vector2Int v2)
    {
        Vector2Int sum = v1 + v2;
        Vector2Int tempVec = sum / 2;
        return new Vector2Int((int)(tempVec.x), (int)(tempVec.y));
    }
}
