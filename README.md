# Future Editor, Scene viewer for the Virtual Toys In-House engine used in the Torrente game series

## How to use

Build the project

Where the output files are, create folders named `opts`, `hms`, `scns`, and `texs` (more to come)

Extract the game's VPK (if it has any) and put the corresponding files in their directories (match the extensions)

Run the compiled .exe

## What's missing? What's wrong?

Currently there's only functionality to view scenes and modify basic parameters

There's no export functionality so these basic parameters are just visual for the current session though an export functionality should be pretty easy to add, as most of the file formats are documented in this project

In the plaza map (for Torrente on-line), the floor of the church culls the semi-transparent flags below, if culling is disabled, these flags are rendered on top of the floor, probably something to do with the render queue for transparent items that has to be fixed

There's also no way to modify the position or vertices of meshes/props/decals and modules like mp_CCar don't render anything on screen as the model parser is still yet to be added (.nfo files and the RenderNFO and AnimatedRenderNFO modules)

And whatever else I cannot think of!!!
