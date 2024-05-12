

This is a repo containing the demo of an approach to simulating a videogame economy using matrix multiplication.

To build and launch the demo, open project.godot with Godot 3.5.1 .Net version.

View video showcase of this project here: https://youtu.be/9B01U9sQYYY

Here is how this simulation works:

Some vocabulary:

We call a "sector" some unit of simulation of our world. This approach was originally conceived for spaceship trading games, so sectors would be the space sectors that player could visit. Sector can contain NPC spaceships, space stations, astronomic objects and other gameplay elements. However, sectors are abstracted away as a sequence of real numbers. In game design sense this could manifest as game objects being generated and used only when player is about to enter the sector, in all other cases sector is only being simulated as an abstract number representation. Importantly for this simulation, sectors could be connected between each other, and the strength of that connection (number between 0 and 1) could represent the duration of traversal of the connection between these sectors, or fuel cost of traversal, or some other thing related to astronavigation.

We call a "metric" some economic value we would like to simulate. Examples of these metrics are: (concentration of) high tech wares, ...ores, ...food, ...fuel, ...crime, ...evil aliens, and so forth.

Most importantly, every metric has a value in every location (sector) of the game. This is represented as a table (matrix) that has rows that correspond to every sector, and columns that correspond to metrics (or vice versa). Entries in this matrix are real (floating point) numbers.

We call this matrix the state matrix S.

As mentioned, sectors are "geographically" (or, rather, astronavigationally) connected to each other. These connections are represented with an adjacency matrix M. This matrix has rows and columns corresponding to each of the sector, and each entry in row n and column m represents the strength of connection between sector m and sector n. Entries are real valued numbers from 0 to 1, and entries on the diagonal are all 1, representing each sector being connected "as well as possible" to itself.

Metrics are connected to each other as well. This represents the idea that, for instance, low tech wares can be produced from ores and can also be used to produce high tech wares. Unlike the previous matrix, this one can be non-symmetric across the main diagonal, representing that derivative wares can be produced from constituent wares, but not vice versa. (Matrix M can also be possibly non-symmetric, representing, for instance, one-way wormholes.) From this we get a "metric adjacency" matrix N.

Important note: Adjacency matrixes M and N need to be row/column normalized, such that all rows (or all columns, depending on the order of the following multiplication) add up to 1.

The simulation goes like this: We do the matrix multiplication between S and M (or N and S). Given the right order of multiplication and orientation of S, we get another matrix S` that has the same size as S.

What is the meaning of S'? It is the state of the world, averaged out according to weights of connections. Take an entry in S. After multiplication, the corresponding entry in S` is a weighted average of that entry with its neighbors (in "sector space" or "metric space", depending on adjacency matrix used).

Take S. Perform the multiplication S' = N * S * M. This is the "averaged state". Subtract D = S' - S. This is the "diff" between current state and the current state. Multiply D by some small constant t to get a step diff, delta = D * t. delta is a matrix of the same size as S that represents the change of state S in a small time step if it were to tend towards the "averaged state" S`. Now you can add S_next = S + delta to advance the world state in simulation. In the next step of simulation, you set the previous S_next to be the state S, and repeat these steps again and again.

In short, simulation can be described by the following statement:

S -> S + (N * S * M - S)*t,

Where S is the state matrix, M is the sector adjacency matrix, N is the metric adjacency matrix, t is a small real number in (0, 1), and -> is the operation of replacing S with new S on each step of simulation.
At this point in explanation, the simulation isn't all that interesting. S tends towards S', and that's that. However, simulating any kind of event is extremely simple: Between steps of simulation, we can set small number of random entries in S to 0 or 1. This corresponds to random events of overproduction, shortages, battles, raids, holidays and much more.
