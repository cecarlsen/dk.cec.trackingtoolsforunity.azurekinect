/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

using UnityEngine;
using com.rfilkov.kinect;

namespace TrackingTools.AzureKinect
{
	public class AzureKinectExtrinsicsExtractor : MonoBehaviour
	{
		[SerializeField] string _colorToDepthExtrinsicsSaveName = "KinectAzureExtrinsics_ColorToDepth";
		[SerializeField] string _depthToColorExtrinsicsSaveName = "KinectAzureExtrinsics_DepthToColor";

		const float millimeterToMeters = 0.001f;


		void Update()
		{
			KinectManager kinectManager = KinectManager.Instance;
			if( !kinectManager || !kinectManager.IsInitialized() ) return;
		
			KinectInterop.SensorData sensorData = kinectManager.GetSensorData( sensorIndex: 0 );

			// Note: If we presume the Kinect is placed like a Unity camera where the lens is pointing along positive Z, then the translation is flipped. So we flip it!
			Extrinsics depthToColorExtrinsics = new Extrinsics();
			depthToColorExtrinsics.Update( -ValuesToVector3( sensorData.depth2ColorExtr.translation ) * millimeterToMeters, RotationMatrixValuesToQuaternion( sensorData.depth2ColorExtr.rotation ) );

			// Because color2DepthExtr.translation contains (0,0,0) - probably a bug - we just take the inverse of depth2ColorExtr.
			//colorToDepthExtrinsics.Update( ValuesToVector3( sensorData.color2DepthExtr.translation ), RotationMatrixValuesToQuaternion( sensorData.depth2ColorExtr.rotation ) );
			Extrinsics colorToDepthExtrinsics = new Extrinsics();
			colorToDepthExtrinsics.CopyFrom( depthToColorExtrinsics );
			colorToDepthExtrinsics.Inverse();

			string colorFilePath = colorToDepthExtrinsics.SaveToFile( _colorToDepthExtrinsicsSaveName );
			string depthFilePath = depthToColorExtrinsics.SaveToFile( _depthToColorExtrinsicsSaveName );

			enabled = false;

			Debug.Log( "Color to depth and depth to color extrinsics saved at paths:\n" + colorFilePath + "\n" + depthFilePath );
		}


		Vector3 ValuesToVector3( float[] values )
		{
			return new Vector3( values[ 0 ], values[ 1 ], values[ 2 ] );
		}


		Quaternion RotationMatrixValuesToQuaternion( float[] values )
		{
			Matrix4x4 rotMat = Matrix4x4.identity;
			rotMat.m00 = values[ 0 ];
			rotMat.m01 = values[ 1 ];
			rotMat.m02 = values[ 2 ];
			rotMat.m10 = values[ 3 ];
			rotMat.m11 = values[ 4 ];
			rotMat.m12 = values[ 5 ];
			rotMat.m20 = values[ 6 ];
			rotMat.m21 = values[ 7 ];
			rotMat.m22 = values[ 8 ];
			return rotMat.rotation;
		}
	}
}