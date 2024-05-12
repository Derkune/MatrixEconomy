This is a repo containing the demo of an approach to simulating a videogame economy using matrix multiplication.

To build and launch the demo, open project.godot with Godot 3.5.1 .Net version.

View video showcase of this project here:  https://youtu.be/9B01U9sQYYY 

Here is how this simulation works:

Some vocabulary: 

We call a "sector" some unit of simulation of our world. This approach was originally conceived for spaceship trading games, so sectors would be the space sectors that player could visit. Sector can contain NPC spaceships, space stations, astronomic objects and other gameplay elements. However, sectors are abstracted away as a sequence of real numbers. In game design sense this could manifest as game objects being generated and used only when player is about to enter the sector, in all other cases sector is only being simulated as an abstract number representation. Importantly for this simulation, sectors could be connected between each other, and the strength of that connection (number between 0 and 1) could represent the duration of traversal of the connection between these sectors, or fuel cost of traversal, or some other thing related to astronavigation. 

We call a "metric" some economic value we would like to simulate. Examples of these metrics are: (concentration of) high tech wares, ...ores, ...food, ...fuel, ...crime, ...evil aliens, and so forth.

Most importantly, every metric has a value in every location (sector) of the game. This is represented as a table (matrix) that has rows that correspond to every sector, and columns that correspond to metrics (or vice versa). Entries in this matrix are real (floating point) numbers.

We call this matrix a state matrix M.

As mentioned, sectors are "geographically" (or, rather, astronavigationally) connected to each other. These connections are represented  with an adjacency matrix M.
