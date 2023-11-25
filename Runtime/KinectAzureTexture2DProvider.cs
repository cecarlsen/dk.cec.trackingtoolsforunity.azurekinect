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

namespace TrackingTools
{
	public class KinectAzureTexture2DProvider : MonoBehaviour
	{
		[SerializeField] int _sensorId = 0;
		[SerializeField] bool _undistortIR = false;

		[Header("Output")]
		[SerializeField] UnityEvent<Texture2D> _colorTexture2DEvent = new UnityEvent<Texture2D>();
		[SerializeField] UnityEvent<Texture2D> _irTexture2DEvent = new UnityEvent<Texture2D>();

		Texture2D _irTexture;
		byte[] _rawImageDataBytes;

		bool _colorEnabled;
		bool _irEnabled;

		Mat _irMat, _irUndistortedMat;
		Mat _undistortMapX, _undistortMapY;

		ulong _lastIRFrameTime;
		ulong _lastColorFrameTime;

		const string logPrepend = "<b>[" + nameof( KinectAzureTexture2DProvider ) + "]</b> ";


		void Awake()
		{
			_colorEnabled = _colorTexture2DEvent != null && _colorTexture2DEvent.GetPersistentEventCount() > 0;
			_irEnabled = _irTexture2DEvent != null && _irTexture2DEvent.GetPersistentEventCount() > 0;
		}


		void Update()
		{
			KinectManager kinectManager = KinectManager.Instance;
			if( !kinectManager || !kinectManager.IsInitialized() ) return;
			
			if( _colorEnabled && kinectManager.getColorFrames == KinectManager.ColorTextureType.None ) kinectManager.getColorFrames = KinectManager.ColorTextureType.ColorTexture;
			if( _irEnabled && kinectManager.getInfraredFrames != KinectManager.InfraredTextureType.None ) kinectManager.getInfraredFrames = KinectManager.InfraredTextureType.RawInfraredData;


			KinectInterop.SensorData sensorData = kinectManager.GetSensorData( _sensorId );

			if( _colorEnabled ) UpdateColorTexture( kinectManager, sensorData );
		 	
			if( _irEnabled ) UpdateIRTexture( kinectManager, sensorData );
		}


		void UpdateColorTexture( KinectManager kinectManager, KinectInterop.SensorData sensorData )
		{
			if( sensorData.lastColorFrameTime == _lastColorFrameTime ) return;
			
			// The color texture is already a Texture2D, so we just output it.
			Texture colorTexture = kinectManager.GetColorImageTex( _sensorId );
			if( colorTexture ){
				if( string.IsNullOrEmpty( colorTexture.name ) ) colorTexture.name = "KinectColor";
				_colorTexture2DEvent.Invoke( colorTexture as Texture2D );
			}
			_lastColorFrameTime = sensorData.lastColorFrameTime;
		}


		void UpdateIRTexture( KinectManager kinectManager, KinectInterop.SensorData sensorData )
		{
			if( sensorData.lastInfraredFrameTime == _lastIRFrameTime ) return;
			
			// Azure Kinect Examples unpacks IR on the GPU into a RenderTexture. We need a Texture2D. Also:

			// Azure Kinect Examples loads IR as RGB24, but the IR is actually R16, so we loose a lot of information.
			// If you look IR in the official Azure Kinect Viewver it matches Azure Kinet Examples. They are very bright, and typically burned out.
			// We want the full spectrum, so we load 16bit directly to texture.
			int width = sensorData.depthImageWidth;
			int height = sensorData.depthImageHeight;
				
			// Create texture.
			if( !_irTexture ) {
				int pixelCount = width * height;
				_irTexture = new Texture2D( width, height, GraphicsFormat.R16_UNorm, TextureCreationFlags.None );
				_irTexture.name = "KinectIR";
				_rawImageDataBytes = new byte[ pixelCount * 2 ];
			}

			// Get raw image data.
			ushort[] rawImageData = kinectManager.GetRawInfraredMap( _sensorId );

			// Currently not working.
			if( _undistortIR ){
					
				// Adapt resources.
				if( _irMat == null || _irMat.width() != width || _irMat.height() != height ){
					_irMat?.Dispose();
					_irUndistortedMat?.Dispose();
					_irMat = new Mat( height, width, CvType.CV_16U );
					_irUndistortedMat = new Mat( height, width, CvType.CV_16U );

					if( _undistortMapX == null ){
						_undistortMapX = new Mat();
						_undistortMapY = new Mat();
					}
					Intrinsics intrinsics = new Intrinsics();
					intrinsics.UpdateFromAzureKinectExamples( sensorData.depthCamIntr );
					Mat _sensorMat = new Mat();
					MatOfDouble _distortionCoeffsMat = new MatOfDouble();
					bool success = intrinsics.ApplyToToOpenCV( ref _sensorMat, ref _distortionCoeffsMat );//, w, h );
					if( !success ){
						Debug.LogWarning( logPrepend + "Failed loading intrinsics\n" );
						return;
					}
					Calib3d.initUndistortRectifyMap( _sensorMat, _distortionCoeffsMat, new Mat(), _sensorMat, new Size( width, height ), CvType.CV_32FC1, _undistortMapX, _undistortMapY );
					_sensorMat.Dispose();
					_distortionCoeffsMat.Dispose();
				}

				// Copy to mat.
				GCHandle arrayHandle = GCHandle.Alloc( rawImageData, GCHandleType.Pinned );
				MatUtils.copyToMat( arrayHandle.AddrOfPinnedObject(), _irMat );
				arrayHandle.Free();

				// Undistort
				Imgproc.remap( _irMat, _irUndistortedMat, _undistortMapX, _undistortMapY, Imgproc.INTER_LINEAR );

				// Copy to texture.
				Utils.fastMatToTexture2D( _irUndistortedMat, _irTexture, flip: false );
					
			} else {
			
				// ushort[] to byte[].
				// https://stackoverflow.com/questions/37213819/convert-ushort-into-byte-and-back
				Buffer.BlockCopy( rawImageData, 0, _rawImageDataBytes, 0, rawImageData.Length * 2 );

				// Load into texture.
				_irTexture.LoadRawTextureData( _rawImageDataBytes );
				_irTexture.Apply();
			}

			// Output.
			_irTexture2DEvent.Invoke( _irTexture );

			// Store time.
			_lastIRFrameTime = sensorData.lastInfraredFrameTime;
		}


		void OnDestroy()
		{
			if( _irTexture ) Destroy( _irTexture );
			_irMat?.Dispose();
			_irUndistortedMat?.Dispose();
			_undistortMapX?.Dispose();
			_undistortMapY?.Dispose();
		}
	}

}