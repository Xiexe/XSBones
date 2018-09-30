This is a Unity editor script and shader that will take an arbitrary mesh and use it to show the bones of a humanoid or non humanoid avatar.

You can find the generator window under Xiexe > Tools > XSBonerGenerator.

"Armature" needs to be the top level object underneath the Animator.

Animator should be your Avatar top level object, with the animator on it.

Bone Model should be the model you want your bones to show as - we have some presets in the Bone Stuff > Bone Models folder.

Skinned Mesh Renderer should be one of the meshes that is weighted to the rig. Any should do.

Bone Material should be the material you want on the bones. We've includeded a shader for the material that can be stenciled through the avatar. If you want to take advantage of this easily, use my Toon shader or another shader that support stencils. Set your stencil to Always, and Pass Replace and set your reference value to the same on both the bone material, and the avatar material(s).

Please note that the stencil will default to 1 for the ref.

IKLines are lines between the legs and arms that show IK chains. The shader for this works the same as the bone material, in that it supports stenciling, and should be the same reference value as the others. IK Lines are optional.



![Image](https://i.imgur.com/yb9taGz.jpg)
