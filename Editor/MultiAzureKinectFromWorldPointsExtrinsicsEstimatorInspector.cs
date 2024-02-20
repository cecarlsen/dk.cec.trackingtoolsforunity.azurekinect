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
		SerializedProperty _extrinsicsSaverProp;
		SerializedProperty _resourcesProp;


		void OnEnable()
		{
			_textureProviderProp = serializedObject.FindProperty( "_textureProvider" );
			_extrinsicsSaverProp = serializedObject.FindProperty( "_extrinsicsSaver" );
			_resourcesProp = serializedObject.FindProperty( "_resources" );
		}


		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.LabelField( "Setup", EditorStyles.boldLabel );

			EditorGUI.BeginDisabledGroup( Application.isPlaying );
			EditorGUILayout.PropertyField( _textureProviderProp );
			EditorGUILayout.PropertyField( _extrinsicsSaverProp );
			EditorGUILayout.PropertyField( _resourcesProp );
			EditorGUI.EndDisabledGroup();

			serializedObject.ApplyModifiedProperties();
		}
	}
}