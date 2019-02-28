using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;
using UnityEditor.Callbacks;
#endif

[System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Class)]
public class NonNullAttribute : PropertyAttribute {}

[System.AttributeUsage(System.AttributeTargets.Field)]
public class AllowNullAttribute : PropertyAttribute {}

#if UNITY_EDITOR
static class NullFieldGUI {
	public static void nullCheckedField(Rect position, SerializedProperty property, GUIContent label, bool showWarning) {
		if (!showWarning) {
			EditorGUI.PropertyField(position, property, label);
			return;
		}
		
		GUI.backgroundColor = Color.red;
		
		var fillCandidate = FindNonNull.findObjectToFill(property);
		if (fillCandidate == null) {
			EditorGUI.PropertyField(position, property, label);
			GUI.backgroundColor = Color.white;
			return;
		}
		
		var propertyRect = new Rect { x = position.x, y = position.y, width = position.width - 40, height = position.height };
		var buttonRect = new Rect { x = position.x + propertyRect.width + 8, y = position.y, width = 40 - 8, height = position.height };
		
		EditorGUI.PropertyField(propertyRect, property, label);
		
		GUI.backgroundColor = Color.white;
		if (GUI.Button(buttonRect, "Fill")) { property.objectReferenceValue = fillCandidate; }
	}
}

[CustomPropertyDrawer(typeof(Object), useForChildren: true)]
public class DefaultObjectDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);
        
        var showWarning = (
            property.propertyType == SerializedPropertyType.ObjectReference
            && property.objectReferenceValue == null
            && FindNonNull.classHasAttributeOfType(property.serializedObject.targetObject.GetType(), typeof(NonNullAttribute))
        );
		
		NullFieldGUI.nullCheckedField(position, property, label, showWarning);
		EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(AllowNullAttribute))]
public class AllowNullAttributeDrawer : PropertyDrawer {
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		EditorGUI.BeginProperty(position, label, property);
		EditorGUI.PropertyField(position, property, label);
		EditorGUI.EndProperty();
	}
}

[CustomPropertyDrawer(typeof(NonNullAttribute))]
public class NonNullAttributeDrawer : PropertyDrawer {
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		EditorGUI.BeginProperty(position, label, property);
		var showWarning = (property.objectReferenceValue == null);
		NullFieldGUI.nullCheckedField(position, property, label, showWarning);
		EditorGUI.EndProperty();
	}
}

class FindNonNull {
    [PostProcessScene]
	public static void scenePostProcess() {
		if (findAllNonNulls()) {
			if (Application.isPlaying) { EditorApplication.isPaused = true; }
		}
	}
    
    [MenuItem("Assets/NonNull/Check for unassigned references in current scene")]
	public static bool findAllNonNulls() {
		var anyNulls = false;
		
		enumerateAllComponentsInScene((GameObject obj, Component component) => {
            nullCheckComponent(obj, component, ref anyNulls);
		});
		
		return anyNulls;
	}
	
	static void nullCheckComponent(GameObject obj, Component component, ref bool anyNulls) {
		var componentHasNonNull = classHasAttributeOfType(component.GetType(), typeof(NonNullAttribute));
		var fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		
		for (var i = 0; i < fields.Length; i++) {
			var field = fields[i];
			if (!shouldNullCheckField(field, componentHasNonNull)) { continue; }
			
			var fieldValue = field.GetValue(component);
			
			if (fieldValue is UnityEngine.Object) {
				if (((Object)fieldValue) != null) { continue; }
			} else {
				if (!object.ReferenceEquals(fieldValue, null)) { continue; }
			}
			
			Debug.LogError("Missing reference for " + field.Name + " in " + component.GetType().Name + " on " + obj.name + " in scene " + EditorSceneManager.GetActiveScene().name, component);
			anyNulls = true;
		}
	}
	
	static bool shouldNullCheckField(FieldInfo field, bool componentHasNonNull) {
		if (!field.FieldType.IsClass) { return false; }
		
		var isSerialized = fieldHasAttributeOfType(field, typeof(SerializeField));
		var isPublic = fieldAccessIs(field, FieldAttributes.Public);
		if (!isSerialized && !isPublic) { return false; }
		
		if (!componentHasNonNull) {
			if (!fieldHasAttributeOfType(field, typeof(NonNullAttribute))) { return false; }
		} else {
			if (fieldHasAttributeOfType(field, typeof(AllowNullAttribute))) { return false; }
		}
		
		return true;
	}
	
	public static bool classHasAttributeOfType(System.Type classType, System.Type ofType) {
		return (classType.GetCustomAttributes(ofType, false).Length > 0);
	}
	
	static bool fieldAccessIs(FieldInfo field, FieldAttributes attribute) {
		return ((field.Attributes & FieldAttributes.FieldAccessMask) == attribute);
	}
	
	static bool fieldHasAttributeOfType(FieldInfo field, System.Type type) {
		return (field.GetCustomAttributes(type, false).Length > 0);
	}
	
	static void enumerateAllComponentsInScene(System.Action<GameObject, Component> callback) {
		enumerateAllGameObjectsInScene(obj => {
			var components = obj.GetComponents<Component>();
			for (var i = 0; i < components.Length; i++) {
				callback(obj, components[i]);
			}
		});
	}
	
	static void enumerateAllGameObjectsInScene(System.Action<GameObject> callback) {
		var rootObjects = EditorSceneManager.GetActiveScene().GetRootGameObjects();
		
		foreach (var rootObject in rootObjects) {
			enumerateChildrenOf(rootObject, callback);
		}
	}
	
	static void enumerateChildrenOf(GameObject obj, System.Action<GameObject> callback) {
		callback(obj);
		
		for (var i = 0; i < obj.transform.childCount; i++) {
			enumerateChildrenOf(obj.transform.GetChild(i).gameObject, callback);
		}
	}
	
	public static Object findObjectToFill(SerializedProperty property) {
		if (property.propertyPath.Contains(".Array")) { return null; }
		
        var objectType = property.serializedObject.targetObject.GetType();
        var fieldType = objectType.GetField(property.propertyPath, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance).FieldType;
		
		if (fieldType.IsSubclassOf(typeof(Component))) {
			return findSceneObjectToFill(fieldType);
		}
		
		if (fieldType.IsSubclassOf(typeof(ScriptableObject))) {
			return findAssetObjectToFill(fieldType);
		}
		
        return null;
    }
	
	static Object findSceneObjectToFill(System.Type fieldType) {
		Object objectInScene = null;
		
		var rootObjects = EditorSceneManager.GetActiveScene().GetRootGameObjects();
		for (var i = 0; i < rootObjects.Length; i++) {
			var candidates = rootObjects[i].GetComponentsInChildren(fieldType, includeInactive: true);
			if (candidates.Length > 1) { return null; }
			if (candidates.Length == 1 && objectInScene != null) { return null; }
			if (candidates.Length == 1) { objectInScene = candidates[0]; }
		}
		
		return objectInScene;
	}
	
	static Object findAssetObjectToFill(System.Type fieldType) {
		var objectsInAssets = AssetDatabase.FindAssets("t:" + fieldType.Name);
		if (objectsInAssets.Length != 1) { return null; }
		
		var assetId = objectsInAssets[0];
		var path = AssetDatabase.GUIDToAssetPath(assetId);
		return AssetDatabase.LoadAssetAtPath(path, fieldType);
	}
}
#endif
