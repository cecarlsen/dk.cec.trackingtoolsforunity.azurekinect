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
		[SerializeField] int _onEnableResourceIndex = -1;

		CameraFromWorldPointsExtrinsicsEstimator _cameraEstimator;
		ExtrinsicsSaver _extrinsicsSaver;

		int _activeResourceIndex = -1;

		static readonly string logPrepend = $"<b>[{nameof(MultiAzureKinectFromWorldPointsExtrinsicsEstimator)}]</b>";


		[System.Serializable]
		public class Resource
		{
			public int sensorIndex = 0;
			public string physicalCameraIntrinsicsFileName = "DefaultCamera";
			public string calibrationPointsFileName = "DefaultPoints";
			public string extrinsicsFileName = "Default";
			public Transform[] worldPointTransforms = null;
			[Tooltip("Position and location used if no extrinsics exists.")] public Transform defaultCameraTransform = null;
			public Key hotKeyCode = Key.Digit1;
		}


		void OnEnable()
		{
			_cameraEstimator = GetComponent<CameraFromWorldPointsExtrinsicsEstimator>();

			_extrinsicsSaver = _cameraEstimator.virtualCamera.GetComponent<ExtrinsicsSaver>();
			if( !_extrinsicsSaver ) _extrinsicsSaver = _cameraEstimator.virtualCamera.gameObject.AddComponent<ExtrinsicsSaver>();

			if( _onEnableResourceIndex >= 0 && _onEnableResourceIndex < _resources.Length ) SetActiveResource( _onEnableResourceIndex );
		}


		void OnDisable()
		{
			if( _activeResourceIndex >= 0 ) SaveResourceExtrinsics( _activeResourceIndex );

			// Force load all extrinsics in entire scene.
			var extrinsicsLoaders = FindObjectsByType<ExtrinsicsLoader>( FindObjectsInactive.Include, FindObjectsSortMode.None );
			foreach( var loader in extrinsicsLoaders ) if( loader.enabled && loader.FileExists() ) loader.LoadAndApply();
		}


		void Update()
		{
			for( int r = 0; r < _resources.Length; r++ )
			{
				var resource = _resources[ r ];
				if( Keyboard.current[ resource.hotKeyCode ].wasPressedThisFrame ){
					if( resource.sensorIndex < _textureProvider.GetActiveSensorCount() ){
						if( _activeResourceIndex >= 0 ) SaveResourceExtrinsics( _activeResourceIndex );
						SetActiveResource( r );
					}
				}
			}
		}



		public void SetWorldPointTransforms( int resourceIndex, Transform[] transforms )
		{
			if( resourceIndex >= _resources.Length ) return;

			_resources[ resourceIndex ].worldPointTransforms = transforms;
		}


		void SetActiveResource( int resourceIndex )
		{
			Resource resource = _resources[ resourceIndex ];

			_textureProvider.sensorIndex = resource.sensorIndex;
			_extrinsicsSaver.extrinsicsFileName = resource.extrinsicsFileName;
			if( resource.defaultCameraTransform ) _cameraEstimator.virtualCamera.transform.SetPositionAndRotation( resource.defaultCameraTransform.position, resource.defaultCameraTransform.rotation );
			_cameraEstimator.SetWorldPointTransforms( resource.worldPointTransforms );
			_cameraEstimator.SetPhysicalCameraIntrinsicsFileName( resource.physicalCameraIntrinsicsFileName );
			_cameraEstimator.SetCalibrationPointFileName( resource.calibrationPointsFileName );

			_activeResourceIndex = resourceIndex;
		}


		void SaveResourceExtrinsics( int resourceIndex )
		{
			Resource resource = _resources[ resourceIndex ];
			var extrinsics = new Extrinsics();
			extrinsics.UpdateFromTransform( _cameraEstimator.virtualCamera.transform );
			string filePath = extrinsics.SaveToFile( resource.extrinsicsFileName );
			Debug.Log( $"{logPrepend} Updated intrinsics for sensor index {resource.sensorIndex}.\n" );
		}
	}
}