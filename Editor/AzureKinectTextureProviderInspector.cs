/*
	Copyright © Carl Emil Carlsen 2023-2024
	http://cec.dk
*/

using com.rfilkov.kinect;
using UnityEditor;
using UnityEngine;

namespace TrackingTools.AzureKinect
{
	[CustomEditor( typeof( AzureKinectTextureProvider ) )]
	[CanEditMultipleObjects]
	public class AzureKinectTextureProviderInspector : Editor
	{
		SerializedProperty _sensorIdProp;
		SerializedProperty _streamProp;
		SerializedProperty _undistortProp;
		SerializedProperty _frameHistoryCapacityProp;
		SerializedProperty _flipVerticallyProp;
		SerializedProperty _latestTextureEventProp;
		SerializedProperty _depthRangeEventProp;

		AzureKinectTextureProvider _provider;

		const int executionOrderNum = -1000;

		const string logPrepend = "<b>[" + nameof( AzureKinectTextureProvider ) + "]</b> ";


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
			_undistortProp = serializedObject.FindProperty( "_undistort" );
			_frameHistoryCapacityProp = serializedObject.FindProperty( "_frameHistoryCapacity" );
			_flipVerticallyProp = serializedObject.FindProperty( "_flipVertically" );
			_latestTextureEventProp = serializedObject.FindProperty( "_latestTextureEvent" );
			_depthRangeEventProp = serializedObject.FindProperty( "_depthRangeEvent" );

			_provider = target as AzureKinectTextureProvider;
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
			EditorGUILayout.PropertyField( _undistortProp );
			EditorGUILayout.PropertyField( _flipVerticallyProp );

			EditorGUILayout.Space();
			EditorGUILayout.LabelField( "Events", EditorStyles.boldLabel );
			EditorGUILayout.PropertyField( _latestTextureEventProp );
			if( ( (AzureKinectTextureProvider.Stream) _streamProp.enumValueIndex ) == AzureKinectTextureProvider.Stream.Depth ) {
				EditorGUILayout.PropertyField( _depthRangeEventProp );
			}
			
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