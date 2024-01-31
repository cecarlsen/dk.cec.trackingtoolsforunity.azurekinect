/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk

	Kinect4AzureInterface sets deviceSyncMode to Standalone at random times.
	This is because WiredSyncMode is not serializable!
*/

using com.rfilkov.kinect;
using Microsoft.Azure.Kinect.Sensor;
using UnityEngine;

namespace TrackingTools
{
	[ExecuteAlways]
	[RequireComponent(typeof(Kinect4AzureInterface))]
	public class ForceKinectDeviceSyncMode : MonoBehaviour
	{
		[SerializeField] SyncMode _syncMode = SyncMode.Standalone;

		Kinect4AzureInterface _interface;

		[System.Serializable] public enum SyncMode
		{
			Standalone,
			Master,
			Subordinate
		}


		void Update()
		{
			if( !_interface ) _interface = GetComponent<Kinect4AzureInterface>();
			if( !_interface ) return;

			if( (int) _interface.deviceSyncMode != (int) _syncMode ) _interface.deviceSyncMode = (WiredSyncMode) (int) _syncMode;
		}
	}
}