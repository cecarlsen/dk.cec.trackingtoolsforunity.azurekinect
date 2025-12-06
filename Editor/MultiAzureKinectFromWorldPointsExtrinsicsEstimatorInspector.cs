/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

using UnityEditor;
using UnityEngine;

namespace TrackingTools.AzureKinect
{
	[CustomEditor( typeof( MultiAzureKinectFromWorldPointsExtrinsicsEstimator ) )]
	public class MultiAzureKinectFromWorldPointsExtrinsicsEstimatorInspector : Editor
	{
		SerializedProperty _textureProviderProp;
		SerializedProperty _resourcesProp;
		SerializedProperty _onEnableResourceIndexProp;


		void OnEnable()
		{
			_textureProviderProp = serializedObject.FindProperty( "_textureProvider" );
			_resourcesProp = serializedObject.FindProperty( "_resources" );
			_onEnableResourceIndexProp = serializedObject.FindProperty( "_onEnableResourceIndex" );
		}


		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.LabelField( "Setup", EditorStyles.boldLabel );

			EditorGUI.BeginDisabledGroup( Application.isPlaying );
			EditorGUILayout.PropertyField( _textureProviderProp );
			EditorGUILayout.PropertyField( _resourcesProp );
			EditorGUILayout.PropertyField( _onEnableResourceIndexProp );
			EditorGUI.EndDisabledGroup();

			serializedObject.ApplyModifiedProperties();
		}
	}
}