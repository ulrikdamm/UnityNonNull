using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;
using UnityEditor.Callbacks;
using System.Collections.Generic;
#endif

[System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Class)]
public class NonNullAttribute : PropertyAttribute {}

[System.AttributeUsage(System.AttributeTargets.Field)]
public class AllowNullAttribute : PropertyAttribute {}

[System.AttributeUsage(System.AttributeTargets.Field)]
public class NonEmptyAttribute : PropertyAttribute {}

#if UNITY_EDITOR
static class NullFieldGUI {
	public static void nullCheckedField(Rect position, SerializedProperty property, GUIContent label, bool showWarning) {
		if (!showWarning) {
			EditorGUI.PropertyField(position, property, label);
			return;
		}
		
		GUI.backgroundColor = Color.red;
		
		string fillButtonText;
		var fillCandidate = FindNonNull.findObjectToFill(property, out fillButtonText);
		if (fillCandidate == null) {
			EditorGUI.PropertyField(position, property, label);
			GUI.backgroundColor = Color.white;
			return;
		}
		
		var propertyRect = new Rect { x = position.x, y = position.y, width = position.width - 45, height = position.height };
		var buttonRect = new Rect { x = position.x + propertyRect.width + 8, y = position.y, width = 45 - 8, height = position.height };
		
		EditorGUI.PropertyField(propertyRect, property, label);
		
		GUI.backgroundColor = Color.white;
		if (GUI.Button(buttonRect, fillButtonText)) { property.objectReferenceValue = fillCandidate; }
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
		var showWarning = (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null);
		NullFieldGUI.nullCheckedField(position, property, label, showWarning);
		EditorGUI.EndProperty();
	}
}

[CustomPropertyDrawer(typeof(NonEmptyAttribute))]
public class NonEmptyAttributeDrawer : PropertyDrawer {
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		EditorGUI.BeginProperty(position, label, property);
		
		bool showWarning;
		
		switch (property.propertyType) {
			case SerializedPropertyType.String: showWarning = string.IsNullOrEmpty(property.stringValue); break;
			case SerializedPropertyType.AnimationCurve: showWarning = (property.animationCurveValue == null || property.animationCurveValue.length == 0); break;
			case SerializedPropertyType.LayerMask: showWarning = (property.intValue == 0); break;
			case SerializedPropertyType.ArraySize: showWarning = (property.arraySize == 0); break;
			case SerializedPropertyType.Color: showWarning = (property.colorValue == null || property.colorValue == new Color(0, 0, 0, 0)); break;
			case SerializedPropertyType.Enum: showWarning = (property.enumValueIndex == 0); break;
			case SerializedPropertyType.Integer: showWarning = (property.intValue == 0); break;
			case SerializedPropertyType.Float: showWarning = (Mathf.Approximately((float)property.doubleValue, 0)); break;
			case SerializedPropertyType.Vector2: showWarning = property.vector2Value == Vector2.zero; break;
			case SerializedPropertyType.Vector3: showWarning = property.vector3Value == Vector3.zero; break;
			// case SerializedPropertyType.Vector4: showWarning = property.vector4Value == Vector4.zero; break;
			case SerializedPropertyType.Vector2Int: showWarning = property.vector2IntValue == Vector2Int.zero; break;
			case SerializedPropertyType.Vector3Int: showWarning = property.vector3IntValue == Vector3Int.zero; break;
			default: showWarning = false; break;
		}
		
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
	
    [MenuItem("Assets/NonNull/Check for unassigned references in all scenes in build settings")]
	public static void findAllNonNullsInAllScenes() {
		var scenes = EditorBuildSettings.scenes;
		
		if (scenes.Length == 0) {
			Debug.Log("No scenes in build settings, so no scenes checked.");
			return;
		}
		
        for (int i = 0; i < scenes.Length; i++) {
            var scene = scenes[i];
            
            if (EditorUtility.DisplayCancelableProgressBar("Checking all scenes", scene.path, i / (float)scenes.Length)) { 
                EditorUtility.ClearProgressBar();
                return; 
            }
			
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
			findAllNonNulls();
		}
		
		EditorUtility.ClearProgressBar();
	}
	
	static void nullCheckComponent(GameObject obj, Component component, ref bool anyNulls) {
		if (component == null) {
			logError("Missing script for component", obj);
			anyNulls = true;
			return;
		}
		
		var componentHasNonNull = classHasAttributeOfType(component.GetType(), typeof(NonNullAttribute));
		var fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		
		for (var i = 0; i < fields.Length; i++) {
			var field = fields[i];
			
			var isSerialized = fieldHasAttributeOfType(field, typeof(SerializeField));
			var isPublic = fieldAccessIs(field, FieldAttributes.Public);
			if (!isSerialized && !isPublic) { continue; }
			
			nullCheckField(obj, component, field, componentHasNonNull, ref anyNulls);
			emptyCheckField(obj, component, field, ref anyNulls);
		}
	}
	
	static void emptyCheckField(GameObject obj, Component component, FieldInfo field, ref bool anyNulls) {
		if (!fieldHasAttributeOfType(field, typeof(NonEmptyAttribute))) { return; }
		
		var fieldValue = field.GetValue(component);
		
		if (fieldValue is string && string.IsNullOrEmpty((string)fieldValue)) {
			logError("Empty string", obj, component, field);
		} else if (fieldValue is AnimationCurve && (fieldValue == null || ((AnimationCurve)fieldValue).length == 0)) {
			logError("Empty animation curve", obj, component, field);
		} else if (fieldValue is LayerMask && (fieldValue == null || ((LayerMask)fieldValue) == 0)) {
			logError("Unspecified layer mask", obj, component, field);
		} else if (fieldValue is System.Array && (fieldValue == null || ((System.Array)fieldValue).Length == 0)) {
			logError("Empty array", obj, component, field);
		} else if (fieldValue is System.Collections.IList && (fieldValue == null || ((System.Collections.IList)fieldValue).Count == 0)) {
			logError("Empty list", obj, component, field);
		} else if (fieldValue is Color && (fieldValue == null || ((Color)fieldValue) == new Color(0, 0, 0, 0))) {
			logError("No color", obj, component, field);
		} else if (fieldValue != null && fieldValue.GetType().IsEnum && ((int)fieldValue) == 0) {
			logError("Empty enum value", obj, component, field);
		} else if (fieldValue is int && ((int)fieldValue) == 0) {
			logError("Zero integer value", obj, component, field);
		} else if (fieldValue is float && Mathf.Approximately((float)fieldValue, 0)) {
			logError("Zero float value", obj, component, field);
		} else if (fieldValue is double && (double)fieldValue == 0) {
			logError("Zero double value", obj, component, field);
		} else if (isEmptyVector(fieldValue)) {
			logError("Empty vector value", obj, component, field);
		} else {
			return;
		}
		
		anyNulls = true;
	}
	
	static bool isEmptyVector(object value) {
		if (value is Vector2 && (Vector2)value == Vector2.zero) { return true; }
		if (value is Vector2Int && (Vector2Int)value == Vector2Int.zero) { return true; }
		if (value is Vector3 && (Vector3)value == Vector3.zero) { return true; }
		if (value is Vector3Int && (Vector3Int)value == Vector3Int.zero) { return true; }
		if (value is Vector4 && (Vector4)value == Vector4.zero) { return true; }
		return false;
	}
	
	static void nullCheckField(GameObject obj, Component component, FieldInfo field, bool componentHasNonNull, ref bool anyNulls) {
		if (!shouldNullCheckField(field, componentHasNonNull)) { return; }
		
		var fieldValue = field.GetValue(component);
		
		if (fieldValue is UnityEngine.Object) {
			if (((Object)fieldValue) != null) { return; }
		} else {
			if (!object.ReferenceEquals(fieldValue, null)) { return; }
		}
		
		logError("Missing reference", obj, component, field);
		anyNulls = true;
	}
	
	static bool shouldNullCheckField(FieldInfo field, bool componentHasNonNull) {
		if (!field.FieldType.IsClass) { return false; }
		
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
	
	public static Object findObjectToFill(SerializedProperty property, out string actionName) {
		actionName = null;
		if (property.propertyType != SerializedPropertyType.ObjectReference) { return null; }
		if (property.propertyPath.Contains(".Array")) { return null; }
		
        var objectType = property.serializedObject.targetObject.GetType();
        var fieldType = objectType.GetField(property.propertyPath, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance).FieldType;
		
		if (fieldType.IsSubclassOf(typeof(Component))) {
			var component = property.serializedObject.targetObject as Component;
			if (component != null) {
				var components = component.GetComponents(fieldType);
				if (components.Length == 1) {
					actionName = "This";
					return components[0];
				}
			}
			
			actionName = "Fill";
			return findSceneObjectToFill(fieldType);
		}
		
		if (fieldType.IsSubclassOf(typeof(ScriptableObject))) {
			actionName = "Fill";
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
	
	static void logError(string error, GameObject obj, Component component, FieldInfo field) {
		Debug.LogError(error + " for " + field.Name + " in " + component.GetType().Name + " on " + obj.name + " in scene " + EditorSceneManager.GetActiveScene().name, component);
	}
	
	static void logError(string error, GameObject obj) {
		Debug.LogError(error + " on " + obj.name + " in scene " + EditorSceneManager.GetActiveScene().name, obj);
	}
}
#endif
