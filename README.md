# Shroomfall

![fallingmushroom](https://github.com/user-attachments/assets/4c1bfe19-313b-4e53-8153-35881fd8e230)

## Overview
A multiplayer cavern diving game where players work together to reach deeper sections of procedurally generated caverns.
Find tools during your expedition, and work together to avoid traps, fall damage, and enemies.
Reach different cave biomes and get closer to the core of the planet to find the mythical ore, Mythril.

### Gameplay
The objective of Shroomfall is to descend as deep as possible into procedurally generated caverns while collecting resources, avoiding hazards, and working together to survive. Each run begins in a safe zone where players prepare their gear before entering the caves. As they progress, players encounter increasingly challenging traps, enemies, and environmental obstacles that require teamwork and strategic thinking. As you and your friends progress further down, you will encounter different biomes, all with their own unique mechanics and aesthetics. The ultimate goal is to survive long enough to reach the planet’s core and uncover the mythical ore, Mythril.

### Mechanics
Each run is procedurally generated, meaning no two expeditions are the same. Death isn’t the end; fallen teammates can be revived by reaching the end of cave biomes, where players can find a “rest area”  before tackling the next biome. Movement is physics-driven, with momentum-based jumping, climbing, and fall damage. Players can climb different surfaces, wall-jump, and use tools found to help navigate through the cavern. The environment is interactive, where players can pick up plants to be used as tools or triggerable events that can cause rocks to become unstable. Players communicate in close proximity to add a new level of immersion and cooperative mechanics.

## Dungeon Generation
This Dungeon Generation implements a 3D dungeon generator that uses Delaunay triangulation to map out rooms and connect them with hallways.

### Original Work and Credits:
  - This project is based on algorithms from the open-source Dungeon Generator 
    by Vazgriz, available at: https://github.com/vazgriz/DungeonGenerator
  - Delaunay triangulation (specifically tetrahedralization due to 3d graph/space of the room locations) and related graph algorithms are adapted from 
    BlueRaja’s implementations, used here under the licenses and permissions provided.

### Explanation:
  - Rooms are represented as vertices on a 3D coordinate plane.
  - Delaunay triangulation is applied to these vertices to determine 
    spatial connections between rooms.
  - The resulting triangulated graph is then used to generate hallways 
    (and stair connections, where necessary) between rooms.
  - A helpful visualization analogy is a Voronoi diagram: intersections 
    represent room positions (vertices), and edges define possible 
    connections between them.

### License

The priority queue is by BlueRaja. The license can be found in `Assets/BlueRaja`.
The delaunay triangulation is based off of code by Simon Zeni. The licenses can be found in `Assets/Scripts2D/Delaunay2D.cs` and `Assets/Scripts3D/Delaunay3D.cs`.
