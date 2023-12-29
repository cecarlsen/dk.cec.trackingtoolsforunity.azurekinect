/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

using com.rfilkov.kinect;
using UnityEditor;
using UnityEngine;

namespace TrackingTools.AzureKinect
{
	[CustomEditor( typeof( KinectAzureTextureProvider ) )]
	public class KinectAzureTextureProviderInspector : Editor
	{
		const int executionOrderNum = -1000;

		const string logPrepend = "<b>[" + nameof( KinectAzureTextureProvider ) + "]</b> ";

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
		}
	}
}