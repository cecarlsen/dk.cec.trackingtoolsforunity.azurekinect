/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

using UnityEngine;
using com.rfilkov.kinect;
using TrackingTools;

public class KinectAzureIntrinsicsExtractor : MonoBehaviour
{
	[SerializeField] string _colorIntrinsicsSaveName = "KinectAzureIntrinsics_Color";
	[SerializeField] string _depthIntrinsicsSaveName = "KinectAzureIntrinsics_Depth";


	void Update()
	{
		KinectManager kinectManager = KinectManager.Instance;
		if( !kinectManager || !kinectManager.IsInitialized() ) return;
		
		KinectInterop.SensorData sensorData = kinectManager.GetSensorData( sensorIndex: 0 );

		var kinectInterface = sensorData.sensorInterface as Kinect4AzureInterface;
		var colorCameraMode = kinectInterface.colorCameraMode;
		var depthCameraMode = kinectInterface.depthCameraMode;

		Intrinsics colorIntrinsics = new Intrinsics();
		Intrinsics depthIntrinsics = new Intrinsics();
		colorIntrinsics.UpdateFromAzureKinectExamples( sensorData.colorCamIntr );
		depthIntrinsics.UpdateFromAzureKinectExamples( sensorData.depthCamIntr );

		string colorFilePath = colorIntrinsics.SaveToFile( _colorIntrinsicsSaveName + colorCameraMode );
		string depthFilePath = depthIntrinsics.SaveToFile( _depthIntrinsicsSaveName + depthCameraMode );

		enabled = false;

		Debug.Log( "Depth and color intrinsics saved at paths:\n" + colorFilePath + "\n" + depthFilePath );
	}
}