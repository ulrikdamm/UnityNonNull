using System.Collections;
using System.Collections.Generic;
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
[CustomPropertyDrawer(typeof(Object), useForChildren: true)]
public class DefaultObjectDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);
        
        var showWarning = (
            property.propertyType == SerializedPropertyType.ObjectReference
            && property.objectReferenceValue == null
            && property.serializedObject.targetObject.GetType().GetCustomAttribute(typeof(NonNullAttribute)) != null
        );
        
        if (showWarning) {
            GUI.backgroundColor = Color.red;
            
            var fillCandidate = findObjectToFill(property);
            if (fillCandidate != null) {
                var propertyRect = new Rect { x = position.x, y = position.y, width = position.width - 40, height = position.height };
                var buttonRect = new Rect { x = position.x + propertyRect.width + 8, y = position.y, width = 40 - 8, height = position.height };
                
                EditorGUI.PropertyField(propertyRect, property, label);
                
                GUI.backgroundColor = Color.white;
                if (GUI.Button(buttonRect, "Fill")) { property.objectReferenceValue = fillCandidate; }
            } else {
                EditorGUI.PropertyField(position, property, label);
                GUI.backgroundColor = Color.white;
            }
        } else {
            EditorGUI.PropertyField(position, property, label);
        }
        
		EditorGUI.EndProperty();
    }
    
    Object findObjectToFill(SerializedProperty property) {
		if (property.propertyPath.Contains(".Array")) { return null; }
		
        var objectType = property.serializedObject.targetObject.GetType();
        var fieldType = objectType.GetField(property.propertyPath, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance).FieldType;
        var objectsInScene = GameObject.FindObjectsOfType(fieldType);
        var objectsInAssets = AssetDatabase.FindAssets("t:" + fieldType.Name);
        
        if (objectsInScene.Length + objectsInAssets.Length != 1) { return null; }
        
        if (objectsInScene.Length == 1) {
            return objectsInScene[0];
        } else {
            var assetId = objectsInAssets[0];
            var path = AssetDatabase.GUIDToAssetPath(assetId);
            return AssetDatabase.LoadAssetAtPath(path, fieldType);
        }
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
		
		if (property.objectReferenceValue == null) { GUI.backgroundColor = Color.red; }
		
		EditorGUI.PropertyField(position, property, label);
		
		GUI.backgroundColor = Color.white;
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
            var componentHasNonNull = component.GetType().GetCustomAttribute(typeof(NonNullAttribute)) != null;
			var fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			
			for (var i = 0; i < fields.Length; i++) {
				var field = fields[i];
				if (!field.FieldType.IsClass) { continue; }
				
                if (!componentHasNonNull) {
                    var nonNullAttr = field.GetCustomAttribute(typeof(NonNullAttribute));
                    if (nonNullAttr == null) { continue; }
                } else {
                    var allowNullAttr = field.GetCustomAttribute(typeof(AllowNullAttribute));
                    if (allowNullAttr != null) { continue; }
                }
				
				var fieldValue = field.GetValue(component);
				
				if (fieldValue is UnityEngine.Object) {
					if (((Object)fieldValue) != null) { continue; }
				} else {
					if (!object.ReferenceEquals(fieldValue, null)) { continue; }
				}
				
				Debug.LogError("Missing reference for " + field.Name + " in " + component.GetType().Name + " on " + obj.name + " in scene " + EditorSceneManager.GetActiveScene().name, component);
				anyNulls = true;
			}
		});
		
		return anyNulls;
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
}
#endif
