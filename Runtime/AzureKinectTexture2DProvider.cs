/*
	Copyright © Carl Emil Carlsen 2020-2024
	http://cec.dk
*/

using System;
using UnityEngine;
using UnityEngine.Events;
using com.rfilkov.kinect;
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
		[SerializeField] bool _flipVertically = false;
		[SerializeField] bool _undistort = false;
		[SerializeField] bool _convertToR8 = false;
		[SerializeField,Range(0f,50f)] float _infrared16BitScalar = 25f;

		// Events.
		[SerializeField] UnityEvent<Texture2D> _latestTextureEvent = new UnityEvent<Texture2D>();

		Texture2D[] _textures;
		double[] _frameTimes;

		byte[] _rawImageDataBytes;

		Mat _convertMat;
		Mat _sourceMat, _undistortTargetMat;
		Mat _undistortMapX, _undistortMapY;

		ulong _latestFrameTimeMicroSeconds;
		ulong _previousFrameTimeMicroSeconds;

		long _latestFrameNum = 0; // The kinect does not provide a frame numer, only time, so we do the counting ourselves.
		int _framesSinceLastUnityUpdate = 0;

		const string logPrepend = "<b>[" + nameof( AzureKinectTexture2DProvider ) + "]</b> ";
		const double microSeconsToSeconds = 0.0000001d;
		const double sixteenToEightBitRatio = 1.0 / 256.0;

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
		public override double latestFrameInterval => ( _latestFrameTimeMicroSeconds - _previousFrameTimeMicroSeconds ) * microSeconsToSeconds;

		/// <summary>
		/// Get latest frame number.
		/// </summary>
		public override long latestFrameNumber => _latestFrameNum;

		/// <summary>
		/// Get number of frames currently stored.
		/// </summary>
		public override int frameHistoryCount => (int) ( _latestFrameNum < _frameHistoryCapacity ? _latestFrameNum : _frameHistoryCapacity );



		[Serializable]
		public enum Stream { Color, Infrared }


		/// <summary>
		/// Get latest aquired frame.
		/// </summary>
		public override Texture GetLatestTexture() => _textures?[ 0 ];


		/// <summary>
		/// Get texture at history index. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public override Texture GetHistoryTexture( int historyIndex )
		{
			if( _textures?.Length > historyIndex ) return _textures[ historyIndex ];
			return null;
		}


		/// <summary>
		/// Get latest aquired frame.
		/// </summary>
		public override Texture2D GetLatestTexture2D() => _textures?[ 0 ];


		/// <summary>
		/// Get texture at history index. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public override Texture2D GetHistoryTexture2D( int historyIndex )
		{
			if( _textures?.Length > historyIndex ) return _textures[ historyIndex ];
			return null;
		}


		/// <summary>
		/// Get latest frame time in seconds, measured relative to capture begin time. 
		/// </summary>
		public override double GetLatestFrameTime() => _latestFrameTimeMicroSeconds * microSeconsToSeconds;


		/// <summary>
		/// Get history frame time in seconds, measured relative to capture begin time. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public override double GetHistoryFrameTime( int historyIndex )
		{
			if( _frameTimes?.Length > historyIndex ) return _frameTimes[ historyIndex ];
			return 0.0;
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

			// Ensure we have a frame history.
			if( _textures?.Length != _frameHistoryCapacity ) {
				if( _textures != null ) foreach( var tex in _textures ) Destroy( tex );
				_textures = new Texture2D[ _frameHistoryCapacity ];
				_frameTimes = new double[ _frameHistoryCapacity ];
			}

			// Force settings onto KinectManager.
			switch( _stream )
			{
				case Stream.Color:
					if( kinectManager.getColorFrames == KinectManager.ColorTextureType.None ) kinectManager.getColorFrames = KinectManager.ColorTextureType.ColorTexture;
					break;
				case Stream.Infrared:
					if( kinectManager.getInfraredFrames != KinectManager.InfraredTextureType.None ) kinectManager.getInfraredFrames = KinectManager.InfraredTextureType.RawInfraredData;
					break;
			}

			// Get data and update texture.
			SensorData sensorData = kinectManager.GetSensorData( _sensorId );
			bool hasNewFrame = false;
			switch( _stream )
			{
				case Stream.Color: hasNewFrame = UpdateColorTexture( sensorData ); break;
				case Stream.Infrared: hasNewFrame = UpdateInfraredTexture( kinectManager, sensorData ); break;
			}

			// Update stats and output.
			if( hasNewFrame ) {
				_latestFrameNum++;
				_framesSinceLastUnityUpdate = 1; // We only pick one frame per Unity update, max.
				_latestTextureEvent.Invoke( GetLatestTexture2D() );
			} else {
				_framesSinceLastUnityUpdate = 0;
			}
		}


		bool UpdateColorTexture( SensorData sensorData )
		{
			if( sensorData.lastColorFrameTime == _latestFrameTimeMicroSeconds ) return false;

			if( _frameHistoryCapacity > 1 ) ShiftHistory();

			// The color texture is already a Texture2D.
			Texture2D colorTexture = sensorData.colorImageTexture as Texture2D;
			if( !colorTexture ) return false;
			if( string.IsNullOrEmpty( colorTexture.name ) ) colorTexture.name = "KinectColor (" + _sensorId + ")";

			// Ensure resources.
			int w = colorTexture.width;
			int h = colorTexture.height;
			TextureFormat format = _convertToR8 ? TextureFormat.R8 : _undistort ? TextureFormat.RGBA32 : TextureFormat.BGRA32;
			int cvType = _convertToR8 ? CvType.CV_8UC1 : CvType.CV_8UC4;
			if( _convertToR8 && ( _convertMat == null || _convertMat.width() != w || _convertMat.height() != h ) ) {
				_convertMat?.Dispose();
				_convertMat = new Mat( h, w, CvType.CV_8UC4 );
			}
			if( ( _convertToR8 || _undistort || _flipVertically ) && ( _sourceMat == null || _sourceMat.width() != w || _sourceMat.height() != h || _sourceMat.type() != cvType ) ) {
				_sourceMat?.Dispose();
				_sourceMat = new Mat( h, w, cvType );
			}
			if( _undistort && ( _undistortTargetMat == null || _undistortTargetMat.width() != w || _undistortTargetMat.height() != h || _undistortTargetMat.type() != cvType ) ) {
				_undistortTargetMat?.Dispose();
				_undistortTargetMat = new Mat( h, w, cvType );
				InitUndistortResources( w, h, sensorData.colorCamIntr );
			}
			if( !_textures[ 0 ] || format != _textures[ 0 ].format ) {
				if( _textures[ 0 ] ) Destroy( _textures[ 0 ] );
				_textures[ 0 ] = new Texture2D( w, h, format, mipChain: false, linear: false );
				_textures[ 0 ].name = "KinectColor (" + _sensorId + ")" + frameHistoryCount;
			}

			// Process.
			// The incoming texture is flipped vertically, and that is exactly what OpenCV expects. But we may want to flip it after processing.
			if( _undistort )
			{
				Utils.texture2DToMat( colorTexture, _convertToR8 ? _convertMat : _sourceMat, flipAfter: false );
				if( _convertToR8 ) TrackingToolsHelper.ColorMatToLumanceMat( _convertMat, _sourceMat );
				Imgproc.remap( _sourceMat, _undistortTargetMat, _undistortMapX, _undistortMapY, Imgproc.INTER_LINEAR );
				Utils.fastMatToTexture2D( _undistortTargetMat, _textures[ 0 ], _flipVertically );

			} else if( _convertToR8 ){

				Utils.texture2DToMat( colorTexture, _convertMat, flipAfter: false );
				TrackingToolsHelper.ColorMatToLumanceMat( _convertMat, _sourceMat );
				Utils.fastMatToTexture2D( _sourceMat, _textures[ 0 ], _flipVertically );

			} else if( _flipVertically ){

				Utils.texture2DToMat( colorTexture, _sourceMat, flipAfter: false );
				Utils.fastMatToTexture2D( _sourceMat, _textures[ 0 ], _flipVertically );

			} else {

				Graphics.CopyTexture( colorTexture, _textures[ 0 ] );
			}

			_previousFrameTimeMicroSeconds = _latestFrameTimeMicroSeconds;
			_latestFrameTimeMicroSeconds = sensorData.lastColorFrameTime;
			_frameTimes[ 0 ] = _latestFrameTimeMicroSeconds * microSeconsToSeconds;

			return true;
		}


		bool UpdateInfraredTexture( KinectManager kinectManager, SensorData sensorData )
		{
			if( sensorData.lastInfraredFrameTime == _latestFrameTimeMicroSeconds ) return false;

			if( _frameHistoryCapacity > 1 ) ShiftHistory();

			// Azure Kinect Examples unpacks IR on the GPU into a RenderTexture. We need a Texture2D. Also:
			// Azure Kinect Examples loads IR as RGB24, but the IR is actually R16, so we loose a lot of information.
			// If you look IR in the official Azure Kinect Viewver it matches Azure Kinet Examples. They are very bright, and typically burned out.
			// We want the full spectrum, so we load 16bit directly to texture.
			int w = sensorData.depthImageWidth;
			int h = sensorData.depthImageHeight;
			
			// Get raw image data.
			ushort[] rawImageData = kinectManager.GetRawInfraredMap( _sensorId );

			// Ensure resources.
			int cvType = _convertToR8 ? CvType.CV_8UC1 : CvType.CV_16U;
			TextureFormat format = _convertToR8 ? TextureFormat.R8 : TextureFormat.R16;
			int pixelCount = w * h;
			if( _rawImageDataBytes?.Length != pixelCount * 2 ) {
				_rawImageDataBytes = new byte[ pixelCount * 2 ];
			}
			if( _convertToR8 && ( _convertMat == null || _convertMat.width() != w || _convertMat.height() != h ) ) {
				_convertMat?.Dispose();
				_convertMat = new Mat( h, w, CvType.CV_16U );
			}
			if( ( _undistort || _convertToR8 || _flipVertically ) && ( _sourceMat == null || _sourceMat.width() != w || _sourceMat.height() != h || _sourceMat.type() != cvType ) ) {
				_sourceMat?.Dispose();
				_sourceMat = new Mat( h, w, cvType );
			}
			if( _undistort && ( _undistortTargetMat == null || _undistortTargetMat.width() != w || _undistortTargetMat.height() != h || _undistortTargetMat.type() != cvType ) ) {
				_undistortTargetMat?.Dispose();
				_undistortTargetMat = new Mat( h, w, cvType );
				InitUndistortResources( w, h, sensorData.depthCamIntr );
			}
			if( !_textures[ 0 ] || _textures[ 0 ].width != w || _textures[ 0 ].height != h || _textures[ 0 ].format != format ) {
				if( _textures[ 0 ] ) Destroy( _textures[ 0 ] );
				_textures[ 0 ] = new Texture2D( w, h, format, mipChain: false, linear: false );
				_textures[ 0 ].name = "KinectInfrared (" + _sensorId + ") " + frameHistoryCount;
			}

			// Process.
			if( _undistort || _convertToR8 ||_flipVertically )
			{		
				// Copy to mat.
				GCHandle arrayHandle = GCHandle.Alloc( rawImageData, GCHandleType.Pinned );
				MatUtils.copyToMat( arrayHandle.AddrOfPinnedObject(), _convertToR8 ? _convertMat : _sourceMat );
				arrayHandle.Free();

				// Convert.
				if( _convertToR8 ) _convertMat.convertTo( _sourceMat, CvType.CV_8U, sixteenToEightBitRatio * _infrared16BitScalar );

				// Undistort
				if( _undistort ) Imgproc.remap( _sourceMat, _undistortTargetMat, _undistortMapX, _undistortMapY, Imgproc.INTER_LINEAR );

				// Copy to texture.
				Utils.fastMatToTexture2D( _undistort ? _undistortTargetMat : _sourceMat, _textures[ 0 ], _flipVertically );
				
			} else {

				// ushort[] to byte[].
				// https://stackoverflow.com/questions/37213819/convert-ushort-into-byte-and-back
				Buffer.BlockCopy( rawImageData, 0, _rawImageDataBytes, 0, rawImageData.Length * 2 );

				// Load into texture.
				_textures[ 0 ].LoadRawTextureData( _rawImageDataBytes );
				_textures[ 0 ].Apply();
			}

			_previousFrameTimeMicroSeconds = _latestFrameTimeMicroSeconds;
			_latestFrameTimeMicroSeconds = sensorData.lastInfraredFrameTime;
			_frameTimes[ 0 ] = _latestFrameTimeMicroSeconds * microSeconsToSeconds;

			return true;
		}



		void InitUndistortResources( int w, int h, CameraIntrinsics rfilkovIntrinsics )
		{
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



		void ShiftHistory()
		{
			var tempTex = _textures[ _textures.Length-1 ]; // Recycle.
			for( int t = _textures.Length-1; t > 0; t-- ){
				_textures[ t ] = _textures[ t-1 ];
				_frameTimes[ t ] = _frameTimes[ t-1 ];
			}
			_textures[ 0 ] = tempTex;
		}


		void OnDestroy()
		{
			foreach( var tex in _textures ) Destroy( tex );
			_sourceMat?.Dispose();
			_undistortTargetMat?.Dispose();
			_undistortMapX?.Dispose();
			_undistortMapY?.Dispose();
			_convertMat?.Dispose();
		}
	}

}