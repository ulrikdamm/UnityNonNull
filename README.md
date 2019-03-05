# UnityNonNull

Small package to add a `[NonNull]` attribute. Put this on a field to make it highlight in the editor when the field haven't been assigned, or on a class to automatically make it apply to all fields in that class. When you put in on a class, you can also put `[AllowNull]` on specific fields to allow them to be unassigned.

![](http://ufd.dk/NonNullEditor.png)

The script will also produce an error when you play the game in the editor or when you make a build if you have any unassigned references in your scenes.

![](http://ufd.dk/NonNullError.png)

This is handy to make sure something doesn't get unassigned by accident. You can also check wether an object is still in use or not by removing it, and then making a build, and see if you get any errors for unassigned references.

Fields with unassigned references many times only have one reference you could possibly assign to it; a `GameManager` field probably wants to refer to the `GameManager` MonoBehaviour in the scene, and a `LocalizationsHandler` field probably wants to refer to the single `LocalizationsHandler` ScriptableObject in your assets. In these cases, the editor will show a "fill" button next to the field, which will automatically fill out the reference. If it could possibly refer to multiple things, like a Rigidbody reference, there won't be a fill button, since it can't be automatically filled.

For value types, the package comes with a `[NonEmpty]` attribute, which can be used to check that lists and strings aren't empty, colors aren't unassigned, enums aren't the default case, numbers aren't zero, and so on.

## Installation

You can drop the NonNullAttribute.cs into your Assets folder (but it does *not* go in the Editor folder), or you can install it via the Unity package manager (recommended!) by adding this line to your Packages/manifest.json file:

`"co.northplay.nonnull": "https://github.com/ulrikdamm/UnityNonNull.git"`

And it will appear in the package manager and automatically install into your project.
