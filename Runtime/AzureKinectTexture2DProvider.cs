/*
	Copyright © Carl Emil Carlsen 2020-2022
	http://cec.dk
*/

using System;
using UnityEngine;
using UnityEngine.Events;
using com.rfilkov.kinect;
using UnityEngine.Experimental.Rendering;
using OpenCVForUnity.CoreModule;
using System.Runtime.InteropServices;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UtilsModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Calib3dModule;
using static com.rfilkov.kinect.KinectInterop;

namespace TrackingTools.AzureKinect
{
	public class AzureKinectTexture2DProvider : CameraTexture2DProvider
	{
		// Setup.
		[SerializeField] int _sensorId = 0;
		[SerializeField] Stream _stream = Stream.Color;

		// Parameters
		[SerializeField] bool _undistort = false;

		// Events.
		[SerializeField] UnityEvent<Texture2D> _latestTextureEvent = new UnityEvent<Texture2D>();

		Texture2D _latestTexture;

		Texture2D _sourceTexture;
		byte[] _rawImageDataBytes;

		Mat _undistortSourceMat, _undistortTargetMat;
		Mat _undistortMapX, _undistortMapY;

		ulong _latestFrameTimeMicroSeconds;
		ulong _previousFrameTimeMicroSeconds;

		long _latestFrameNum = 0; // The kinect does not provide a frame numer, only time, so we do the counting ourselves.
		int _framesSinceLastUnityUpdate = 0;

		const string logPrepend = "<b>[" + nameof( AzureKinectTexture2DProvider ) + "]</b> ";


		/// <summary>
		/// Number of frames counted since last Unity update.
		/// </summary>
		public override int framesSinceLastUnityUpdate => _framesSinceLastUnityUpdate;

		/// <summary>
		/// Number of frames aquired and available since last Unity update.
		/// </summary>
		public override int framesAquiredSinceLastUnityUpdate => _framesSinceLastUnityUpdate;

		/// <summary>
		/// Interval between two latest frames in seconds.
		/// </summary>
		public override double latestFrameInterval => ( _latestFrameTimeMicroSeconds - _previousFrameTimeMicroSeconds ) * 0.0000001d;

		/// <summary>
		/// Get latest frame number.
		/// </summary>
		public override long latestFrameNumber => _latestFrameNum;



		[Serializable]
		public enum Stream { Color, Infrared }


		/// <summary>
		/// Get latest aquired frame.
		/// </summary>
		public override Texture GetLatestTexture() => _latestTexture;


		/// <summary>
		/// Get texture at history index. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public override Texture GetHistoryTexture( int historyIndex ) {
			if( historyIndex != 0 ) throw new Exception( "AzureKinectProvider only stores one frame." );

			return _latestTexture;
		}


		/// <summary>
		/// Get latest aquired frame.
		/// </summary>
		public override Texture2D GetLatestTexture2D() => _latestTexture;


		/// <summary>
		/// Get texture at history index. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public override Texture2D GetHistoryTexture2D( int historyIndex ) {
			if( historyIndex != 0 ) throw new Exception( "AzureKinectProvider only stores one frame." );

			return _latestTexture;
		}


		/// <summary>
		/// Get latest frame time in seconds, measured relative to capture begin time. 
		/// </summary>
		public override double GetLatestFrameTime() => _latestFrameTimeMicroSeconds * 0.0000001d;


		/// <summary>
		/// Get history frame time in seconds, measured relative to capture begin time. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public override double GetHistoryFrameTime( int historyIndex ) {
			if( historyIndex != 0 ) throw new System.Exception( "AzureKinectProvider only stores one frame." );

			return _latestFrameTimeMicroSeconds * 0.0000001d;
		}


		void Awake()
		{
			
		}


		void OnEnable()
		{

		}


		void OnDisable()
		{
			_framesSinceLastUnityUpdate = 0;
		}


		void Update()
		{
			KinectManager kinectManager = KinectManager.Instance;
			if( !kinectManager || !kinectManager.IsInitialized() ) return;

			switch( _stream )
			{
				case Stream.Color:
					if( kinectManager.getColorFrames == KinectManager.ColorTextureType.None ) kinectManager.getColorFrames = KinectManager.ColorTextureType.ColorTexture;
					break;
				case Stream.Infrared:
					if( kinectManager.getInfraredFrames != KinectManager.InfraredTextureType.None ) kinectManager.getInfraredFrames = KinectManager.InfraredTextureType.RawInfraredData;
					break;
			}

			SensorData sensorData = kinectManager.GetSensorData( _sensorId );

			// Update texture.
			bool hasNewFrame = false;
			switch( _stream ) {
				case Stream.Color: hasNewFrame = UpdateColorTexture( kinectManager, sensorData ); break;
				case Stream.Infrared: hasNewFrame = UpdateInfraredTexture( kinectManager, sensorData ); break;
			}

			if( hasNewFrame ) {
				_latestFrameNum++;
				_framesSinceLastUnityUpdate = 1;
				_latestTextureEvent.Invoke( _latestTexture );
			} else {
				_framesSinceLastUnityUpdate = 0;
			}
		}


		bool UpdateColorTexture( KinectManager kinectManager, SensorData sensorData )
		{
			if( sensorData.lastColorFrameTime == _latestFrameTimeMicroSeconds ) return false;
			
			// The color texture is already a Texture2D, so we just output it.
			Texture2D colorTexture = kinectManager.GetColorImageTex( _sensorId ) as Texture2D;
			if( !colorTexture ) return false;

			if( string.IsNullOrEmpty( colorTexture.name ) ) colorTexture.name = "KinectColor";

			if( _undistort )
			{
				if( !_sourceTexture ) {
					_sourceTexture = new Texture2D( colorTexture.width, colorTexture.height, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None );
					_sourceTexture.name = "KinectColor";
				}

				// Ensure undistort resources.
				if( _undistortSourceMat == null || _undistortSourceMat.width() != colorTexture.width || _undistortSourceMat.height() != colorTexture.height ) {
					InitUndistortResources( colorTexture.width, colorTexture.height, CvType.CV_8UC4, sensorData.colorCamIntr );
				}

				Utils.texture2DToMat( colorTexture, _undistortSourceMat, flipAfter: false );

				// Undistort
				Imgproc.remap( _undistortSourceMat, _undistortTargetMat, _undistortMapX, _undistortMapY, Imgproc.INTER_LINEAR );

				// Copy to texture.
				Utils.fastMatToTexture2D( _undistortTargetMat, _sourceTexture, flip: false );
			}

			_latestTexture = _undistort ? _sourceTexture : colorTexture;

			_previousFrameTimeMicroSeconds = _latestFrameTimeMicroSeconds;
			_latestFrameTimeMicroSeconds = sensorData.lastColorFrameTime;

			return true;
		}


		bool UpdateInfraredTexture( KinectManager kinectManager, SensorData sensorData )
		{
			if( sensorData.lastInfraredFrameTime == _latestFrameTimeMicroSeconds ) return false;
			
			// Azure Kinect Examples unpacks IR on the GPU into a RenderTexture. We need a Texture2D. Also:

			// Azure Kinect Examples loads IR as RGB24, but the IR is actually R16, so we loose a lot of information.
			// If you look IR in the official Azure Kinect Viewver it matches Azure Kinet Examples. They are very bright, and typically burned out.
			// We want the full spectrum, so we load 16bit directly to texture.
			int width = sensorData.depthImageWidth;
			int height = sensorData.depthImageHeight;
				
			// Create texture.
			if( !_sourceTexture ) {
				int pixelCount = width * height;
				_sourceTexture = new Texture2D( width, height, GraphicsFormat.R16_UNorm, TextureCreationFlags.None );
				_sourceTexture.name = "KinectInfrared";
				_rawImageDataBytes = new byte[ pixelCount * 2 ];
			}

			// Get raw image data.
			ushort[] rawImageData = kinectManager.GetRawInfraredMap( _sensorId );

			if( _undistort )
			{		
				// Ensure undistort resources.
				if( _undistortSourceMat == null || _undistortSourceMat.width() != width || _undistortSourceMat.height() != height ){
					InitUndistortResources( width, height, CvType.CV_16U, sensorData.depthCamIntr );
				}

				// Copy to mat.
				GCHandle arrayHandle = GCHandle.Alloc( rawImageData, GCHandleType.Pinned );
				MatUtils.copyToMat( arrayHandle.AddrOfPinnedObject(), _undistortSourceMat );
				arrayHandle.Free();

				// Undistort
				Imgproc.remap( _undistortSourceMat, _undistortTargetMat, _undistortMapX, _undistortMapY, Imgproc.INTER_LINEAR );

				// Copy to texture.
				Utils.fastMatToTexture2D( _undistortTargetMat, _sourceTexture, flip: false );
					
			} else {
			
				// ushort[] to byte[].
				// https://stackoverflow.com/questions/37213819/convert-ushort-into-byte-and-back
				Buffer.BlockCopy( rawImageData, 0, _rawImageDataBytes, 0, rawImageData.Length * 2 );

				// Load into texture.
				_sourceTexture.LoadRawTextureData( _rawImageDataBytes );
				_sourceTexture.Apply();
			}

			_latestTexture = _sourceTexture;
			_previousFrameTimeMicroSeconds = _latestFrameTimeMicroSeconds;
			_latestFrameTimeMicroSeconds = sensorData.lastInfraredFrameTime;

			return true;
		}



		void InitUndistortResources( int w, int h, int cvType, CameraIntrinsics rfilkovIntrinsics )
		{
			_undistortSourceMat?.Dispose();
			_undistortTargetMat?.Dispose();
			_undistortSourceMat = new Mat( h, w, cvType );
			_undistortTargetMat = new Mat( h, w, cvType );

			if( _undistortMapX == null ) {
				_undistortMapX = new Mat();
				_undistortMapY = new Mat();
			}
			Intrinsics intrinsics = new Intrinsics();
			intrinsics.UpdateFromAzureKinectExamples( rfilkovIntrinsics );
			Mat _sensorMat = new Mat();
			MatOfDouble _distortionCoeffsMat = new MatOfDouble();
			bool success = intrinsics.ApplyToToOpenCV( ref _sensorMat, ref _distortionCoeffsMat );//, w, h );
			if( !success ) {
				Debug.LogWarning( logPrepend + "Failed loading intrinsics\n" );
				return;
			}
			Calib3d.initUndistortRectifyMap( _sensorMat, _distortionCoeffsMat, new Mat(), _sensorMat, new Size( w, h ), CvType.CV_32FC1, _undistortMapX, _undistortMapY );
			_sensorMat.Dispose();
			_distortionCoeffsMat.Dispose();
		}




		void OnDestroy()
		{
			if( _sourceTexture ) Destroy( _sourceTexture );
			_undistortSourceMat?.Dispose();
			_undistortTargetMat?.Dispose();
			_undistortMapX?.Dispose();
			_undistortMapY?.Dispose();
		}
	}

}