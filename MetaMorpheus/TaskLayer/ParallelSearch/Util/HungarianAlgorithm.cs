using System;
using System.Linq;

namespace TaskLayer.ParallelSearch.Util;

/// <summary>
/// Solves the minimum-cost assignment problem using the Hungarian algorithm.
/// </summary>
public static class HungarianAlgorithm
{
    public static int[] FindAssignments(double[,] costMatrix)
    {
        int rows = costMatrix.GetLength(0);
        int cols = costMatrix.GetLength(1);
        int dim = Math.Max(rows, cols);

        double[,] cost = new double[dim, dim];
        for (int i = 0; i < dim; i++)
        for (int j = 0; j < dim; j++)
            cost[i, j] = i < rows && j < cols ? costMatrix[i, j] : 0;

        for (int i = 0; i < dim; i++)
        {
            double min = double.MaxValue;
            for (int j = 0; j < dim; j++)
                if (cost[i, j] < min) min = cost[i, j];
            for (int j = 0; j < dim; j++)
                cost[i, j] -= min;
        }

        for (int j = 0; j < dim; j++)
        {
            double min = double.MaxValue;
            for (int i = 0; i < dim; i++)
                if (cost[i, j] < min) min = cost[i, j];
            for (int i = 0; i < dim; i++)
                cost[i, j] -= min;
        }

        int[] rowCover = new int[dim];
        int[] colCover = new int[dim];
        int[,] mask = new int[dim, dim];
        int step = 1;
        int[] pathRow = new int[dim * 2];
        int[] pathCol = new int[dim * 2];
        int pathCount = 0;

        while (true)
        {
            switch (step)
            {
                case 1:
                    for (int i = 0; i < dim; i++)
                    for (int j = 0; j < dim; j++)
                        if (cost[i, j] == 0 && rowCover[i] == 0 && colCover[j] == 0)
                        {
                            mask[i, j] = 1;
                            rowCover[i] = 1;
                            colCover[j] = 1;
                        }
                    Array.Clear(rowCover, 0, dim);
                    Array.Clear(colCover, 0, dim);
                    step = 2;
                    break;

                case 2:
                    for (int i = 0; i < dim; i++)
                    for (int j = 0; j < dim; j++)
                        if (mask[i, j] == 1)
                            colCover[j] = 1;
                    step = colCover.Sum() >= dim ? 7 : 3;
                    break;

                case 3:
                    bool done = false;
                    while (!done)
                    {
                        var (zRow, zCol) = FindZero(cost, rowCover, colCover, dim);
                        if (zRow == -1)
                        {
                            step = 5;
                            done = true;
                        }
                        else
                        {
                            mask[zRow, zCol] = 2;
                            int starCol = -1;
                            for (int j = 0; j < dim; j++)
                            {
                                if (mask[zRow, j] == 1)
                                {
                                    starCol = j;
                                    break;
                                }
                            }

                            if (starCol != -1)
                            {
                                rowCover[zRow] = 1;
                                colCover[starCol] = 0;
                            }
                            else
                            {
                                step = 4;
                                pathRow[0] = zRow;
                                pathCol[0] = zCol;
                                pathCount = 1;
                                done = true;
                            }
                        }
                    }
                    break;

                case 4:
                    bool found;
                    do
                    {
                        int row = FindStarInCol(mask, pathCol[pathCount - 1], dim);
                        if (row != -1)
                        {
                            pathRow[pathCount] = row;
                            pathCol[pathCount] = pathCol[pathCount - 1];
                            pathCount++;
                        }
                        else
                        {
                            found = false;
                            break;
                        }

                        int col = FindPrimeInRow(mask, pathRow[pathCount - 1], dim);
                        pathRow[pathCount] = pathRow[pathCount - 1];
                        pathCol[pathCount] = col;
                        pathCount++;
                        found = true;
                    } while (found);

                    for (int i = 0; i < pathCount; i++)
                        mask[pathRow[i], pathCol[i]] = mask[pathRow[i], pathCol[i]] == 1 ? 0 : 1;

                    Array.Clear(rowCover, 0, dim);
                    Array.Clear(colCover, 0, dim);
                    for (int i = 0; i < dim; i++)
                    for (int j = 0; j < dim; j++)
                        if (mask[i, j] == 2)
                            mask[i, j] = 0;
                    step = 2;
                    break;

                case 5:
                    double minVal = double.MaxValue;
                    for (int i = 0; i < dim; i++)
                        if (rowCover[i] == 0)
                            for (int j = 0; j < dim; j++)
                                if (colCover[j] == 0 && cost[i, j] < minVal)
                                    minVal = cost[i, j];
                    for (int i = 0; i < dim; i++)
                    for (int j = 0; j < dim; j++)
                    {
                        if (rowCover[i] == 1)
                            cost[i, j] += minVal;
                        if (colCover[j] == 0)
                            cost[i, j] -= minVal;
                    }
                    step = 3;
                    break;

                case 7:
                    int[] result = new int[rows];
                    for (int i = 0; i < rows; i++)
                    {
                        result[i] = -1;
                        for (int j = 0; j < cols; j++)
                            if (mask[i, j] == 1)
                                result[i] = j;
                    }
                    return result;
            }
        }
    }

    private static (int, int) FindZero(double[,] cost, int[] rowCover, int[] colCover, int dim)
    {
        for (int i = 0; i < dim; i++)
            if (rowCover[i] == 0)
                for (int j = 0; j < dim; j++)
                    if (cost[i, j] == 0 && colCover[j] == 0)
                        return (i, j);
        return (-1, -1);
    }

    private static int FindStarInCol(int[,] mask, int col, int dim)
    {
        for (int i = 0; i < dim; i++)
            if (mask[i, col] == 1)
                return i;
        return -1;
    }

    private static int FindPrimeInRow(int[,] mask, int row, int dim)
    {
        for (int j = 0; j < dim; j++)
            if (mask[row, j] == 2)
                return j;
        return -1;
    }
}
