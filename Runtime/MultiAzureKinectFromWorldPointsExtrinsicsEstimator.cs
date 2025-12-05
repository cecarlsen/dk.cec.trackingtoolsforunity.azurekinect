/*
	Copyright © Carl Emil Carlsen 2024-2025
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.InputSystem;


namespace TrackingTools.AzureKinect
{
	[RequireComponent(typeof( CameraFromWorldPointsExtrinsicsEstimator ) )]
	public class MultiAzureKinectFromWorldPointsExtrinsicsEstimator : MonoBehaviour
	{
		[SerializeField] AzureKinectTextureProvider _textureProvider;
		[SerializeField] Resource[] _resources;

		CameraFromWorldPointsExtrinsicsEstimator _cameraEstimator;

		ExtrinsicsSaver _extrinsicsSaver;


		[System.Serializable]
		public class Resource
		{
			public int sensorIndex = 0;
			public string physicalCameraIntrinsicsFileName = "DefaultCamera";
			public string calibrationPointsFileName = "DefaultPoints";
			public string extrinsicsFileName = "Default";
			public Transform[] worldPointTransforms = null;
			public Key hotKeyCode = Key.Digit1;
		}


		void Awake()
		{
			_cameraEstimator = GetComponent<CameraFromWorldPointsExtrinsicsEstimator>();

			_extrinsicsSaver = _cameraEstimator.virtualCamera.GetComponent<ExtrinsicsSaver>();
			if( !_extrinsicsSaver ) _extrinsicsSaver = _cameraEstimator.virtualCamera.gameObject.AddComponent<ExtrinsicsSaver>();
		}


		void Start()
		{
			SetResource( _resources[ 0 ] );
		}


		void Update()
		{
			foreach( var resource in _resources )
			{
				if( Keyboard.current[ resource.hotKeyCode ].wasPressedThisFrame ) SetResource( resource );
			}
		}


		public void SetWorldPointTransforms( int resourceIndex, Transform[] transforms )
		{
			if( resourceIndex >= _resources.Length ) return;

			_resources[ resourceIndex ].worldPointTransforms = transforms;
		}


		void SetResource( Resource resource )
		{
			_textureProvider.sensorIndex = resource.sensorIndex;
			_extrinsicsSaver.extrinsicsFileName = resource.extrinsicsFileName;
			_cameraEstimator.SetWorldPointTransforms( resource.worldPointTransforms );
			_cameraEstimator.SetPhysicalCameraIntrinsicsFileName( resource.physicalCameraIntrinsicsFileName );
			_cameraEstimator.SetCalibrationPointFileName( resource.calibrationPointsFileName );
		}
	}
}