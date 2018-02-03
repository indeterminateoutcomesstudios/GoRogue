﻿using GoRogue;
using GoRogue.Pathing;
using GoRogue.Random;
using GoRogue.MapGeneration.Generators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using CA = EMK.Cartography;

namespace GoRogue_UnitTests
{
    class GraphReturn
    {
        public CA.Node[,] Nodes;
        public CA.Graph Graph;
    }

    [TestClass]
    public class PathingTests
    {
        static private readonly int MAP_WIDTH = 30;
        static private readonly int MAP_HEIGHT = 30;

        static private readonly int ITERATIONS = 100;

        static private readonly Coord START = Coord.Get(1, 2);
        static private readonly Coord END = Coord.Get(17, 14);

        [TestMethod]
        public void ManualAStarChebyshevTest()
        {
            var map = new ArrayMapOf<bool>(MAP_WIDTH, MAP_HEIGHT);
            RectangleMapGenerator.Generate(map);

            var pather = new AStar(map, Distance.CHEBYSHEV);
            var path = pather.ShortestPath(START, END);

            Utility.PrintHightlightedPoints(map, path.StepsWithStart);

            foreach (var point in path.StepsWithStart)
                Console.WriteLine(point);
        }

        [TestMethod]
        public void ManualAStarManhattanTest()
        {
            var map = new ArrayMapOf<bool>(MAP_WIDTH, MAP_HEIGHT);
            RectangleMapGenerator.Generate(map);

            var pather = new AStar(map, Distance.MANHATTAN);
            var path = pather.ShortestPath(START, END);

            Utility.PrintHightlightedPoints(map, path.StepsWithStart);

            foreach (var point in path.StepsWithStart)
                Console.WriteLine(point);
        }

        [TestMethod]
        public void ManualAStarEuclidianTest()
        {
            var map = new ArrayMapOf<bool>(MAP_WIDTH, MAP_HEIGHT);
            RectangleMapGenerator.Generate(map);

            var pather = new AStar(map, Distance.EUCLIDEAN);
            var path = pather.ShortestPath(START, END);

            Utility.PrintHightlightedPoints(map, path.StepsWithStart);

            foreach (var point in path.StepsWithStart)
                Console.WriteLine(point);
        }

        [TestMethod]
        public void AStarMatchesCorrectChebyshev() => aStarMatches(Distance.CHEBYSHEV);

        [TestMethod]
        public void AStarMatchesCorrectEuclidean() => aStarMatches(Distance.EUCLIDEAN);

        [TestMethod]
        public void AStarMatchesCorrectManhattan() => aStarMatches(Distance.MANHATTAN);

        private void aStarMatches(Distance distanceCalc)
        {
            var map = new ArrayMapOf<bool>(MAP_WIDTH, MAP_HEIGHT);
            CellularAutomataGenerator.Generate(map);
            var graphTuple = initGraph(map, distanceCalc);

            var pather = new AStar(map, distanceCalc);
            var controlPather = new CA.AStar(graphTuple.Graph);
            controlPather.ChoosenHeuristic = distanceHeuristic(distanceCalc);


            for (int i = 0; i < ITERATIONS; i++)
            {
                Coord start = Coord.Get(SingletonRandom.DefaultRNG.Next(map.Width - 1), SingletonRandom.DefaultRNG.Next(map.Height - 1));
                while (!map[start])
                    start = Coord.Get(SingletonRandom.DefaultRNG.Next(map.Width - 1), SingletonRandom.DefaultRNG.Next(map.Height - 1));

                Coord end = Coord.Get(SingletonRandom.DefaultRNG.Next(map.Width - 1), SingletonRandom.DefaultRNG.Next(map.Height - 1));
                while (end == start || !map[end])
                    end = Coord.Get(SingletonRandom.DefaultRNG.Next(map.Width - 1), SingletonRandom.DefaultRNG.Next(map.Height - 1));

                var path1 = pather.ShortestPath(start, end);
                controlPather.SearchPath(graphTuple.Nodes[start.X, start.Y], graphTuple.Nodes[end.X, end.Y]);
                var path2 = controlPather.PathByNodes;
                
                if (path2.Length != path1.LengthWithStart)
                {
                    Console.WriteLine($"Error: Control got {path2.Length}, but custom AStar got {path1.Length}");
                    Console.WriteLine("Control: ");
                    Utility.PrintHightlightedPoints(map, Utility.ToCoords(path2));
                    Console.WriteLine("AStar  :");
                    Utility.PrintHightlightedPoints(map, path1.StepsWithStart);
                }

                bool lengthGood = (path2.Length <= path1.LengthWithStart);
                Assert.AreEqual(true, lengthGood);
                Assert.AreEqual(path1.Start, start);
                Assert.AreEqual(path1.End, end);
                checkWalkable(path1, map);
                checkAdjacency(path1, distanceCalc, map);
            }
        }

        private static void checkWalkable(Path path, IMapOf<bool> map)
        {
            foreach (var pos in path.StepsWithStart)
                Assert.AreEqual(true, map[pos]);
        }

        private static void checkAdjacency(Path path, Distance distanceCalc, IMapOf<bool> map)
        {
            if (path.LengthWithStart == 1)
                return;

            for (int i = 0; i < path.LengthWithStart - 2; i++)
            {
                bool isAdjacent = false;

                foreach (var dir in Direction.GetNeighbors(distanceCalc)())
                {
                    if (path.GetStepWithStart(i) + dir == path.GetStepWithStart(i + 1))
                    {
                        isAdjacent = true;
                        break;
                    }
                }

                Assert.AreEqual(true, isAdjacent);
            }
        }

        private static CA.Heuristic distanceHeuristic(Distance distanceCalc)
        {
            switch(distanceCalc.Type)
            {
                case DistanceType.CHEBYSHEV:
                    return CA.AStar.MaxAlongAxisHeuristic;
                case DistanceType.EUCLIDEAN:
                    return CA.AStar.EuclidianHeuristic;
                case DistanceType.MANHATTAN:
                    return CA.AStar.ManhattanHeuristic;
                default:
                    throw new Exception("Should not occur");
            }
        }

        // Initialize graph for control-case AStar, based on a GoRogue IMapOf
        private static GraphReturn initGraph(IMapOf<bool> map, Distance connectivity)
        {
            var neighborsGetter = Direction.GetNeighbors(connectivity);

            var returnVal = new GraphReturn();
            returnVal.Graph = new CA.Graph();

            returnVal.Nodes = new CA.Node[map.Width, map.Height]; // So we can add arcs easier

            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if (map[x, y])
                    {
                        returnVal.Nodes[x, y] = new CA.Node(x, y, 0);
                        returnVal.Graph.AddNode(returnVal.Nodes[x, y]);
                    }

            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                {
                    if (map[x, y])
                    {
                        foreach (var dir in neighborsGetter())
                        {
                            var neighbor = Coord.Get(x, y) + dir;

                            // Out of bounds of map
                            if (neighbor.X < 0 || neighbor.Y < 0 || neighbor.X >= map.Width || neighbor.Y >= map.Height)
                                continue;

                            if (!map[neighbor]) // Not walkable, so no node exists for it
                                continue;

                            returnVal.Graph.AddArc(new CA.Arc(returnVal.Nodes[x, y], returnVal.Nodes[neighbor.X, neighbor.Y]));
                        }
                    }
                    
                }

            return returnVal;
        }


    }
}