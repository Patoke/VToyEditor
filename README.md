# VToyEditor

### Editor and Scene viewer for the Virtual Toys In-House engine used in the Torrente game series

## How to use

Build the project or use the releases

Place the game's ``demo.vpk`` where ``VToyEditor.exe`` is located

Run ``VToyEditor.exe``

## What's missing? What's wrong?

There's only export functionality for scene files and not much support for different game modules

In the plaza map (for Torrente on-line), the floor of the church culls the semi-transparent flags below, if culling is disabled, these flags are rendered on top of the floor, probably something to do with the render queue for transparent items that has to be fixed

There's also no way to modify the position or vertices of meshes/props/decals and modules like mp_CCar don't render anything on screen as the model parser is still yet to be added (.nfo files and the RenderNFO and AnimatedRenderNFO modules)

And whatever else I cannot think of!!!
