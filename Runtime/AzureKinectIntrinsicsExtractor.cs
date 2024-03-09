/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

using UnityEngine;
using com.rfilkov.kinect;

namespace TrackingTools.AzureKinect
{
	public class AzureKinectIntrinsicsExtractor : MonoBehaviour
	{
		[SerializeField] string _colorIntrinsicsSaveName = "KinectAzureIntrinsics_Color";
		[SerializeField] string _depthIntrinsicsSaveName = "KinectAzureIntrinsics_Depth";


		void Update()
		{
			KinectManager kinectManager = KinectManager.Instance;
			if( !kinectManager || !kinectManager.IsInitialized() ) return;
		
			KinectInterop.SensorData sensorData = kinectManager.GetSensorData( sensorIndex: 0 );

			Intrinsics colorIntrinsics = new Intrinsics();
			Intrinsics depthIntrinsics = new Intrinsics();
			if(
				!colorIntrinsics.UpdateFromAzureKinectExamples( sensorData.colorCamIntr ) ||
				!depthIntrinsics.UpdateFromAzureKinectExamples( sensorData.depthCamIntr )
			){
				enabled = false;
				return;
			}

			string colorIntrinsicsSaveName = _colorIntrinsicsSaveName;
			string depthIntrinsicsSaveName = _depthIntrinsicsSaveName;
			if( sensorData.sensorInterface is Kinect4AzureInterface ) {
				var kinectInterface = sensorData.sensorInterface as Kinect4AzureInterface;
				var colorCameraMode = kinectInterface.colorCameraMode;
				var depthCameraMode = kinectInterface.depthCameraMode;
				colorIntrinsicsSaveName += colorCameraMode;
				depthIntrinsicsSaveName += depthCameraMode;
			} else if( sensorData.sensorInterface is RealSenseInterface ) {
				var realsenseInterface = sensorData.sensorInterface as RealSenseInterface;
				var colorCameraMode = realsenseInterface.colorCameraMode;
				var depthCameraMode = realsenseInterface.depthCameraMode;
				colorIntrinsicsSaveName += colorCameraMode;
				depthIntrinsicsSaveName += depthCameraMode;
			}

			string colorFilePath = colorIntrinsics.SaveToFile( colorIntrinsicsSaveName );
			string depthFilePath = depthIntrinsics.SaveToFile( depthIntrinsicsSaveName );

			enabled = false;

			Debug.Log( "Depth and color intrinsics saved at paths:\n" + colorFilePath + "\n" + depthFilePath );
		}
	}
}