/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

using com.rfilkov.kinect;
using UnityEditor;
using UnityEngine;

namespace TrackingTools.AzureKinect
{
	[CustomEditor( typeof( AzureKinectTexture2DProvider ) )]
	public class AzureKinectTexture2DProviderInspector : Editor
	{
		SerializedProperty _sensorIdProp;
		SerializedProperty _streamProp;
		SerializedProperty _convertToR8Prop;
		SerializedProperty _undistortProp;
		SerializedProperty _infrared16BitScalarProp;
		SerializedProperty _frameHistoryCapacityProp;
		SerializedProperty _flipVerticallyProp;
		SerializedProperty _latestTextureEventProp;

		AzureKinectTexture2DProvider _provider;

		const int executionOrderNum = -1000;

		const string logPrepend = "<b>[" + nameof( AzureKinectTexture2DProvider ) + "]</b> ";


		void OnEnable()
		{
			// Ensure that KinectAzureTextureProvider scripts will be executed early, so it can deliver messages before we compute anything.
			MonoScript providerScript = MonoScript.FromMonoBehaviour( target as MonoBehaviour );
			if( MonoImporter.GetExecutionOrder( providerScript ) != executionOrderNum ) {
				MonoImporter.SetExecutionOrder( providerScript, executionOrderNum );
				Debug.Log( logPrepend + " Updated execution order to " + executionOrderNum + ".\n" );
			}

			// Ensure that KinectManager is executed before KinectAzureTextureProvider.
			var kinectManager = FindFirstObjectByType<KinectManager>( FindObjectsInactive.Include );
			if( kinectManager ) {
				MonoScript managerScript = MonoScript.FromMonoBehaviour( kinectManager );
				if( MonoImporter.GetExecutionOrder( managerScript ) > executionOrderNum ) {
					MonoImporter.SetExecutionOrder( managerScript, executionOrderNum - 1 );
					Debug.Log( logPrepend + " Updated execution order of KinectManager to " + ( executionOrderNum - 1 ) + ".\n" );
				}
			}

			_sensorIdProp = serializedObject.FindProperty( "_sensorId" );
			_streamProp = serializedObject.FindProperty( "_stream" );
			_convertToR8Prop = serializedObject.FindProperty( "_convertToR8" );
			_undistortProp = serializedObject.FindProperty( "_undistort" );
			_flipVerticallyProp = serializedObject.FindProperty( "_flipVertically" );
			_infrared16BitScalarProp = serializedObject.FindProperty( "_infrared16BitScalar" );
			_frameHistoryCapacityProp = serializedObject.FindProperty( "_frameHistoryCapacity" );
			_latestTextureEventProp = serializedObject.FindProperty( "_latestTextureEvent" );

			_provider = target as AzureKinectTexture2DProvider;
		}


		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.LabelField( "Setup", EditorStyles.boldLabel );

			EditorGUI.BeginDisabledGroup( Application.isPlaying );
			EditorGUILayout.PropertyField( _sensorIdProp );
			EditorGUILayout.PropertyField( _streamProp );
			EditorGUILayout.PropertyField( _frameHistoryCapacityProp );
			EditorGUI.EndDisabledGroup();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField( "Parameters", EditorStyles.boldLabel );
			EditorGUILayout.PropertyField( _flipVerticallyProp );
			EditorGUILayout.PropertyField( _convertToR8Prop );
			if( _streamProp.enumValueIndex == (int) AzureKinectTexture2DProvider.Stream.Infrared && _convertToR8Prop.boolValue ) {
				EditorGUILayout.PropertyField( _infrared16BitScalarProp );
			}
			EditorGUILayout.PropertyField( _undistortProp );

			EditorGUILayout.Space();
			EditorGUILayout.LabelField( "Events", EditorStyles.boldLabel );
			EditorGUILayout.PropertyField( _latestTextureEventProp );
			
			serializedObject.ApplyModifiedProperties();

			if( Application.isPlaying && _provider.enabled ) {
				EditorGUILayout.Space();

				EditorGUILayout.LabelField( "Info", EditorStyles.boldLabel );

				EditorGUI.BeginDisabledGroup( true );

				EditorGUILayout.LabelField( "Frame Interval", ( _provider.latestFrameInterval * 1000 ).ToString( "F2" ) + "ms" );
				EditorGUILayout.LabelField( "Frame Rate", ( 1d / _provider.latestFrameInterval ).ToString( "F1" ) );
				EditorGUILayout.LabelField( "Frames Since Last Update", _provider.framesSinceLastUnityUpdate.ToString() );

				EditorGUI.EndDisabledGroup();
			}
		}
	}
}