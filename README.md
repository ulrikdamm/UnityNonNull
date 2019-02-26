# UnityNonNull

Small package to add a [NonNull] attribute. Put this on a field to make it highlight in the editor when the field haven't been assigned, or on a class to automatically make it apply to all fields in that class. When you put in on a class, you can also put [AllowNull] on specific fields to allow them to be unassigned.

The script will also produce an error when you play the game in the editor or when you make a build if you have any unassigned references in your scenes.

This is handy to make sure something doesn't get unassigned by accident. You can also check wether an object is still in use or not by removing it, and then making a build, and see if you get any errors for unassigned references.

Fields with unassigned references many times only have one reference you could possibly assign to it; a GameManager field probably wants to refer to the GameManager MonoBehaviour in the scene, and a LocalizationsHandler field probably wants to refer to the single LocalizationsHandler ScriptableObject in your assets. In these cases, the editor will show a "fill" button next to the field, which will automatically fill out the reference. If it could possibly refer to multiple things, like a Rigidbody reference, there won't be a fill button, since it can't be automatically filled.
