/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools.AzureKinect
{
	[RequireComponent(typeof( CameraFromWorldPointsExtrinsicsEstimator ) )]
	public class MultiAzureKinectFromWorldPointsExtrinsicsEstimator : MonoBehaviour
	{
		[SerializeField] AzureKinectTextureProvider _textureProvider;
		[SerializeField] ExtrinsicsSaver _extrinsicsSaver;
		[SerializeField] Resource[] _resources;

		CameraFromWorldPointsExtrinsicsEstimator _cameraEstimator;


		[System.Serializable]
		public class Resource
		{
			public int sensorIndex = 0;
			public string physicalCameraIntrinsicsFileName = "DefaultCamera";
			public string calibrationPointsFileName = "DefaultPoints";
			public string extrinsicsFileName = "Default";
			public KeyCode hotKeyCode = KeyCode.Alpha1;
		}


		void Awake()
		{
			_cameraEstimator = GetComponent<CameraFromWorldPointsExtrinsicsEstimator>();
		}


		void Start()
		{
			SetResource( _resources[ 0 ] );
		}


		void Update()
		{
			foreach( var resource in _resources )
			{
				if( Input.GetKeyDown( resource.hotKeyCode ) ) SetResource( resource );
			}
		}


		void SetResource( Resource resource )
		{
			_textureProvider.sensorIndex = resource.sensorIndex;
			_extrinsicsSaver.extrinsicsFileName = resource.extrinsicsFileName;
			_cameraEstimator.SetPhysicalCameraIntrinsicsFileName( resource.physicalCameraIntrinsicsFileName );
			_cameraEstimator.SetCalibrationPointFileName( resource.calibrationPointsFileName );
		}
	}
}