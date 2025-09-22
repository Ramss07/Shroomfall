using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CorridorNode : Node
{
    private Node structure1;
    private Node structure2;
    private int corridorWidth;
    private int modifierDistance = 1; // distance from a wall we want to preserve

    public CorridorNode(Node node1, Node node2, int corridorWidth) : base(null)
    {
        this.structure1 = node1;
        this.structure2 = node2;
        this.corridorWidth = corridorWidth;

        GenerateCorridor();
    }

    private void GenerateCorridor()
    {
        var relativePosition = CheckPosition();

        switch (relativePosition)
        {
            case RelativePosition.Up:
                ProcessUpDown(this.structure1, this.structure2);
                break;
            case RelativePosition.Down:
                ProcessUpDown(this.structure2, this.structure1);
                break;
            case RelativePosition.Right:
                ProcessRightLeft(this.structure1, this.structure2);
                break;
            case RelativePosition.Left:
                ProcessRightLeft(this.structure2, this.structure1);
                break;
            default:
                break;
        }
    }

    private void ProcessRightLeft(Node structure1, Node structure2)
    {
        Node left = null;
        Node right = null;
        List<Node> leftChildren = StructureHelper.ExtractLowestLeaves(structure1);
        List<Node> rightChildren = StructureHelper.ExtractLowestLeaves(structure2);

        var sortedLeft = leftChildren.OrderByDescending(child => child.TopRightAreaCorner.x).ToList();

        // check if left structure has no children
        if (sortedLeft.Count == 1)
        {
            left = sortedLeft[0];
        }
        else
        {
            int maxX = sortedLeft[0].TopRightAreaCorner.x;

            sortedLeft = sortedLeft.Where(children => Math.Abs(maxX - children.TopRightAreaCorner.x) < 10).ToList();

            int index = UnityEngine.Random.Range(0, sortedLeft.Count); // randomly choose which index we take

            left = sortedLeft[index];
        }

        // do the same with the right
        var possibleNeighborsRight = rightChildren.Where(child => GetValidY(left.TopRightAreaCorner, left.BottomRightAreaCorner, child.TopLeftAreaCorner, child.BottomLeftAreaCorner) != -1).ToList();

        if (possibleNeighborsRight.Count <= 0)
        {
            right = structure2;
        }
        else
        {
            right = possibleNeighborsRight[0];
        }

        int y = GetValidY(left.TopLeftAreaCorner, left.BottomLeftAreaCorner, right.TopLeftAreaCorner, right.BottomLeftAreaCorner);

        while (y == -1 && sortedLeft.Count > 1)
        {
            sortedLeft = sortedLeft.Where(child => child.TopLeftAreaCorner.y != left.TopLeftAreaCorner.y).ToList();
            left = sortedLeft[0];
            y = GetValidY(left.TopLeftAreaCorner, left.BottomLeftAreaCorner, right.TopLeftAreaCorner, right.BottomLeftAreaCorner);
        }

        BottomLeftAreaCorner = new Vector2Int(left.BottomRightAreaCorner.x, y);
        TopRightAreaCorner = new Vector2Int(right.TopLeftAreaCorner.x, y + this.corridorWidth);
    }

    private int GetValidY(Vector2Int leftUp, Vector2Int leftDown, Vector2Int rightUp, Vector2Int rightDown)
    {
        // case if left node is bigger on y's than the right node
        if (rightUp.y >= leftUp.y && leftDown.y >= rightDown.y)
        {
            return StructureHelper.CalculateMiddlePoint(leftDown + new Vector2Int(0, modifierDistance), leftUp - new Vector2Int(0, modifierDistance + this.corridorWidth)).y;
        }

        // reverse case
        if (rightUp.y <= leftUp.y && leftDown.y <= rightDown.y)
        {
            return StructureHelper.CalculateMiddlePoint(rightDown + new Vector2Int(0, modifierDistance), rightUp - new Vector2Int(0, modifierDistance + this.corridorWidth)).y;
        }

        // check if the bottom point of left node is inside the right node
        if (leftUp.y >= rightDown.y && leftUp.y <= rightUp.y)
        {
            return StructureHelper.CalculateMiddlePoint(rightDown + new Vector2Int(0, modifierDistance), leftUp - new Vector2Int(0, modifierDistance)).y;
        }

        // reverse case
        if (leftDown.y >= rightDown.y && leftDown.y <= rightUp.y)
        {
            return StructureHelper.CalculateMiddlePoint(leftDown + new Vector2Int(0, modifierDistance), rightUp - new Vector2Int(0, modifierDistance + this.corridorWidth)).y;
        }

        return -1;
    }

    private void ProcessUpDown(Node structure1, Node structure2)
    {
        Node bottom = null;
        Node top = null;

        List<Node> bottomChildren = StructureHelper.ExtractLowestLeaves(structure1);
        List<Node> topChildren = StructureHelper.ExtractLowestLeaves(structure2);

        var sortedBottom = bottomChildren.OrderByDescending(child => child.TopRightAreaCorner.y).ToList();

        if (sortedBottom.Count == 1)
        {
            bottom = bottomChildren[0];
        }
        else
        {
            int maxY = sortedBottom[0].TopLeftAreaCorner.y;
            sortedBottom = sortedBottom.Where(child => Mathf.Abs(maxY - child.TopLeftAreaCorner.y) < 10).ToList();

            int index = UnityEngine.Random.Range(0, sortedBottom.Count);
            bottom = sortedBottom[index];
        }

        var possibleNeighborsTop = topChildren.Where(child => GetValidX(bottom.TopLeftAreaCorner, bottom.TopRightAreaCorner, child.BottomLeftAreaCorner, child.BottomRightAreaCorner) != -1).OrderBy(child => child.BottomRightAreaCorner.y).ToList();

        if (possibleNeighborsTop.Count == 0)
        {
            top = structure2;
        }
        else
        {
            top = possibleNeighborsTop[0];
        }

        int x = GetValidX(bottom.TopLeftAreaCorner, bottom.TopRightAreaCorner, top.BottomLeftAreaCorner, top.BottomRightAreaCorner);

        while (x == -1 && sortedBottom.Count > 1)
        {
            sortedBottom = sortedBottom.Where(child => child.TopLeftAreaCorner.x != top.TopLeftAreaCorner.x).ToList();
            bottom = sortedBottom[0];
            x = GetValidX(bottom.TopLeftAreaCorner, bottom.TopRightAreaCorner, top.BottomLeftAreaCorner, top.BottomRightAreaCorner);
        }
        BottomLeftAreaCorner = new Vector2Int(x, bottom.TopLeftAreaCorner.y);
        TopRightAreaCorner = new Vector2Int(x + this.corridorWidth, top.BottomLeftAreaCorner.y);
    }

    private int GetValidX(Vector2Int bottomLeft, Vector2Int bottomRight, Vector2Int topLeft, Vector2Int topRight)
    {
        if (topLeft.x < bottomLeft.x && bottomRight.x < topRight.x)
        {
            return StructureHelper.CalculateMiddlePoint(bottomLeft + new Vector2Int(modifierDistance, 0), bottomRight - new Vector2Int(this.corridorWidth + modifierDistance, 0)).x;
        }

        if (topLeft.x >= bottomLeft.x && bottomRight.x >= topRight.x)
        {
            return StructureHelper.CalculateMiddlePoint(topLeft + new Vector2Int(modifierDistance, 0), topRight - new Vector2Int(this.corridorWidth + modifierDistance, 0)).x;
        }

        if (bottomLeft.x >= topLeft.x && bottomLeft.x <= topRight.x)
        {
            return StructureHelper.CalculateMiddlePoint(bottomLeft + new Vector2Int(modifierDistance, 0), topRight - new Vector2Int(this.corridorWidth + modifierDistance, 0)).x;
        }

        if (bottomRight.x <= topRight.x && bottomRight.x >= topLeft.x)
        {
            return StructureHelper.CalculateMiddlePoint(topLeft + new Vector2Int(modifierDistance, 0), bottomRight - new Vector2Int(this.corridorWidth + modifierDistance, 0)).x;
        }

        return -1;
    }

    private RelativePosition CheckPosition()
    {
        // temporary middle points of structure 1 and structure 2
        Vector2 m1Temp = ((Vector2)(structure1.TopRightAreaCorner + structure1.BottomLeftAreaCorner)) / 2;
        Vector2 m2Temp = ((Vector2)(structure2.TopRightAreaCorner + structure2.BottomLeftAreaCorner)) / 2;

        float angle = CalculateAngle(m1Temp, m2Temp);

        if ((angle <= 45 && angle >= 0) || (angle > -45 && angle < 0))
        {
            return RelativePosition.Right;
        }
        else if (angle > 45 && angle < 135)
        {
            return RelativePosition.Up;
        }
        else if (angle > -135 && angle < -45)
        {
            return RelativePosition.Down;
        }
        else
        {
            return RelativePosition.Left;
        }
    }

    private float CalculateAngle(Vector2 m1, Vector2 m2)
    {
        return Mathf.Atan2(m2.y - m1.y, m2.x - m1.x) * Mathf.Rad2Deg;
    }
}
