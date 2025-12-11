/*
	Copyright © Carl Emil Carlsen 2023-2025
	http://cec.dk
*/

using UnityEngine;
using com.rfilkov.kinect;

namespace TrackingTools.AzureKinect
{
	public class AzureKinectIntrinsicsExtractor : MonoBehaviour
	{
		[SerializeField] string _colorIntrinsicsSaveName = "KinectAzureFactoryIntrinsics";
		[SerializeField] string _depthIntrinsicsSaveName = "KinectAzureFactoryIntrinsics";
		[SerializeField,Range(0,7)] int _sensorIndex = 0;

		static readonly string logPrepend = $"<b>[{nameof(AzureKinectIntrinsicsExtractor)}]</b>";


		void Update()
		{
			var kinectManager = KinectManager.Instance;
			if( !kinectManager || !kinectManager.IsInitialized() ) return;

			int sensorCount = kinectManager.GetSensorCount();
			if( _sensorIndex >= sensorCount ){
				Debug.LogWarning( $"{logPrepend} Aborting. Sensor index is out of range. Sensor count: {kinectManager.GetSensorCount()}.\n" );
				enabled = false;
				return;
			}
		
			var sensorData = kinectManager.GetSensorData( _sensorIndex );
			if( sensorData == null ){
				Debug.LogWarning( $"{logPrepend} Aborting. SensorData is null.\n" );
				enabled = false;
				return;
			}

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

			colorIntrinsicsSaveName += $"_D{sensorData.sensorId}_Color";
			depthIntrinsicsSaveName += $"_D{sensorData.sensorId}_Depth";

			if( sensorData.sensorInterface is Kinect4AzureInterface ) {
				var kinectInterface = sensorData.sensorInterface as Kinect4AzureInterface;
				colorIntrinsicsSaveName += kinectInterface.colorCameraMode;
				depthIntrinsicsSaveName += kinectInterface.depthCameraMode;
			} else if( sensorData.sensorInterface is RealSenseInterface ) {
				var realsenseInterface = sensorData.sensorInterface as RealSenseInterface;
				colorIntrinsicsSaveName += realsenseInterface.colorCameraMode;
				depthIntrinsicsSaveName += realsenseInterface.depthCameraMode;
			}

			string colorFilePath = colorIntrinsics.SaveToFile( colorIntrinsicsSaveName );
			string depthFilePath = depthIntrinsics.SaveToFile( depthIntrinsicsSaveName );

			enabled = false;

			Debug.Log( $"{logPrepend} Depth and color intrinsics saved at paths:\n{colorFilePath}\n{depthFilePath}" );
		}
	}
}